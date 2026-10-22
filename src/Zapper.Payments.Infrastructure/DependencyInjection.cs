using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Zapper.Payments.Application.Interfaces;
using Zapper.Payments.Application.Services;
using Zapper.Payments.Domain.Repositories;
using Zapper.Payments.Infrastructure.Acme;
using Zapper.Payments.Infrastructure.Persistence;
using Zapper.Payments.Infrastructure.Persistence.Repositories;

namespace Zapper.Payments.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Dual-Provider Database Configuration
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
                               ?? "Data Source=zapper_payments.db";

        services.AddDbContext<AppDbContext>(options =>
        {
            if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
                connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
            {
                options.UseNpgsql(connectionString);
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        });

        // 2. Domain Repositories & Unit of Work
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<ISubMerchantRepository, SubMerchantRepository>();
        services.AddScoped<ICustomerCardRepository, CustomerCardRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // 3. Application Services
        services.AddScoped<IPaymentService, PaymentService>();

        // 4. Outbound Acme Pay Client with Polly v8 Resilience
        var acmeBaseUrl = configuration["Acme:BaseUrl"] ?? "http://localhost:5001";
        var timeoutSeconds = configuration.GetValue<int>("Acme:TimeoutSeconds", 30);

        services.AddHttpClient<IAcmePayClient, AcmePayClient>(client =>
        {
            client.BaseAddress = new Uri(acmeBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        })
        .AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(timeoutSeconds + 5);
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.FromMilliseconds(500);
            options.Retry.BackoffType = DelayBackoffType.Exponential;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(timeoutSeconds * 2);
        });

        return services;
    }
}
