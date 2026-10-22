namespace Zapper.Payments.Domain.Entities;

public class PaymentAuditLog
{
    public long Id { get; private set; }
    public Guid? PaymentId { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string Endpoint { get; private set; } = string.Empty;
    public string HttpMethod { get; private set; } = string.Empty;
    public string? RequestPayload { get; private set; }
    public string? ResponsePayload { get; private set; }
    public int? HttpStatusCode { get; private set; }
    public long DurationMs { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime TimestampUtc { get; private set; } = DateTime.UtcNow;

    protected PaymentAuditLog() { }

    public PaymentAuditLog(
        Guid? paymentId,
        string correlationId,
        string endpoint,
        string httpMethod,
        string? requestPayload,
        string? responsePayload,
        int? httpStatusCode,
        long durationMs,
        string? errorMessage = null)
    {
        PaymentId = paymentId;
        CorrelationId = correlationId;
        Endpoint = endpoint;
        HttpMethod = httpMethod;
        RequestPayload = requestPayload;
        ResponsePayload = responsePayload;
        HttpStatusCode = httpStatusCode;
        DurationMs = durationMs;
        ErrorMessage = errorMessage;
        TimestampUtc = DateTime.UtcNow;
    }
}
