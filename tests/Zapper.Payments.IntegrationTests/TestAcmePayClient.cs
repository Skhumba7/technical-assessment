using Zapper.Payments.Application.DTOs;
using Zapper.Payments.Application.Interfaces;

namespace Zapper.Payments.IntegrationTests;

public class TestAcmePayClient : IAcmePayClient
{
    public bool SimulateTimeout { get; set; }
    public string DesiredTransactionStatus { get; set; } = "Success";
    public string DesiredResponseCode { get; set; } = "00";
    public string DesiredAuthCode { get; set; } = "238699";
    public string? DesiredReconId { get; set; } = "MR747891457";
    public string? DesiredDescription { get; set; } = "Approved or completed successfully";
    public string? DesiredClientMessage { get; set; } = "Approved.";

    public int SubmitCallsCount { get; private set; }
    public int RttqCallsCount { get; private set; }

    public Task<AcmeResponseEnvelope> SubmitPaymentAsync(
        AcmePaymentEnvelope request,
        string correlationId,
        CancellationToken ct = default)
    {
        SubmitCallsCount++;

        if (SimulateTimeout)
        {
            throw new TimeoutException("Simulated Acme Pay gateway timeout.");
        }

        var isSuccess = DesiredTransactionStatus.Equals("Success", StringComparison.OrdinalIgnoreCase);

        var response = new AcmeResponseEnvelope(
            MessageHeader: new AcmeMessageHeader(
                MessageDateTime: DateTime.UtcNow.AddHours(2).ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                MessageID: Guid.NewGuid().ToString("D"),
                Channel: "App"
            ),
            PaymentResponse: new AcmePaymentResponsePayload(
                TransactionID: request.PaymentRequest.TransactionID,
                TransactionDateTime: DateTime.UtcNow.AddHours(2).ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                OrderNumber: request.PaymentRequest.OrderNumber,
                ReconID: DesiredReconId,
                RetrievalReferenceNumber: request.PaymentRequest.OrderNumber.Substring(0, 12),
                AuthCode: isSuccess ? DesiredAuthCode : null,
                TransactionStatus: DesiredTransactionStatus,
                ResponseDetail: new AcmeResponseDetail(
                    Source: "Acquirer",
                    Code: DesiredResponseCode,
                    Description: DesiredDescription ?? "",
                    ClientMessage: DesiredClientMessage ?? ""
                ),
                AuthAmount: isSuccess ? request.PaymentRequest.Amount : null
            )
        );

        return Task.FromResult(response);
    }

    public Task<AcmeResponseEnvelope?> QueryRealTimeTransactionAsync(
        string merchantId,
        string? transactionId,
        string? orderNumber,
        string correlationId,
        CancellationToken ct = default)
    {
        RttqCallsCount++;

        var response = new AcmeResponseEnvelope(
            MessageHeader: new AcmeMessageHeader(
                MessageDateTime: DateTime.UtcNow.AddHours(2).ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                MessageID: Guid.NewGuid().ToString("D"),
                Channel: "App"
            ),
            PaymentResponse: new AcmePaymentResponsePayload(
                TransactionID: transactionId ?? Guid.NewGuid().ToString("D"),
                TransactionDateTime: DateTime.UtcNow.AddHours(2).ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                OrderNumber: orderNumber ?? "MR1234567890",
                ReconID: DesiredReconId,
                RetrievalReferenceNumber: (orderNumber ?? "MR1234567890").Substring(0, 12),
                AuthCode: DesiredAuthCode,
                TransactionStatus: DesiredTransactionStatus,
                ResponseDetail: new AcmeResponseDetail("Acquirer", DesiredResponseCode, DesiredDescription ?? "", DesiredClientMessage ?? ""),
                AuthAmount: 1999
            )
        );

        return Task.FromResult<AcmeResponseEnvelope?>(response);
    }
}
