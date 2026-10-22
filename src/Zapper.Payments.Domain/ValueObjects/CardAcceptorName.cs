using Zapper.Payments.Domain.Common;

namespace Zapper.Payments.Domain.ValueObjects;

/// <summary>
/// Positional 40-character CardAcceptorName for payment facilitators.
/// Specification:
/// Positions 1–23: Merchant name (on FNB/ABSA/Nedbank: FacilitatorName*SubMerchantName, asterisk at 4, 8, or 13).
/// Positions 24–36: City, up to 13 characters.
/// Positions 37–38: First two characters of region code.
/// Positions 39–40: Country code.
/// Total field length must be exactly 40 characters, space-padded.
/// </summary>
public sealed record CardAcceptorName
{
    public string FormattedValue { get; }

    public CardAcceptorName(string formattedValue)
    {
        if (formattedValue is null || formattedValue.Length != 40)
        {
            throw new DomainException($"CardAcceptorName must be exactly 40 characters long. Received length: {formattedValue?.Length ?? 0}", "INVALID_CARD_ACCEPTOR_NAME");
        }

        FormattedValue = formattedValue;
    }

    public static CardAcceptorName ForFnb(
        string subMerchantTradingName,
        string city,
        string region,
        string country,
        string facilitatorName = "ZAP")
    {
        if (string.IsNullOrWhiteSpace(subMerchantTradingName))
        {
            throw new DomainException("Sub-merchant trading name is required.", "INVALID_MERCHANT_NAME");
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            throw new DomainException("City is required.", "INVALID_CITY");
        }

        if (string.IsNullOrWhiteSpace(region) || region.Length < 2)
        {
            throw new DomainException("Region code must be at least 2 characters.", "INVALID_REGION");
        }

        if (string.IsNullOrWhiteSpace(country) || country.Length < 2)
        {
            throw new DomainException("Country code must be at least 2 characters.", "INVALID_COUNTRY");
        }

        // On FNB, positions 1-23 take FacilitatorName*SubMerchantName, asterisk at pos 4, 8, or 13 (1-indexed).
        string cleanFacilitator = facilitatorName.Trim().ToUpperInvariant();
        if (cleanFacilitator != "ZAP" && cleanFacilitator != "ZAPPER")
        {
            cleanFacilitator = "ZAP"; // default to 3 chars + '*' = asterisk at pos 4
        }

        int asteriskPos = cleanFacilitator.Length + 1; // 4 for "ZAP", 7 for "ZAPPER" -> adjust to 8 if needed
        if (asteriskPos != 4 && asteriskPos != 8 && asteriskPos != 13)
        {
            // Normalize to ZAP (asterisk at pos 4)
            cleanFacilitator = "ZAP";
        }

        string rawMerchantPart = $"{cleanFacilitator}*{subMerchantTradingName.Trim().ToUpperInvariant()}";
        string merchantField = TruncateOrPad(rawMerchantPart, 23);

        string cleanCity = city.Trim().ToUpperInvariant();
        string cityField = TruncateOrPad(cleanCity, 13);

        string cleanRegion = region.Trim().ToUpperInvariant().Substring(0, 2);
        string cleanCountry = country.Trim().ToUpperInvariant().Substring(0, 2);

        string combined = $"{merchantField}{cityField}{cleanRegion}{cleanCountry}";

        return new CardAcceptorName(combined);
    }

    private static string TruncateOrPad(string value, int length)
    {
        if (value.Length > length)
        {
            return value.Substring(0, length);
        }

        return value.PadRight(length, ' ');
    }

    public override string ToString() => FormattedValue;

    public static implicit operator string(CardAcceptorName name) => name.FormattedValue;
}
