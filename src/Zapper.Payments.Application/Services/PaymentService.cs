using Microsoft.Extensions.Logging;
using Zapper.Payments.Application.DTOs;
using Zapper.Payments.Application.Interfaces;
using Zapper.Payments.Domain.Aggregates;
using Zapper.Payments.Domain.Common;
using Zapper.Payments.Domain.Entities;
using Zapper.Payments.Domain.Enums;
using Zapper.Payments.Domain.Repositories;
using Zapper.Payments.Domain.ValueObjects;

namespace Zapper.Payments.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ISubMerchantRepository _subMerchantRepository;
    private readonly ICustomerCardRepository _customerCardRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAcmePayClient _acmePayClient;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IPaymentRepository paymentRepository,
        ISubMerchantRepository subMerchantRepository,
        ICustomerCardRepository customerCardRepository,
        IAuditRepository auditRepository,
        IUnitOfWork unitOfWork,
        IAcmePayClient acmePayClient,
        ILogger<PaymentService> logger)
    {
        _paymentRepository = paymentRepository;
        _subMerchantRepository = subMerchantRepository;
        _customerCardRepository = customerCardRepository;
        _auditRepository = auditRepository;
        _unitOfWork = unitOfWork;
        _acmePayClient = acmePayClient;
        _logger = logger;
    }

    public async Task<SubmitPaymentResult> SubmitPaymentAsync(
        SubmitPaymentRequest request,
        string correlationId,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Processing payment submission for AppPaymentId {AppPaymentId}, Customer {CustomerId}, Merchant {MerchantId}",
            request.AppPaymentId, request.CustomerId, request.SubMerchantId);

        // 1. Idempotency Check: Same app payment identifier submitted twice
        var existing = await _paymentRepository.GetByAppPaymentIdAsync(request.AppPaymentId, ct);
        if (existing != null)
        {
            _logger.LogWarning("Duplicate payment submission detected for AppPaymentId {AppPaymentId}. Returning existing payment state.", request.AppPaymentId);
            return new SubmitPaymentResult(
                existing.Id,
                existing.AppPaymentId,
                existing.OrderNumber,
                existing.RetrievalReferenceNumber,
                existing.Status,
                existing.LastResponseCode,
                existing.AuthCode,
                existing.AuthAmount,
                existing.ClientMessage,
                IsDuplicateSubmission: true
            );
        }

        // 2. Validate dependencies
        var subMerchant = await _subMerchantRepository.GetByIdAsync(request.SubMerchantId, ct)
            ?? throw new DomainException($"Sub-merchant '{request.SubMerchantId}' not found.", "MERCHANT_NOT_FOUND");

        var customerCard = await _customerCardRepository.GetByCustomerIdAsync(request.CustomerId, ct)
            ?? throw new DomainException($"Stored card for customer '{request.CustomerId}' not found.", "CARD_NOT_FOUND");

        // 3. Create domain aggregate
        var money = new Money(request.AmountInMinorUnits, request.Currency);
        var payment = new Payment(
            request.AppPaymentId,
            request.CustomerId,
            request.SubMerchantId,
            customerCard.AcmeToken,
            money
        );

        await _paymentRepository.AddAsync(payment, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // 4. Construct Acme Pay Request
        var transactionId = Guid.NewGuid().ToString("D");
        payment.MarkSubmittedToGateway(transactionId);
        await _unitOfWork.SaveChangesAsync(ct);

        var cardAcceptorName = subMerchant.GetCardAcceptorName("ZAP");
        var nowSast = DateTime.UtcNow.AddHours(2).ToString("yyyy-MM-ddTHH:mm:ss.fff");

        var envelope = new AcmePaymentEnvelope(
            MessageHeader: new AcmeMessageHeader(
                MessageDateTime: nowSast,
                MessageID: Guid.NewGuid().ToString("D"),
                Channel: "App"
            ),
            PaymentRequest: new AcmePaymentRequestPayload(
                MerchantID: "8B67BF88-BB8D-4EB5-ADFC-0F5C97C7ED67", // Zapper account MerchantID
                TransactionID: transactionId,
                OrderNumber: payment.OrderNumber,
                Amount: payment.Amount.AmountInMinorUnits,
                CurrencyCode: payment.Amount.Currency,
                PaymentService: "CardNotPresent",
                Card: new AcmeCard(Token: customerCard.AcmeToken),
                CardOnFileScenario: "CIT",
                SubMerchantID: subMerchant.SubMerchantId,
                MerchantCategoryCode: subMerchant.MerchantCategoryCode,
                CardAcceptorName: cardAcceptorName.FormattedValue,
                Email: customerCard.Email
            )
        );

        // 5. Submit to Gateway with Resilience & Timeout Handling
        try
        {
            var response = await _acmePayClient.SubmitPaymentAsync(envelope, correlationId, ct);
            var isSuccess = response.PaymentResponse.TransactionStatus.Equals("Success", StringComparison.OrdinalIgnoreCase);

            payment.ApplyGatewayResponse(
                isSuccess,
                response.PaymentResponse.ResponseDetail?.Code ?? (isSuccess ? "00" : "E50"),
                response.PaymentResponse.ReconID,
                response.PaymentResponse.AuthCode,
                response.PaymentResponse.AuthAmount,
                response.PaymentResponse.ResponseDetail?.Description,
                response.PaymentResponse.ResponseDetail?.ClientMessage
            );
        }
        catch (TimeoutException tex)
        {
            _logger.LogWarning(tex, "Timeout submitting payment {PaymentId} to Acme. Attempting RTTQ query.", payment.Id);
            payment.MarkTimedOut("Acme Pay gateway timeout");

            // Real Time Transaction Query (RTTQ) fallback per specification
            try
            {
                var rttq = await _acmePayClient.QueryRealTimeTransactionAsync(
                    envelope.PaymentRequest.MerchantID,
                    transactionId,
                    payment.OrderNumber,
                    correlationId,
                    ct
                );

                if (rttq != null)
                {
                    payment.ResolveFromRttq(
                        rttq.PaymentResponse.TransactionStatus,
                        rttq.PaymentResponse.ResponseDetail?.Code,
                        rttq.PaymentResponse.ReconID,
                        rttq.PaymentResponse.AuthCode,
                        rttq.PaymentResponse.AuthAmount,
                        rttq.PaymentResponse.ResponseDetail?.Description
                    );
                }
            }
            catch (Exception rttqEx)
            {
                _logger.LogError(rttqEx, "RTTQ query for payment {PaymentId} failed.", payment.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error communicating with Acme for payment {PaymentId}.", payment.Id);
            payment.ApplyGatewayResponse(
                false,
                "E50",
                null,
                null,
                null,
                "Gateway communication failure",
                "We could not process that payment at this time."
            );
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return new SubmitPaymentResult(
            payment.Id,
            payment.AppPaymentId,
            payment.OrderNumber,
            payment.RetrievalReferenceNumber,
            payment.Status,
            payment.LastResponseCode,
            payment.AuthCode,
            payment.AuthAmount,
            payment.ClientMessage
        );
    }

    public async Task<PaymentDto?> GetPaymentByIdAsync(Guid paymentId, CancellationToken ct = default)
    {
        var payment = await _paymentRepository.GetByIdAsync(paymentId, ct);
        return payment is null ? null : MapToDto(payment);
    }

    public async Task<PaymentDto?> GetPaymentByAppIdAsync(string appPaymentId, CancellationToken ct = default)
    {
        var payment = await _paymentRepository.GetByAppPaymentIdAsync(appPaymentId, ct);
        return payment is null ? null : MapToDto(payment);
    }

    public async Task<bool> HandleAdviceNotificationAsync(
        PaymentAdvicePayload advice,
        string rawPayload,
        string? authHeader,
        CancellationToken ct = default)
    {
        var notificationLog = new PaymentNotificationLog(
            "PaymentAdvice",
            advice.TransactionID,
            rawPayload,
            authHeader
        );

        Payment? payment = null;
        if (!string.IsNullOrWhiteSpace(advice.RetrievalReferenceNumber))
        {
            payment = await _paymentRepository.GetByRetrievalReferenceNumberAsync(advice.RetrievalReferenceNumber, ct);
        }

        if (payment == null && !string.IsNullOrWhiteSpace(advice.OrderNumber))
        {
            payment = await _paymentRepository.GetByOrderNumberAsync(advice.OrderNumber, ct);
        }

        if (payment == null && !string.IsNullOrWhiteSpace(advice.TransactionID))
        {
            payment = await _paymentRepository.GetByTransactionIdAsync(advice.TransactionID, ct);
        }

        if (payment != null)
        {
            payment.ApplyAdviceNotification(
                advice.ResponseCode,
                advice.ReconID,
                advice.AuthCode,
                advice.Amount,
                advice.ResponseDescription
            );

            notificationLog.MarkProcessed(payment.Id, $"Applied to payment {payment.Id}");
            await _auditRepository.LogNotificationAsync(notificationLog, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return true;
        }

        notificationLog.MarkFailed("No matching payment record found for notification.");
        await _auditRepository.LogNotificationAsync(notificationLog, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> HandleSwitchWebhookAsync(
        SwitchWebhookPayload webhook,
        string rawPayload,
        string? signatureHeader,
        CancellationToken ct = default)
    {
        var notificationLog = new PaymentNotificationLog(
            "SwitchWebhook",
            webhook.transactionUUID,
            rawPayload,
            signatureHeader
        );

        var payment = await _paymentRepository.GetByRetrievalReferenceNumberAsync(webhook.rrn, ct);
        if (payment != null)
        {
            if (webhook.transactionType.Equals("Reversal", StringComparison.OrdinalIgnoreCase))
            {
                payment.ApplyReversal(webhook.transactionUUID, webhook.transactionAmount);
            }
            else if (webhook.transactionType.Equals("Refund", StringComparison.OrdinalIgnoreCase))
            {
                payment.ApplyRefund(webhook.transactionUUID, webhook.transactionAmount);
            }

            notificationLog.MarkProcessed(payment.Id, $"Switch event {webhook.transactionType} applied to payment {payment.Id}");
            await _auditRepository.LogNotificationAsync(notificationLog, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return true;
        }

        notificationLog.MarkFailed($"No payment found matching RRN {webhook.rrn}");
        await _auditRepository.LogNotificationAsync(notificationLog, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DailySettlementSummaryDto> GetDailySettlementSummaryAsync(
        DateOnly date,
        CancellationToken ct = default)
    {
        var startUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var records = await _paymentRepository.GetDailySettlementSummaryAsync(startUtc, endUtc, ct);

        var subMerchantSummaries = records.Select(r => new SubMerchantSettlementSummaryDto(
            r.SubMerchantId,
            r.TradingName,
            r.Currency,
            r.ApprovedCount,
            r.ApprovedTotalValue,
            r.DeclinedCount,
            r.DeclinedTotalValue,
            r.ReversalCount,
            r.ReversalTotalValue,
            r.RefundCount,
            r.RefundTotalValue
        )).ToList();

        return new DailySettlementSummaryDto(date, subMerchantSummaries);
    }

    private static PaymentDto MapToDto(Payment payment)
    {
        return new PaymentDto(
            payment.Id,
            payment.AppPaymentId,
            payment.CustomerId,
            payment.SubMerchantId,
            payment.Amount.AmountInMinorUnits,
            payment.Amount.Currency,
            payment.OrderNumber,
            payment.RetrievalReferenceNumber,
            payment.TransactionId,
            payment.Status,
            payment.AuthCode,
            payment.AuthAmount,
            payment.RefundedAmount,
            payment.LastResponseCode,
            payment.FailureReason,
            payment.ClientMessage,
            payment.CreatedAtUtc,
            payment.UpdatedAtUtc,
            payment.History.Select(h => new PaymentHistoryDto(
                h.FromStatus,
                h.ToStatus,
                h.Reason,
                h.Source,
                h.TimestampUtc
            )).ToList()
        );
    }
}
