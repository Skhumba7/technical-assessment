using Zapper.Payments.Domain.Entities;

namespace Zapper.Payments.Domain.Repositories;

public interface IAuditRepository
{
    Task LogAuditAsync(PaymentAuditLog auditLog, CancellationToken ct = default);
    Task LogNotificationAsync(PaymentNotificationLog notificationLog, CancellationToken ct = default);
}
