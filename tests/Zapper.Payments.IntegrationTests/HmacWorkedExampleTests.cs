using System.Text;
using FluentAssertions;
using Xunit;
using Zapper.Payments.Infrastructure.Security;

namespace Zapper.Payments.IntegrationTests;

public class HmacWorkedExampleTests
{
    [Fact]
    public void ValidateSignature_MatchesAppendixAWorkedExampleExactly()
    {
        // Arrange (from Appendix A, page 10)
        var secret = "k7Qf2mZr9XpL4vNcA8sTgH3wYbE6dJ5u";
        var body = "{\"transactionUUID\":\"7f3a9c12-4b8e-4d21-9f0a-6c5e2d81b4a7\",\"transactionType\":\"Reversal\",\"transactionDateTime\":\"2026-03-04T08:02:11.417\",\"rrn\":\"MR43851259\",\"transactionAmount\":1999,\"currencyCode\":\"ZAR\",\"txResultCode\":\"00\",\"isApproved\":true}";
        var expectedSignature = "KURAJlPrV3BWvWzsGBgr0EzClUSfu3itG62dM5Lqy/Y=";

        var bodyBytes = Encoding.UTF8.GetBytes(body);

        // Act
        var isValid = HmacSignatureValidator.ValidateSignature(bodyBytes, expectedSignature, secret);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateSignature_TamperedBody_ReturnsFalse()
    {
        var secret = "k7Qf2mZr9XpL4vNcA8sTgH3wYbE6dJ5u";
        var tamperedBody = "{\"transactionUUID\":\"7f3a9c12-4b8e-4d21-9f0a-6c5e2d81b4a7\",\"transactionAmount\":9999}";
        var originalSignature = "KURAJlPrV3BWvWzsGBgr0EzClUSfu3itG62dM5Lqy/Y=";

        var bodyBytes = Encoding.UTF8.GetBytes(tamperedBody);

        var isValid = HmacSignatureValidator.ValidateSignature(bodyBytes, originalSignature, secret);

        isValid.Should().BeFalse();
    }
}
