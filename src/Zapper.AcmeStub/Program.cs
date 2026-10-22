using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

// In-memory state storage for the stub
builder.Services.AddSingleton<AcmeStateStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// -------------------------------------------------------------
// POST /api/payment
// Submits a payment for processing (Appendix A, Page 6)
// -------------------------------------------------------------
app.MapPost("/api/payment", async (
    HttpContext context,
    AcmePaymentEnvelope request,
    AcmeStateStore store,
    IConfiguration config) =>
{
    // 1. Authentication check
    var authHeader = context.Request.Headers.Authorization.ToString();
    if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer sk_test_"))
    {
        return Results.Json(new AcmeResponseEnvelope(
            MessageHeader: CreateResponseHeader(),
            PaymentResponse: new AcmePaymentResponsePayload(
                TransactionID: request?.PaymentRequest?.TransactionID ?? Guid.NewGuid().ToString("D"),
                TransactionDateTime: GetSastTimestamp(),
                OrderNumber: request?.PaymentRequest?.OrderNumber ?? "",
                ReconID: null,
                RetrievalReferenceNumber: null,
                AuthCode: null,
                TransactionStatus: "Failure",
                ResponseDetail: new AcmeResponseDetail("AcmePay", "E02", "The API key is missing or not valid.", "Invalid API key."),
                AuthAmount: null
            )
        ), statusCode: StatusCodes.Status401Unauthorized);
    }

    // 2. Body validation
    if (request?.PaymentRequest == null ||
        string.IsNullOrWhiteSpace(request.PaymentRequest.MerchantID) ||
        string.IsNullOrWhiteSpace(request.PaymentRequest.TransactionID) ||
        string.IsNullOrWhiteSpace(request.PaymentRequest.OrderNumber) ||
        request.PaymentRequest.Card == null ||
        string.IsNullOrWhiteSpace(request.PaymentRequest.Card.Token))
    {
        return Results.Json(new AcmeResponseEnvelope(
            MessageHeader: CreateResponseHeader(),
            PaymentResponse: new AcmePaymentResponsePayload(
                TransactionID: request?.PaymentRequest?.TransactionID ?? Guid.NewGuid().ToString("D"),
                TransactionDateTime: GetSastTimestamp(),
                OrderNumber: request?.PaymentRequest?.OrderNumber ?? "",
                ReconID: null,
                RetrievalReferenceNumber: null,
                AuthCode: null,
                TransactionStatus: "Failure",
                ResponseDetail: new AcmeResponseDetail("AcmePay", "E01", "The body could not be parsed, or a required field is missing.", "Missing required fields."),
                AuthAmount: null
            )
        ), statusCode: StatusCodes.Status400BadRequest);
    }

    var rrn = request.PaymentRequest.OrderNumber.Length >= 12
        ? request.PaymentRequest.OrderNumber.Substring(0, 12)
        : request.PaymentRequest.OrderNumber;

    // 3. Duplicate TransactionID replay check (24 hour idempotent replay)
    if (store.TryGetByTransactionId(request.PaymentRequest.TransactionID, out var existingRecord))
    {
        return Results.Ok(new AcmeResponseEnvelope(
            MessageHeader: CreateResponseHeader(),
            PaymentResponse: existingRecord!
        ));
    }

    // 4. Duplicate OrderNumber / RRN conflict check (409 E09)
    if (store.TryGetByRrn(rrn, out var conflictingRecord))
    {
        return Results.Json(new AcmeResponseEnvelope(
            MessageHeader: CreateResponseHeader(),
            PaymentResponse: new AcmePaymentResponsePayload(
                TransactionID: conflictingRecord!.TransactionID,
                TransactionDateTime: GetSastTimestamp(),
                OrderNumber: request.PaymentRequest.OrderNumber,
                ReconID: null,
                RetrievalReferenceNumber: rrn,
                AuthCode: null,
                TransactionStatus: "Failure",
                ResponseDetail: new AcmeResponseDetail("Application", "E09", "Retrieval Reference Number already in use", "We could not process that payment."),
                AuthAmount: null
            )
        ), statusCode: StatusCodes.Status409Conflict);
    }

    // 5. Simulated Timeout check (Triggered if amount == 99999 or header "X-Simulate-Timeout" is true)
    if (context.Request.Headers.ContainsKey("X-Simulate-Timeout") || request.PaymentRequest.Amount == 99999)
    {
        // Store as processing so RTTQ can find it
        var processingRecord = new AcmePaymentResponsePayload(
            TransactionID: request.PaymentRequest.TransactionID,
            TransactionDateTime: GetSastTimestamp(),
            OrderNumber: request.PaymentRequest.OrderNumber,
            ReconID: $"MR{Random.Shared.Next(100000000, 999999999)}",
            RetrievalReferenceNumber: rrn,
            AuthCode: "238699",
            TransactionStatus: "Success",
            ResponseDetail: new AcmeResponseDetail("Acquirer", "00", "Approved or completed successfully", "Approved."),
            AuthAmount: request.PaymentRequest.Amount
        );
        store.Save(request.PaymentRequest.TransactionID, rrn, processingRecord);

        // Delay longer than client timeout (e.g. 5 seconds for fast test or 35 seconds)
        var delayMs = context.Request.Headers.TryGetValue("X-Timeout-Delay-Ms", out var customDelay) && int.TryParse(customDelay, out var parsed)
            ? parsed 
            : 35000;
        await Task.Delay(delayMs, context.RequestAborted);
    }

    // 6. Outcome simulation based on Card Token or Amount
    // Tokens starting with "tok_decline" or amounts ending in "51" will simulate declines
    bool isDeclined = request.PaymentRequest.Card.Token.Contains("decline", StringComparison.OrdinalIgnoreCase) ||
                      request.PaymentRequest.Amount == 5100;

    AcmePaymentResponsePayload responsePayload;
    if (isDeclined)
    {
        responsePayload = new AcmePaymentResponsePayload(
            TransactionID: request.PaymentRequest.TransactionID,
            TransactionDateTime: GetSastTimestamp(),
            OrderNumber: request.PaymentRequest.OrderNumber,
            ReconID: null,
            RetrievalReferenceNumber: rrn,
            AuthCode: null,
            TransactionStatus: "Failure",
            ResponseDetail: new AcmeResponseDetail("Acquirer", "51", "Insufficient funds", "There was not enough money in the account."),
            AuthAmount: null
        );
    }
    else
    {
        responsePayload = new AcmePaymentResponsePayload(
            TransactionID: request.PaymentRequest.TransactionID,
            TransactionDateTime: GetSastTimestamp(),
            OrderNumber: request.PaymentRequest.OrderNumber,
            ReconID: $"MR{Random.Shared.Next(100000000, 999999999)}",
            RetrievalReferenceNumber: rrn,
            AuthCode: Random.Shared.Next(100000, 999999).ToString(),
            TransactionStatus: "Success",
            ResponseDetail: new AcmeResponseDetail("Acquirer", "00", "Approved or completed successfully", "Approved."),
            AuthAmount: request.PaymentRequest.Amount
        );
    }

    store.Save(request.PaymentRequest.TransactionID, rrn, responsePayload);

    return Results.Ok(new AcmeResponseEnvelope(
        MessageHeader: CreateResponseHeader(),
        PaymentResponse: responsePayload
    ));
});

