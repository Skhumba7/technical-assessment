using Zapper.Payments.Domain.Enums;

namespace Zapper.Payments.Domain.Entities;

public class PaymentHistoryEntry
{
    public long Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public PaymentStatus FromStatus { get; private set; }
    public PaymentStatus ToStatus { get; private set; }
    public string? Reason { get; private set; }
    public string Source { get; private set; } = string.Empty; // e.g. "AcmeApi", "AdviceNotification", "SwitchWebhook", "SystemTimeout"
    public DateTime TimestampUtc { get; private set; } = DateTime.UtcNow;

    protected PaymentHistoryEntry() { }

    public PaymentHistoryEntry(
        Guid paymentId,
        PaymentStatus fromStatus,
        PaymentStatus toStatus,
        string? reason,
        string source)
    {
        PaymentId = paymentId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Reason = reason;
        Source = source;
        TimestampUtc = DateTime.UtcNow;
    }
}
