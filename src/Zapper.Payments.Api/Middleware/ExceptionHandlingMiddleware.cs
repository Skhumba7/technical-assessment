using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Zapper.Payments.Domain.Common;

namespace Zapper.Payments.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException dex)
        {
            _logger.LogWarning(dex, "Domain invariant violation: {Message}", dex.Message);

            var problem = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc9457",
                Title = "Domain Rule Violation",
                Status = (int)HttpStatusCode.UnprocessableEntity,
                Detail = dex.Message,
                Instance = context.Request.Path
            };
            problem.Extensions["code"] = dex.Code;

            context.Response.StatusCode = (int)HttpStatusCode.UnprocessableEntity;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled server exception processing request {Path}", context.Request.Path);

            var problem = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc9457",
                Title = "An unexpected error occurred",
                Status = (int)HttpStatusCode.InternalServerError,
                Detail = "A server error prevented processing your request.",
                Instance = context.Request.Path
            };
            problem.Extensions["code"] = "INTERNAL_SERVER_ERROR";

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
        }
    }
}
