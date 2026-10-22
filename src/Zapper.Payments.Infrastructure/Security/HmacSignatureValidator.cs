using System.Security.Cryptography;
using System.Text;

namespace Zapper.Payments.Infrastructure.Security;

public static class HmacSignatureValidator
{
    /// <summary>
    /// Validates HMAC-SHA256 signature header against raw body bytes using the webhook secret.
    /// Formula per Appendix A: openssl dgst -sha256 -hmac "$ACME_WEBHOOK_SECRET" -binary | base64
    /// </summary>
    public static bool ValidateSignature(byte[] rawBodyBytes, string? signatureHeader, string secret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(secret) || rawBodyBytes == null)
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computedHash = hmac.ComputeHash(rawBodyBytes);
        var computedSignature = Convert.ToBase64String(computedHash);

        // Constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature),
            Encoding.UTF8.GetBytes(signatureHeader.Trim())
        );
    }
}
