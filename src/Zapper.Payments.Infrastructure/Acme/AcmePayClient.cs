using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Zapper.Payments.Application.DTOs;
using Zapper.Payments.Application.Interfaces;
using Zapper.Payments.Domain.Entities;
using Zapper.Payments.Domain.Repositories;

namespace Zapper.Payments.Infrastructure.Acme;

public class AcmePayClient : IAcmePayClient
{
    private readonly HttpClient _httpClient;
    private readonly IAuditRepository _auditRepository;
    private readonly ILogger<AcmePayClient> _logger;
    private readonly string _apiKey;
    private readonly JsonSerializerOptions _jsonOptions;

    public AcmePayClient(
        HttpClient httpClient,
        IAuditRepository auditRepository,
        IConfiguration config,
        ILogger<AcmePayClient> logger)
    {
        _httpClient = httpClient;
        _auditRepository = auditRepository;
        _logger = logger;
        _apiKey = config["Acme:ApiKey"] ?? "sk_test_demo_key_987654321";

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<AcmeResponseEnvelope> SubmitPaymentAsync(
        AcmePaymentEnvelope request,
        string correlationId,
        CancellationToken ct = default)
    {
        var endpoint = "/api/payment";
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        string? responseBody = null;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        httpRequest.Headers.Add("X-Correlation-ID", correlationId);

        try
        {
            response = await _httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
            responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            stopwatch.Stop();

            await _auditRepository.LogAuditAsync(new PaymentAuditLog(
                paymentId: null,
                correlationId: correlationId,
                endpoint: endpoint,
                httpMethod: "POST",
                requestPayload: json,
                responsePayload: responseBody,
                httpStatusCode: (int)response.StatusCode,
                durationMs: stopwatch.ElapsedMilliseconds
            ), ct);

            if (!string.IsNullOrWhiteSpace(responseBody))
            {
                var envelope = JsonSerializer.Deserialize<AcmeResponseEnvelope>(responseBody, _jsonOptions);
                if (envelope != null)
                {
                    return envelope;
                }
            }

            // Fallback envelope if body could not be parsed
            return CreateFallbackEnvelope(request.PaymentRequest.TransactionID, request.PaymentRequest.OrderNumber, (int)response.StatusCode, responseBody);
        }
        catch (TaskCanceledException tce) when (!ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            await _auditRepository.LogAuditAsync(new PaymentAuditLog(
                paymentId: null,
                correlationId: correlationId,
                endpoint: endpoint,
                httpMethod: "POST",
                requestPayload: json,
                responsePayload: null,
                httpStatusCode: 408,
                durationMs: stopwatch.ElapsedMilliseconds,
                errorMessage: "HTTP request timed out waiting for Acme Pay response."
            ), CancellationToken.None).ConfigureAwait(false);

            throw new TimeoutException("HTTP client timed out calling Acme Pay.", tce);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await _auditRepository.LogAuditAsync(new PaymentAuditLog(
                paymentId: null,
                correlationId: correlationId,
                endpoint: endpoint,
                httpMethod: "POST",
                requestPayload: json,
                responsePayload: null,
                httpStatusCode: 500,
                durationMs: stopwatch.ElapsedMilliseconds,
                errorMessage: ex.Message
            ), CancellationToken.None).ConfigureAwait(false);

            throw;
        }
    }

    public async Task<AcmeResponseEnvelope?> QueryRealTimeTransactionAsync(
        string merchantId,
        string? transactionId,
        string? orderNumber,
        string correlationId,
        CancellationToken ct = default)
    {
        var queryString = !string.IsNullOrWhiteSpace(transactionId)
            ? $"TransactionID={Uri.EscapeDataString(transactionId)}"
            : $"OrderNumber={Uri.EscapeDataString(orderNumber ?? "")}";

        var endpoint = $"/apirttq/query/Merchant/{merchantId}?{queryString}";
        var stopwatch = Stopwatch.StartNew();

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        httpRequest.Headers.Add("X-Correlation-ID", correlationId);

        try
        {
            var response = await _httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            stopwatch.Stop();

            await _auditRepository.LogAuditAsync(new PaymentAuditLog(
                paymentId: null,
                correlationId: correlationId,
                endpoint: endpoint,
                httpMethod: "GET",
                requestPayload: null,
                responsePayload: responseBody,
                httpStatusCode: (int)response.StatusCode,
                durationMs: stopwatch.ElapsedMilliseconds
            ), ct).ConfigureAwait(false);

            if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(responseBody))
            {
                return JsonSerializer.Deserialize<AcmeResponseEnvelope>(responseBody, _jsonOptions);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying RTTQ for Merchant {MerchantId}", merchantId);
            return null;
        }
    }

    private static AcmeResponseEnvelope CreateFallbackEnvelope(string txId, string orderNumber, int statusCode, string? body)
    {
        return new AcmeResponseEnvelope(
            MessageHeader: new AcmeMessageHeader(
                MessageDateTime: DateTime.UtcNow.AddHours(2).ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                MessageID: Guid.NewGuid().ToString("D"),
                Channel: "App"
            ),
            PaymentResponse: new AcmePaymentResponsePayload(
                TransactionID: txId,
                TransactionDateTime: DateTime.UtcNow.AddHours(2).ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                OrderNumber: orderNumber,
                ReconID: null,
                RetrievalReferenceNumber: orderNumber.Length >= 12 ? orderNumber.Substring(0, 12) : orderNumber,
                AuthCode: null,
                TransactionStatus: statusCode >= 200 && statusCode < 300 ? "Success" : "Failure",
                ResponseDetail: new AcmeResponseDetail(
                    Source: "AcmeGateway",
                    Code: statusCode == 409 ? "E09" : statusCode == 401 ? "E02" : "E50",
                    Description: body ?? "Gateway response",
                    ClientMessage: "We could not process that payment."
                ),
                AuthAmount: null
            )
        );
    }
}
