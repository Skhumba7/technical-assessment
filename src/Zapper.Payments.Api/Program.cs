using Zapper.Payments.Api.Endpoints;
using Zapper.Payments.Api.Middleware;
using Zapper.Payments.Infrastructure;
using Zapper.Payments.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. Add services to container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Zapper Payments Integration API",
        Version = "v1",
        Description = "Zapper Payment Facilitator API integration with Acme Pay PSP"
    });
});

builder.Services.AddHealthChecks();

// 2. Add Infrastructure layer
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// 3. Auto-migrate & seed database for local run
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    await dbContext.SeedAsync();
}

// 4. Configure HTTP Request pipeline
if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Zapper Payments v1"));
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 5. Health check endpoints
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

// 6. Map API route groups
app.MapGroup("/api/v1/payments")
    .MapPaymentsEndpoints()
    .WithTags("Payments");

app.MapGroup("/api/v1/notifications")
    .MapNotificationEndpoints()
    .WithTags("Notifications");

app.MapGroup("/api/v1/settlement")
    .MapSettlementEndpoints()
    .WithTags("Settlement");

app.Run();

// Required for WebApplicationFactory in IntegrationTests
public partial class Program { }
