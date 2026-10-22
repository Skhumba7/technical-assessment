using Zapper.Payments.Domain.Common;

namespace Zapper.Payments.Domain.ValueObjects;

public sealed record Money
{
    public long AmountInMinorUnits { get; }
    public string Currency { get; }

    public Money(long amountInMinorUnits, string currency = "ZAR")
    {
        if (amountInMinorUnits <= 0)
        {
            throw new DomainException("Payment amount must be greater than zero.", "INVALID_AMOUNT");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            throw new DomainException("Currency code must be a 3-letter ISO code.", "INVALID_CURRENCY");
        }

        AmountInMinorUnits = amountInMinorUnits;
        Currency = currency.ToUpperInvariant();
    }

    public static Money FromZarCents(long cents) => new(cents, "ZAR");

    public decimal ToDecimal() => AmountInMinorUnits / 100m;

    public override string ToString() => $"{Currency} {ToDecimal():N2}";
}
