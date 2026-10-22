using Microsoft.EntityFrameworkCore;
using Zapper.Payments.Domain.Aggregates;
using Zapper.Payments.Domain.Entities;
using Zapper.Payments.Domain.ValueObjects;

namespace Zapper.Payments.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentHistoryEntry> PaymentHistory => Set<PaymentHistoryEntry>();
    public DbSet<SubMerchant> SubMerchants => Set<SubMerchant>();
    public DbSet<CustomerCard> CustomerCards => Set<CustomerCard>();
    public DbSet<PaymentAuditLog> AuditLogs => Set<PaymentAuditLog>();
    public DbSet<PaymentNotificationLog> NotificationLogs => Set<PaymentNotificationLog>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Payment Aggregate Mapping
        modelBuilder.Entity<Payment>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.AppPaymentId).IsRequired().HasMaxLength(100);
            b.HasIndex(p => p.AppPaymentId).IsUnique();

            b.Property(p => p.CustomerId).IsRequired().HasMaxLength(100);
            b.Property(p => p.SubMerchantId).IsRequired().HasMaxLength(50);
            b.Property(p => p.CardToken).IsRequired().HasMaxLength(100);
            b.Property(p => p.OrderNumber).IsRequired().HasMaxLength(50);
            b.HasIndex(p => p.OrderNumber).IsUnique();

            b.Property(p => p.RetrievalReferenceNumber).IsRequired().HasMaxLength(12);
            b.HasIndex(p => p.RetrievalReferenceNumber);

            b.Property(p => p.TransactionId).HasMaxLength(100);
            b.HasIndex(p => p.TransactionId);

            b.Property(p => p.Status).IsRequired();
            b.Property(p => p.ReconId).HasMaxLength(50);
            b.Property(p => p.AuthCode).HasMaxLength(10);
            b.Property(p => p.LastResponseCode).HasMaxLength(10);
            b.Property(p => p.FailureReason).HasMaxLength(250);
            b.Property(p => p.ClientMessage).HasMaxLength(250);

            // Money Value Object as Owned Entity
            b.OwnsOne(p => p.Amount, money =>
            {
                money.Property(m => m.AmountInMinorUnits).HasColumnName("AmountInMinorUnits").IsRequired();
                money.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3).IsRequired();
            });

            // Backing field for history
            b.HasMany(p => p.History)
                .WithOne()
                .HasForeignKey(h => h.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Navigation(p => p.History).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // PaymentHistoryEntry Mapping
        modelBuilder.Entity<PaymentHistoryEntry>(b =>
        {
            b.HasKey(h => h.Id);
            b.Property(h => h.FromStatus).IsRequired();
            b.Property(h => h.ToStatus).IsRequired();
            b.Property(h => h.Reason).HasMaxLength(500);
            b.Property(h => h.Source).IsRequired().HasMaxLength(50);
            b.Property(h => h.TimestampUtc).IsRequired();
        });

        // SubMerchant Mapping
        modelBuilder.Entity<SubMerchant>(b =>
        {
            b.HasKey(s => s.SubMerchantId);
            b.Property(s => s.SubMerchantId).HasMaxLength(15);
            b.Property(s => s.TradingName).IsRequired().HasMaxLength(100);
            b.Property(s => s.City).IsRequired().HasMaxLength(50);
            b.Property(s => s.Region).IsRequired().HasMaxLength(10);
            b.Property(s => s.Country).IsRequired().HasMaxLength(10);
            b.Property(s => s.MerchantCategoryCode).IsRequired().HasMaxLength(4);
        });

        // CustomerCard Mapping
        modelBuilder.Entity<CustomerCard>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.CustomerId).IsRequired().HasMaxLength(100);
            b.HasIndex(c => c.CustomerId);
            b.Property(c => c.AcmeToken).IsRequired().HasMaxLength(100);
            b.Property(c => c.Email).IsRequired().HasMaxLength(254);
        });

        // PaymentAuditLog Mapping
        modelBuilder.Entity<PaymentAuditLog>(b =>
        {
            b.HasKey(a => a.Id);
            b.Property(a => a.CorrelationId).IsRequired().HasMaxLength(100);
            b.Property(a => a.Endpoint).IsRequired().HasMaxLength(250);
            b.Property(a => a.HttpMethod).IsRequired().HasMaxLength(10);
        });

        // PaymentNotificationLog Mapping
        modelBuilder.Entity<PaymentNotificationLog>(b =>
        {
            b.HasKey(n => n.Id);
            b.Property(n => n.NotificationType).IsRequired().HasMaxLength(50);
            b.Property(n => n.Identifier).HasMaxLength(100);
        });
    }

    public async Task SeedAsync()
    {
        if (!await SubMerchants.AnyAsync())
        {
            await SubMerchants.AddRangeAsync(
                new SubMerchant("ZAP0004182", "CAFE DEL SOL", "CAPE TOWN", "WC", "ZA", "5812"),
                new SubMerchant("ZAP0004183", "THE GRILL HOUSE", "JOHANNESBURG", "GP", "ZA", "5812"),
                new SubMerchant("ZAP0004184", "DURBAN BEACH DINER", "DURBAN", "KZN", "ZA", "5812")
            );
        }

        if (!await CustomerCards.AnyAsync())
        {
            await CustomerCards.AddRangeAsync(
                new CustomerCard("cust-001", "4f2a8c9e1b7d3560", "alice@example.com"),
                new CustomerCard("cust-002", "tok_decline_funds", "bob@example.com"),
                new CustomerCard("cust-003", "tok_timeout_test", "charlie@example.com")
            );
        }

        await SaveChangesAsync();
    }
}
