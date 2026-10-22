using Zapper.Payments.Domain.Enums;

namespace Zapper.Payments.Application.DTOs;

public record SubmitPaymentRequest(
    string AppPaymentId,
    string CustomerId,
    string SubMerchantId,
    long AmountInMinorUnits,
    string Currency = "ZAR"
);

public record SubmitPaymentResult(
    Guid PaymentId,
    string AppPaymentId,
    string OrderNumber,
    string RetrievalReferenceNumber,
    PaymentStatus Status,
    string? ResponseCode,
    string? AuthCode,
    long? AuthAmount,
    string? ClientMessage,
    bool IsDuplicateSubmission = false
);

public record PaymentDto(
    Guid PaymentId,
    string AppPaymentId,
    string CustomerId,
    string SubMerchantId,
    long Amount,
    string Currency,
    string OrderNumber,
    string RetrievalReferenceNumber,
    string? TransactionId,
    PaymentStatus Status,
    string? AuthCode,
    long? AuthAmount,
    long RefundedAmount,
    string? LastResponseCode,
    string? FailureReason,
    string? ClientMessage,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    List<PaymentHistoryDto> History
);

public record PaymentHistoryDto(
    PaymentStatus FromStatus,
    PaymentStatus ToStatus,
    string? Reason,
    string Source,
    DateTime TimestampUtc
);

public record DailySettlementSummaryDto(
    DateOnly Date,
    List<SubMerchantSettlementSummaryDto> SubMerchants
);

public record SubMerchantSettlementSummaryDto(
    string SubMerchantId,
    string TradingName,
    string Currency,
    int ApprovedCount,
    long ApprovedTotalValue,
    int DeclinedCount,
    long DeclinedTotalValue,
    int ReversalCount,
    long ReversalTotalValue,
    int RefundCount,
    long RefundTotalValue
);
