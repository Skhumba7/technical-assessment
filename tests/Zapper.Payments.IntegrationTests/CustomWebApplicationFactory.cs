using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zapper.Payments.Application.Interfaces;
using Zapper.Payments.Infrastructure.Persistence;

namespace Zapper.Payments.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;
    public IAcmePayClient? MockAcmeClient { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing AppDbContext registration
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Create open in-memory SQLite connection
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // Replace IAcmePayClient if a mock/stub is supplied
            if (MockAcmeClient != null)
            {
                var acmeDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAcmePayClient));
                if (acmeDescriptor != null)
                {
                    services.Remove(acmeDescriptor);
                }
                services.AddSingleton(MockAcmeClient);
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection?.Dispose();
    }
}