// -------------------------------------------------------------
// GET /apirttq/query/Merchant/{merchantId}
// Real Time Transaction Query (Appendix A, Page 9)
// -------------------------------------------------------------
app.MapGet("/apirttq/query/Merchant/{merchantId}", (
    string merchantId,
    string? transactionId,
    string? orderNumber,
    HttpContext context,
    AcmeStateStore store) =>
{
    var authHeader = context.Request.Headers.Authorization.ToString();
    if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer sk_test_"))
    {
        return Results.Unauthorized();
    }

    AcmePaymentResponsePayload? payload = null;
    if (!string.IsNullOrWhiteSpace(transactionId) && store.TryGetByTransactionId(transactionId, out var byTx))
    {
        payload = byTx;
    }
    else if (!string.IsNullOrWhiteSpace(orderNumber))
    {
        var rrn = orderNumber.Length >= 12 ? orderNumber.Substring(0, 12) : orderNumber;
        if (store.TryGetByRrn(rrn, out var byRrn))
        {
            payload = byRrn;
        }
    }

    if (payload == null)
    {
        return Results.NotFound(new { Message = "No transaction found matching identifier." });
    }

    return Results.Ok(new AcmeResponseEnvelope(
        MessageHeader: CreateResponseHeader(),
        PaymentResponse: payload
    ));
});

