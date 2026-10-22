using FluentAssertions;
using Xunit;
using Zapper.Payments.Domain.Common;
using Zapper.Payments.Domain.ValueObjects;

namespace Zapper.Payments.Domain.UnitTests;

public class CardAcceptorNameTests
{
    [Fact]
    public void ForFnb_ProducesExact40CharactersMatchingAppendixAExample()
    {
        // Act
        var result = CardAcceptorName.ForFnb(
            subMerchantTradingName: "CAFE DEL SOL",
            city: "CAPE TOWN",
            region: "WC",
            country: "ZA",
            facilitatorName: "ZAP"
        );

        // Assert
        result.FormattedValue.Should().HaveLength(40);
        result.FormattedValue.Should().Be("ZAP*CAFE DEL SOL       CAPE TOWN    WCZA");

        // Verify positional layout
        var merchantSlice = result.FormattedValue.Substring(0, 23); // 1-23
        var citySlice = result.FormattedValue.Substring(23, 13);    // 24-36
        var regionSlice = result.FormattedValue.Substring(36, 2);   // 37-38
        var countrySlice = result.FormattedValue.Substring(38, 2);  // 39-40

        merchantSlice.Should().Be("ZAP*CAFE DEL SOL       ");
        merchantSlice[3].Should().Be('*'); // 1-based position 4 has asterisk
        citySlice.Should().Be("CAPE TOWN    ");
        regionSlice.Should().Be("WC");
        countrySlice.Should().Be("ZA");
    }

    [Fact]
    public void ForFnb_TruncatesLongNamesSafelyToFitPositionalSlices()
    {
        // Act
        var result = CardAcceptorName.ForFnb(
            subMerchantTradingName: "VERY LONG RESTAURANT AND CAFE NAME THAT EXCEEDS LIMIT",
            city: "JOHANNESBURG CENTRAL",
            region: "GAUTENG",
            country: "SOUTH AFRICA",
            facilitatorName: "ZAP"
        );

        // Assert
        result.FormattedValue.Should().HaveLength(40);
        result.FormattedValue.Substring(0, 4).Should().Be("ZAP*");
        result.FormattedValue.Substring(36, 2).Should().Be("GA");
        result.FormattedValue.Substring(38, 2).Should().Be("SO");
    }

    [Fact]
    public void Constructor_InvalidLength_ThrowsDomainException()
    {
        var action = () => new CardAcceptorName("TOO_SHORT");
        action.Should().Throw<DomainException>()
            .WithMessage("*must be exactly 40 characters long*");
    }
}
