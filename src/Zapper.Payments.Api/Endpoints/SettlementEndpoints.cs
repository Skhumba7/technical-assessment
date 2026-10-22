using Microsoft.AspNetCore.Mvc;
using Zapper.Payments.Application.Interfaces;

namespace Zapper.Payments.Api.Endpoints;

public static class SettlementEndpoints
{
    public static RouteGroupBuilder MapSettlementEndpoints(this RouteGroupBuilder group)
    {
        // -------------------------------------------------------------
        // GET /api/v1/settlement/daily-summary
        // Brief Task 2: Daily payment summary for merchant settlement
        // Grouped by sub-merchant and currency:
        // - Number and total value of approved payments
        // - Number and total value of declined payments
        // - Number and total value of reversals
        // - Number and total value of refunds
        // -------------------------------------------------------------
        group.MapGet("/daily-summary", async (
            [FromQuery] string? date,
            IPaymentService paymentService,
            CancellationToken ct) =>
        {
            DateOnly queryDate;
            if (!string.IsNullOrWhiteSpace(date))
            {
                if (!DateOnly.TryParse(date, out queryDate))
                {
                    return Results.BadRequest(new { Message = "Invalid date format. Expected YYYY-MM-DD." });
                }
            }
            else
            {
                queryDate = DateOnly.FromDateTime(DateTime.UtcNow);
            }

            var summary = await paymentService.GetDailySettlementSummaryAsync(queryDate, ct);
            return Results.Ok(summary);
        })
        .WithName("GetDailySettlementSummary")
        .WithSummary("Generates daily merchant settlement summary grouped by sub-merchant and currency");

        return group;
    }
}
