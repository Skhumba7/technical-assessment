using Microsoft.EntityFrameworkCore;
using Zapper.Payments.Domain.Aggregates;
using Zapper.Payments.Domain.Repositories;

namespace Zapper.Payments.Infrastructure.Persistence.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly AppDbContext _context;

    public PaymentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Payments
            .Include(p => p.History)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<Payment?> GetByAppPaymentIdAsync(string appPaymentId, CancellationToken ct = default)
    {
        return await _context.Payments
            .Include(p => p.History)
            .FirstOrDefaultAsync(p => p.AppPaymentId == appPaymentId, ct);
    }

    public async Task<Payment?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default)
    {
        return await _context.Payments
            .Include(p => p.History)
            .FirstOrDefaultAsync(p => p.OrderNumber == orderNumber, ct);
    }

    public async Task<Payment?> GetByRetrievalReferenceNumberAsync(string rrn, CancellationToken ct = default)
    {
        return await _context.Payments
            .Include(p => p.History)
            .FirstOrDefaultAsync(p => p.RetrievalReferenceNumber == rrn, ct);
    }

    public async Task<Payment?> GetByTransactionIdAsync(string transactionId, CancellationToken ct = default)
    {
        return await _context.Payments
            .Include(p => p.History)
            .FirstOrDefaultAsync(p => p.TransactionId == transactionId, ct);
    }

    public async Task<List<SettlementSummaryRecord>> GetDailySettlementSummaryAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct = default)
    {
        var paymentItems = await _context.Payments
            .AsNoTracking()
            .Where(p => p.CreatedAtUtc >= startUtc && p.CreatedAtUtc <= endUtc)
            .Select(p => new
            {
                p.SubMerchantId,
                Currency = p.Amount.Currency,
                p.Status,
                p.Amount.AmountInMinorUnits,
                p.AuthAmount,
                p.RefundedAmount
            })
            .ToListAsync(ct);

        var subMerchants = await _context.SubMerchants
            .AsNoTracking()
            .ToDictionaryAsync(s => s.SubMerchantId, s => s.TradingName, ct);

        var grouped = paymentItems
            .GroupBy(p => new { p.SubMerchantId, p.Currency })
            .Select(g =>
            {
                var tradingName = subMerchants.TryGetValue(g.Key.SubMerchantId, out var name) ? name : g.Key.SubMerchantId;

                var approved = g.Where(p => p.Status == Domain.Enums.PaymentStatus.Approved).ToList();
                var declined = g.Where(p => p.Status == Domain.Enums.PaymentStatus.Declined).ToList();
                var reversed = g.Where(p => p.Status == Domain.Enums.PaymentStatus.Reversed).ToList();
                var refunded = g.Where(p => p.Status == Domain.Enums.PaymentStatus.Refunded || p.Status == Domain.Enums.PaymentStatus.PartiallyRefunded).ToList();

                return new SettlementSummaryRecord(
                    SubMerchantId: g.Key.SubMerchantId,
                    TradingName: tradingName,
                    Currency: g.Key.Currency,
                    ApprovedCount: approved.Count,
                    ApprovedTotalValue: approved.Sum(p => p.AuthAmount ?? p.AmountInMinorUnits),
                    DeclinedCount: declined.Count,
                    DeclinedTotalValue: declined.Sum(p => p.AmountInMinorUnits),
                    ReversalCount: reversed.Count,
                    ReversalTotalValue: reversed.Sum(p => p.AmountInMinorUnits),
                    RefundCount: refunded.Count,
                    RefundTotalValue: refunded.Sum(p => p.RefundedAmount)
                );
            })
            .ToList();

        return grouped;
    }

    public async Task AddAsync(Payment payment, CancellationToken ct = default)
    {
        await _context.Payments.AddAsync(payment, ct);
    }

    public Task UpdateAsync(Payment payment, CancellationToken ct = default)
    {
        _context.Payments.Update(payment);
        return Task.CompletedTask;
    }
}
