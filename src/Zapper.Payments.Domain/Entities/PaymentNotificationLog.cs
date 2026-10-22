namespace Zapper.Payments.Domain.Entities;

public class PaymentNotificationLog
{
    public long Id { get; private set; }
    public string NotificationType { get; private set; } = string.Empty; // "PaymentAdvice" or "SwitchWebhook"
    public string? Identifier { get; private set; } // TransactionID, RRN, or transactionUUID
    public Guid? MatchedPaymentId { get; private set; }
    public string RawPayload { get; private set; } = string.Empty;
    public string? AuthOrSignatureHeader { get; private set; }
    public bool ProcessedSuccessfully { get; private set; }
    public string? ProcessingNotes { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; } = DateTime.UtcNow;

    protected PaymentNotificationLog() { }

    public PaymentNotificationLog(
        string notificationType,
        string? identifier,
        string rawPayload,
        string? authOrSignatureHeader)
    {
        NotificationType = notificationType;
        Identifier = identifier;
        RawPayload = rawPayload;
        AuthOrSignatureHeader = authOrSignatureHeader;
        ReceivedAtUtc = DateTime.UtcNow;
    }

    public void MarkProcessed(Guid? paymentId, string? notes = null)
    {
        MatchedPaymentId = paymentId;
        ProcessedSuccessfully = true;
        ProcessingNotes = notes ?? "Processed successfully";
    }

    public void MarkFailed(string error)
    {
        ProcessedSuccessfully = false;
        ProcessingNotes = error;
    }
}
