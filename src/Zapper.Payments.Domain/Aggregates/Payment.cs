using Zapper.Payments.Domain.Common;
using Zapper.Payments.Domain.Entities;
using Zapper.Payments.Domain.Enums;
using Zapper.Payments.Domain.ValueObjects;

namespace Zapper.Payments.Domain.Aggregates;

public class Payment
{
    private readonly List<PaymentHistoryEntry> _history = new();
    private readonly HashSet<string> _processedSwitchUuids = new(StringComparer.OrdinalIgnoreCase);

    public Guid Id { get; private set; }
    public string AppPaymentId { get; private set; } = string.Empty;
    public string CustomerId { get; private set; } = string.Empty;
    public string SubMerchantId { get; private set; } = string.Empty;
    public string CardToken { get; private set; } = string.Empty;
    public Money Amount { get; private set; } = null!;
    public string OrderNumber { get; private set; } = string.Empty;
    public string RetrievalReferenceNumber { get; private set; } = string.Empty;
    public string? TransactionId { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? ReconId { get; private set; }
    public string? AuthCode { get; private set; }
    public long? AuthAmount { get; private set; }
    public long RefundedAmount { get; private set; }
    public string? LastResponseCode { get; private set; }
    public string? FailureReason { get; private set; }
    public string? ClientMessage { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<PaymentHistoryEntry> History => _history.AsReadOnly();

    // EF Core constructor
    protected Payment() { }

    public Payment(
        string appPaymentId,
        string customerId,
        string subMerchantId,
        string cardToken,
        Money amount)
    {
        if (string.IsNullOrWhiteSpace(appPaymentId))
            throw new DomainException("AppPaymentId cannot be empty.", "INVALID_APP_PAYMENT_ID");
        if (string.IsNullOrWhiteSpace(customerId))
            throw new DomainException("CustomerId cannot be empty.", "INVALID_CUSTOMER_ID");
        if (string.IsNullOrWhiteSpace(subMerchantId))
            throw new DomainException("SubMerchantId cannot be empty.", "INVALID_SUB_MERCHANT_ID");
        if (string.IsNullOrWhiteSpace(cardToken))
            throw new DomainException("CardToken cannot be empty.", "INVALID_CARD_TOKEN");

        Id = Guid.NewGuid();
        AppPaymentId = appPaymentId.Trim();
        CustomerId = customerId.Trim();
        SubMerchantId = subMerchantId.Trim();
        CardToken = cardToken.Trim();
        ArgumentNullException.ThrowIfNull(amount);
        Amount = amount;

        // OrderNumber: alphanumeric, first 12 chars become the RetrievalReferenceNumber (RRN)
        // Format: "MR" + 10 digits/hex
        string uniquePart = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant();
        OrderNumber = $"MR{uniquePart}";
        RetrievalReferenceNumber = OrderNumber.Substring(0, 12);

        Status = PaymentStatus.Initiated;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;

        AddHistoryEntry(PaymentStatus.Initiated, "Payment initiated", "Client");
    }

    public void MarkSubmittedToGateway(string transactionId)
    {
        if (Status != PaymentStatus.Initiated)
        {
            throw new DomainException($"Cannot submit payment in status {Status} to gateway.", "INVALID_TRANSITION");
        }

        TransactionId = transactionId;
        ChangeStatus(PaymentStatus.SubmittedToGateway, $"Submitted to Acme Pay with TransactionID {transactionId}", "System");
    }

    public void ApplyGatewayResponse(
        bool isSuccess,
        string responseCode,
        string? reconId,
        string? authCode,
        long? authAmount,
        string? description,
        string? clientMessage)
    {
        LastResponseCode = responseCode;
        FailureReason = description;
        ClientMessage = clientMessage;

        if (reconId != null) ReconId = reconId;
        if (authCode != null) AuthCode = authCode;

        if (isSuccess && responseCode == "00")
        {
            AuthAmount = authAmount ?? Amount.AmountInMinorUnits;
            ChangeStatus(PaymentStatus.Approved, description ?? "Approved by acquirer", "AcmeApi");
        }
        else
        {
            ChangeStatus(PaymentStatus.Declined, description ?? "Declined by acquirer/gateway", "AcmeApi");
        }
    }

    public void ApplyAdviceNotification(
        string responseCode,
        string? reconId,
        string? authCode,
        long? authAmount,
        string? description)
    {
        LastResponseCode = responseCode;
        if (reconId != null) ReconId = reconId;
        if (authCode != null) AuthCode = authCode;

        // Idempotency check: if already terminal and matches outcome, log duplicate delivery
        if (Status == PaymentStatus.Approved && responseCode == "00")
        {
            AddHistoryEntry(Status, "Duplicate approval advice received (idempotent ignore)", "AdviceNotification");
            return;
        }

        if (Status == PaymentStatus.Declined && responseCode != "00")
        {
            AddHistoryEntry(Status, "Duplicate decline advice received (idempotent ignore)", "AdviceNotification");
            return;
        }

        // Final state protection: Reversals and refunds are terminal post-approval states
        if (Status == PaymentStatus.Reversed || Status == PaymentStatus.Refunded || Status == PaymentStatus.PartiallyRefunded)
        {
            AddHistoryEntry(Status, $"Out-of-order advice ignored for payment in {Status} status", "AdviceNotification");
            return;
        }

        // If previously timed out or in flight, update based on advice
        if (responseCode == "00")
        {
            AuthAmount = authAmount ?? Amount.AmountInMinorUnits;
            ChangeStatus(PaymentStatus.Approved, description ?? "Approved via payment advice", "AdviceNotification");
        }
        else
        {
            ChangeStatus(PaymentStatus.Declined, description ?? "Declined via payment advice", "AdviceNotification");
        }
    }

    public void ApplyReversal(string transactionUuid, long amount, string? note = null)
    {
        if (!_processedSwitchUuids.Add(transactionUuid))
        {
            AddHistoryEntry(Status, $"Duplicate reversal webhook ignored for UUID {transactionUuid}", "SwitchWebhook");
            return;
        }

        if (Status != PaymentStatus.Approved && Status != PaymentStatus.PartiallyRefunded)
        {
            AddHistoryEntry(Status, $"Out-of-order reversal rejected; current status is {Status}", "SwitchWebhook");
            throw new DomainException($"Cannot reverse payment in status {Status}. Payment must be Approved.", "INVALID_REVERSAL");
        }

        ChangeStatus(PaymentStatus.Reversed, note ?? $"Reversed via Switch webhook {transactionUuid} for amount {amount}", "SwitchWebhook");
    }

    public void ApplyRefund(string transactionUuid, long refundAmount, string? note = null)
    {
        if (refundAmount <= 0)
        {
            throw new DomainException("Refund amount must be positive.", "INVALID_REFUND_AMOUNT");
        }

        if (!_processedSwitchUuids.Add(transactionUuid))
        {
            AddHistoryEntry(Status, $"Duplicate refund webhook ignored for UUID {transactionUuid}", "SwitchWebhook");
            return;
        }

        if (Status != PaymentStatus.Approved && Status != PaymentStatus.PartiallyRefunded)
        {
            AddHistoryEntry(Status, $"Out-of-order refund rejected; current status is {Status}", "SwitchWebhook");
            throw new DomainException($"Cannot refund payment in status {Status}. Payment must be Approved or PartiallyRefunded.", "INVALID_REFUND");
        }

        long totalAuthorized = AuthAmount ?? Amount.AmountInMinorUnits;
        if (RefundedAmount + refundAmount > totalAuthorized)
        {
            throw new DomainException($"Refund amount {refundAmount} exceeds remaining refundable balance.", "EXCESSIVE_REFUND");
        }

        RefundedAmount += refundAmount;

        var targetStatus = RefundedAmount >= totalAuthorized 
            ? PaymentStatus.Refunded 
            : PaymentStatus.PartiallyRefunded;

        ChangeStatus(targetStatus, note ?? $"Refunded {refundAmount} (Total: {RefundedAmount}/{totalAuthorized}) via Switch UUID {transactionUuid}", "SwitchWebhook");
    }

    public void MarkTimedOut(string reason)
    {
        if (Status != PaymentStatus.SubmittedToGateway)
        {
            // Already finalized by a concurrent callback or response
            return;
        }

        ChangeStatus(PaymentStatus.TimedOut, reason, "SystemTimeout");
    }

    public void ResolveFromRttq(
        string transactionStatus,
        string? responseCode,
        string? reconId,
        string? authCode,
        long? authAmount,
        string? description)
    {
        if (reconId != null) ReconId = reconId;
        if (authCode != null) AuthCode = authCode;
        LastResponseCode = responseCode;

        if (transactionStatus.Equals("Success", StringComparison.OrdinalIgnoreCase) && responseCode == "00")
        {
            AuthAmount = authAmount ?? Amount.AmountInMinorUnits;
            ChangeStatus(PaymentStatus.Approved, description ?? "Approved resolved via RTTQ", "AcmeRTTQ");
        }
        else if (transactionStatus.Equals("Processing", StringComparison.OrdinalIgnoreCase))
        {
            AddHistoryEntry(Status, "RTTQ indicated transaction is still processing at Acme", "AcmeRTTQ");
        }
        else
        {
            ChangeStatus(PaymentStatus.Declined, description ?? "Declined resolved via RTTQ", "AcmeRTTQ");
        }
    }

    private void ChangeStatus(PaymentStatus newStatus, string reason, string source)
    {
        var oldStatus = Status;
        Status = newStatus;
        UpdatedAtUtc = DateTime.UtcNow;
        AddHistoryEntry(oldStatus, newStatus, reason, source);
    }

    private void AddHistoryEntry(PaymentStatus status, string reason, string source)
    {
        _history.Add(new PaymentHistoryEntry(Id, status, status, reason, source));
    }

    private void AddHistoryEntry(PaymentStatus fromStatus, PaymentStatus toStatus, string reason, string source)
    {
        _history.Add(new PaymentHistoryEntry(Id, fromStatus, toStatus, reason, source));
    }
}
