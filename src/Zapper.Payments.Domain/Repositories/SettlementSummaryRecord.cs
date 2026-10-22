namespace Zapper.Payments.Domain.Repositories;

public record SettlementSummaryRecord(
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