// -------------------------------------------------------------
// POST /api/test/trigger-advice
// Test helper to dispatch Payment Advice Notification to Zapper
// -------------------------------------------------------------
app.MapPost("/api/test/trigger-advice", async (
    TriggerAdviceRequest req,
    IHttpClientFactory httpFactory,
    IConfiguration config) =>
{
    var client = httpFactory.CreateClient();
    var targetUrl = req.TargetUrl ?? config["Zapper:AdviceUrl"] ?? "http://localhost:5000/api/v1/notifications/payment-advice";

    var advicePayload = new
    {
        RequestType = "Payment",
        MerchantID = "8B67BF88-BB8D-4EB5-ADFC-0F5C97C7ED67",
        TransactionID = req.TransactionID,
        TransactionDateTime = GetSastTimestamp(),
        OrderNumber = req.OrderNumber,
        ReconID = req.ReconID ?? $"MR{Random.Shared.Next(100000000, 999999999)}",
        RetrievalReferenceNumber = req.Rrn,
        SystemsTraceAuditNumber = Random.Shared.Next(1000, 9999).ToString(),
        Amount = req.Amount,
        CurrencyCode = req.CurrencyCode ?? "ZAR",
        PaymentService = "CardNotPresent",
        TransactionStatus = req.ResponseCode == "00" ? "Success" : "Failure",
        ResponseSource = "Acquirer",
        ResponseCode = req.ResponseCode ?? "00",
        ResponseDescription = req.ResponseCode == "00" ? "Approved" : "Declined",
        AuthCode = req.AuthCode ?? "238699",
        MaskedCardNumber = "541333******1429",
        CommProtocol = "REST"
    };

    var json = JsonSerializer.Serialize(advicePayload);
    var httpRequest = new HttpRequestMessage(HttpMethod.Post, targetUrl)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    var username = req.Username ?? config["Acme:AdviceUsername"] ?? "acme_advice_user";
    var password = req.Password ?? config["Acme:AdvicePassword"] ?? "acme_advice_pass";
    var basicCreds = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
    httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Basic {basicCreds}");

    var resp = await client.SendAsync(httpRequest);
    return Results.Ok(new
    {
        StatusCode = (int)resp.StatusCode,
        TargetUrl = targetUrl,
        PayloadSent = advicePayload
    });
});

