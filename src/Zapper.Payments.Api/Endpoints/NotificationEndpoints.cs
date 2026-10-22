using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Zapper.Payments.Application.DTOs;
using Zapper.Payments.Application.Interfaces;
using Zapper.Payments.Infrastructure.Security;

namespace Zapper.Payments.Api.Endpoints;

public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this RouteGroupBuilder group)
    {
        // -------------------------------------------------------------
        // POST /api/v1/notifications/payment-advice
        // Appendix A: Payment Advice notifications (HTTP Basic Auth)
        // -------------------------------------------------------------
        group.MapPost("/payment-advice", async (
            HttpContext context,
            IPaymentService paymentService,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            var expectedUser = config["Acme:AdviceUsername"] ?? "acme_advice_user";
            var expectedPass = config["Acme:AdvicePassword"] ?? "acme_advice_password_123";

            if (!BasicAuthValidator.Validate(authHeader, expectedUser, expectedPass))
            {
                return Results.Unauthorized();
            }

            const long maxNotificationBytes = 65536; // 64 KB
            if (context.Request.ContentLength > maxNotificationBytes)
            {
                return Results.Problem("Notification payload exceeds maximum allowed size (64 KB).", statusCode: StatusCodes.Status413PayloadTooLarge);
            }

            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync(ct);
            context.Request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return Results.BadRequest(new { Message = "Empty notification body." });
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var advicePayload = JsonSerializer.Deserialize<PaymentAdvicePayload>(rawBody, options);

            if (advicePayload == null)
            {
                return Results.BadRequest(new { Message = "Could not parse PaymentAdvicePayload." });
            }

            await paymentService.HandleAdviceNotificationAsync(advicePayload, rawBody, authHeader, ct);

            // Acme requires HTTP 200 acknowledgment
            return Results.Ok(new { Status = "Acknowledged" });
        })
        .WithName("ReceivePaymentAdvice")
        .WithSummary("Receives Payment Advice notifications from Acme Pay (HTTP Basic Auth protected)");

        // -------------------------------------------------------------
        // POST /api/v1/notifications/transaction-status
        // Appendix A: Acme Switch Webhook (HMAC-SHA256 Base64 x-signature)
        // -------------------------------------------------------------
        group.MapPost("/transaction-status", async (
            HttpContext context,
            IPaymentService paymentService,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var signatureHeader = context.Request.Headers["x-signature"].ToString();
            var secret = config["Acme:WebhookSecret"] ?? "k7Qf2mZr9XpL4vNcA8sTgH3wYbE6dJ5u";

            if (context.Request.ContentLength > 65536)
            {
                return Results.Problem("Webhook payload exceeds maximum allowed size (64 KB).", statusCode: StatusCodes.Status413PayloadTooLarge);
            }

            context.Request.EnableBuffering();
            using var memoryStream = new MemoryStream();
            await context.Request.Body.CopyToAsync(memoryStream, ct);
            var bodyBytes = memoryStream.ToArray();
            var rawBody = Encoding.UTF8.GetString(bodyBytes);
            context.Request.Body.Position = 0;

            // Validate cryptographic HMAC signature
            if (!HmacSignatureValidator.ValidateSignature(bodyBytes, signatureHeader, secret))
            {
                return Results.Unauthorized();
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var switchPayload = JsonSerializer.Deserialize<SwitchWebhookPayload>(rawBody, options);

            if (switchPayload == null)
            {
                return Results.BadRequest(new { Message = "Could not parse SwitchWebhookPayload." });
            }

            await paymentService.HandleSwitchWebhookAsync(switchPayload, rawBody, signatureHeader, ct);

            // Acme Switch requires HTTP 200 acknowledgment
            return Results.Ok(new { Status = "Acknowledged" });
        })
        .WithName("ReceiveSwitchWebhook")
        .WithSummary("Receives Transaction Status webhooks from Acme Switch (HMAC-SHA256 signature protected)");

        return group;
    }
}
