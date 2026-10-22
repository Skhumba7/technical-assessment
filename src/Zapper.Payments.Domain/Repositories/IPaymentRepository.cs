using Zapper.Payments.Domain.Aggregates;

namespace Zapper.Payments.Domain.Repositories;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Payment?> GetByAppPaymentIdAsync(string appPaymentId, CancellationToken ct = default);
    Task<Payment?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default);
    Task<Payment?> GetByRetrievalReferenceNumberAsync(string rrn, CancellationToken ct = default);
    Task<Payment?> GetByTransactionIdAsync(string transactionId, CancellationToken ct = default);
    Task<List<SettlementSummaryRecord>> GetDailySettlementSummaryAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct = default);
    Task AddAsync(Payment payment, CancellationToken ct = default);
    Task UpdateAsync(Payment payment, CancellationToken ct = default);
}
