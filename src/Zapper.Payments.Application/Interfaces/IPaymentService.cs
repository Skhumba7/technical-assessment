using Zapper.Payments.Application.DTOs;

namespace Zapper.Payments.Application.Interfaces;

public interface IPaymentService
{
    Task<SubmitPaymentResult> SubmitPaymentAsync(
        SubmitPaymentRequest request,
        string correlationId,
        CancellationToken ct = default);

    Task<PaymentDto?> GetPaymentByIdAsync(
        Guid paymentId,
        CancellationToken ct = default);

    Task<PaymentDto?> GetPaymentByAppIdAsync(
        string appPaymentId,
        CancellationToken ct = default);

    Task<bool> HandleAdviceNotificationAsync(
        PaymentAdvicePayload advice,
        string rawPayload,
        string? authHeader,
        CancellationToken ct = default);

    Task<bool> HandleSwitchWebhookAsync(
        SwitchWebhookPayload webhook,
        string rawPayload,
        string? signatureHeader,
        CancellationToken ct = default);

    Task<DailySettlementSummaryDto> GetDailySettlementSummaryAsync(
        DateOnly date,
        CancellationToken ct = default);
}
