using Zapper.Payments.Domain.Common;
using Zapper.Payments.Domain.ValueObjects;

namespace Zapper.Payments.Domain.Entities;

public class SubMerchant
{
    public string SubMerchantId { get; private set; } = string.Empty;
    public string TradingName { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string Region { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;
    public string MerchantCategoryCode { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    // EF Core constructor
    protected SubMerchant() { }

    public SubMerchant(
        string subMerchantId,
        string tradingName,
        string city,
        string region,
        string country,
        string merchantCategoryCode)
    {
        if (string.IsNullOrWhiteSpace(subMerchantId))
            throw new DomainException("SubMerchantId cannot be empty.", "INVALID_SUB_MERCHANT_ID");
        if (string.IsNullOrWhiteSpace(tradingName))
            throw new DomainException("Trading name cannot be empty.", "INVALID_TRADING_NAME");
        if (string.IsNullOrWhiteSpace(city))
            throw new DomainException("City cannot be empty.", "INVALID_CITY");
        if (string.IsNullOrWhiteSpace(region))
            throw new DomainException("Region cannot be empty.", "INVALID_REGION");
        if (string.IsNullOrWhiteSpace(country))
            throw new DomainException("Country cannot be empty.", "INVALID_COUNTRY");
        if (string.IsNullOrWhiteSpace(merchantCategoryCode) || merchantCategoryCode.Length != 4)
            throw new DomainException("Merchant Category Code must be 4 digits.", "INVALID_MCC");

        SubMerchantId = subMerchantId.Trim();
        TradingName = tradingName.Trim();
        City = city.Trim();
        Region = region.Trim().ToUpperInvariant();
        Country = country.Trim().ToUpperInvariant();
        MerchantCategoryCode = merchantCategoryCode.Trim();
    }

    public CardAcceptorName GetCardAcceptorName(string facilitatorName = "ZAP")
    {
        return CardAcceptorName.ForFnb(TradingName, City, Region, Country, facilitatorName);
    }
}
