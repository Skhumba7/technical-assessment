using Zapper.Payments.Domain.Common;

namespace Zapper.Payments.Domain.Entities;

public class CustomerCard
{
    public Guid Id { get; private set; }
    public string CustomerId { get; private set; } = string.Empty;
    public string AcmeToken { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    // EF Core constructor
    protected CustomerCard() { }

    public CustomerCard(string customerId, string acmeToken, string email)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            throw new DomainException("CustomerId cannot be empty.", "INVALID_CUSTOMER_ID");
        if (string.IsNullOrWhiteSpace(acmeToken))
            throw new DomainException("Acme card token cannot be empty.", "INVALID_CARD_TOKEN");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new DomainException("A valid customer email is required.", "INVALID_EMAIL");

        Id = Guid.NewGuid();
        CustomerId = customerId.Trim();
        AcmeToken = acmeToken.Trim();
        Email = email.Trim().ToLowerInvariant();
    }
}
