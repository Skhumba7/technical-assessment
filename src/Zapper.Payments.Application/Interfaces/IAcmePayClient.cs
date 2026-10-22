using Zapper.Payments.Application.DTOs;

namespace Zapper.Payments.Application.Interfaces;

public interface IAcmePayClient
{
    Task<AcmeResponseEnvelope> SubmitPaymentAsync(
        AcmePaymentEnvelope request,
        string correlationId,
        CancellationToken ct = default);

    Task<AcmeResponseEnvelope?> QueryRealTimeTransactionAsync(
        string merchantId,
        string? transactionId,
        string? orderNumber,
        string correlationId,
        CancellationToken ct = default);
}
