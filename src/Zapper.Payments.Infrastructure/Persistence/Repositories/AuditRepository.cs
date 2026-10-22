using Zapper.Payments.Domain.Entities;
using Zapper.Payments.Domain.Repositories;

namespace Zapper.Payments.Infrastructure.Persistence.Repositories;

public class AuditRepository : IAuditRepository
{
    private readonly AppDbContext _context;

    public AuditRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task LogAuditAsync(PaymentAuditLog auditLog, CancellationToken ct = default)
    {
        await _context.AuditLogs.AddAsync(auditLog, ct);
    }

    public async Task LogNotificationAsync(PaymentNotificationLog notificationLog, CancellationToken ct = default)
    {
        await _context.NotificationLogs.AddAsync(notificationLog, ct);
    }
}
