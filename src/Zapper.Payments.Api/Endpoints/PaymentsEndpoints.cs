using Microsoft.AspNetCore.Mvc;
using Zapper.Payments.Application.DTOs;
using Zapper.Payments.Application.Interfaces;

namespace Zapper.Payments.Api.Endpoints;

public static class PaymentsEndpoints
{
    public static RouteGroupBuilder MapPaymentsEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", async (
            [FromBody] SubmitPaymentRequest request,
            IPaymentService paymentService,
            HttpContext context,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.AppPaymentId) ||
                string.IsNullOrWhiteSpace(request.CustomerId) ||
                string.IsNullOrWhiteSpace(request.SubMerchantId) ||
                request.AmountInMinorUnits <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Payment"] = new[] { "AppPaymentId, CustomerId, SubMerchantId and positive Amount are required." }
                });
            }

            var correlationId = context.Request.Headers["X-Correlation-ID"].ToString();
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = Guid.NewGuid().ToString("D");
            }

            var result = await paymentService.SubmitPaymentAsync(request, correlationId, ct);
            return Results.Ok(result);
        })
        .WithName("SubmitPayment")
        .WithSummary("Submits a payment request initiated by the Zapper app");

        group.MapGet("/{paymentId:guid}", async (
            Guid paymentId,
            IPaymentService paymentService,
            CancellationToken ct) =>
        {
            var payment = await paymentService.GetPaymentByIdAsync(paymentId, ct);
            return payment is not null 
                ? Results.Ok(payment) 
                : Results.NotFound(new { Message = $"Payment '{paymentId}' not found." });
        })
        .WithName("GetPaymentById")
        .WithSummary("Queries the current state of a payment by its internal ID");

        group.MapGet("/by-app-id/{appPaymentId}", async (
            string appPaymentId,
            IPaymentService paymentService,
            CancellationToken ct) =>
        {
            var payment = await paymentService.GetPaymentByAppIdAsync(appPaymentId, ct);
            return payment is not null 
                ? Results.Ok(payment) 
                : Results.NotFound(new { Message = $"Payment with AppPaymentId '{appPaymentId}' not found." });
        })
        .WithName("GetPaymentByAppId")
        .WithSummary("Queries the current state of a payment by the App Payment ID");

        return group;
    }
}