// -------------------------------------------------------------
// POST /api/test/trigger-switch
// Test helper to dispatch Acme Switch Webhook (Reversal/Refund) to Zapper
// -------------------------------------------------------------
app.MapPost("/api/test/trigger-switch", async (
    TriggerSwitchRequest req,
    IHttpClientFactory httpFactory,
    IConfiguration config) =>
{
    var client = httpFactory.CreateClient();
    var targetUrl = req.TargetUrl ?? config["Zapper:SwitchWebhookUrl"] ?? "http://localhost:5000/api/v1/notifications/transaction-status";

    var webhookPayload = new
    {
        transactionUUID = req.TransactionUUID ?? Guid.NewGuid().ToString("D"),
        transactionType = req.TransactionType, // "Reversal" or "Refund"
        transactionDateTime = GetSastTimestamp(),
        rrn = req.Rrn,
        transactionAmount = req.Amount,
        currencyCode = req.CurrencyCode ?? "ZAR",
        txResultCode = "00",
        isApproved = true
    };

    var json = JsonSerializer.Serialize(webhookPayload);
    var secret = req.WebhookSecret ?? config["Acme:WebhookSecret"] ?? "k7Qf2mZr9XpL4vNcA8sTgH3wYbE6dJ5u";

    // Compute HMAC-SHA256 signature
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(json));
    var signature = Convert.ToBase64String(hashBytes);

    var httpRequest = new HttpRequestMessage(HttpMethod.Post, targetUrl)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
    httpRequest.Headers.Add("x-signature", signature);

    var resp = await client.SendAsync(httpRequest);
    return Results.Ok(new
    {
        StatusCode = (int)resp.StatusCode,
        Signature = signature,
        TargetUrl = targetUrl,
        PayloadSent = webhookPayload
    });
});

app.Run();

// -------------------------------------------------------------
// Helpers & State Store
// -------------------------------------------------------------
static AcmeMessageHeader CreateResponseHeader() => new(
    MessageDateTime: GetSastTimestamp(),
    MessageID: Guid.NewGuid().ToString("D"),
    Channel: "App"
);

static string GetSastTimestamp() => DateTime.UtcNow.AddHours(2).ToString("yyyy-MM-ddTHH:mm:ss.fff");

public class AcmeStateStore
{
    private readonly Dictionary<string, AcmePaymentResponsePayload> _byTransactionId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AcmePaymentResponsePayload> _byRrn = new(StringComparer.OrdinalIgnoreCase);

    public void Save(string transactionId, string rrn, AcmePaymentResponsePayload payload)
    {
        _byTransactionId[transactionId] = payload;
        _byRrn[rrn] = payload;
    }

    public bool TryGetByTransactionId(string txId, out AcmePaymentResponsePayload? payload) =>
        _byTransactionId.TryGetValue(txId, out payload);

    public bool TryGetByRrn(string rrn, out AcmePaymentResponsePayload? payload) =>
        _byRrn.TryGetValue(rrn, out payload);
}

public record AcmePaymentEnvelope(AcmeMessageHeader MessageHeader, AcmePaymentRequestPayload PaymentRequest);
public record AcmeMessageHeader(string MessageDateTime, string MessageID, string Channel);
public record AcmeCard(string Token);
public record AcmePaymentRequestPayload(
    string MerchantID,
    string TransactionID,
    string OrderNumber,
    long Amount,
    string? CurrencyCode,
    string PaymentService,
    AcmeCard Card,
    string? CardOnFileScenario,
    string SubMerchantID,
    string MerchantCategoryCode,
    string CardAcceptorName,
    string? Email
);
public record AcmeResponseEnvelope(AcmeMessageHeader MessageHeader, AcmePaymentResponsePayload PaymentResponse);
public record AcmePaymentResponsePayload(
    string TransactionID,
    string? TransactionDateTime,
    string OrderNumber,
    string? ReconID,
    string? RetrievalReferenceNumber,
    string? AuthCode,
    string TransactionStatus,
    AcmeResponseDetail? ResponseDetail,
    long? AuthAmount
);
public record AcmeResponseDetail(string Source, string Code, string Description, string ClientMessage);

public record TriggerAdviceRequest(
    string TransactionID,
    string OrderNumber,
    string Rrn,
    long Amount,
    string? CurrencyCode = "ZAR",
    string? ResponseCode = "00",
    string? ReconID = null,
    string? AuthCode = null,
    string? TargetUrl = null,
    string? Username = null,
    string? Password = null
);

public record TriggerSwitchRequest(
    string TransactionType,
    string Rrn,
    long Amount,
    string? TransactionUUID = null,
    string? CurrencyCode = "ZAR",
    string? TargetUrl = null,
    string? WebhookSecret = null
);
