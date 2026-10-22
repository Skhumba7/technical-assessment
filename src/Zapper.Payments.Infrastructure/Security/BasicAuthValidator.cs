using System.Net.Http.Headers;
using System.Text;

namespace Zapper.Payments.Infrastructure.Security;

public static class BasicAuthValidator
{
    public static bool Validate(string? authorizationHeader, string expectedUsername, string expectedPassword)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
            return false;

        if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out var authHeader) ||
            !authHeader.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(authHeader.Parameter))
        {
            return false;
        }

        try
        {
            var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
            var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);
            if (credentials.Length != 2)
                return false;

            var usernameBytes = Encoding.UTF8.GetBytes(credentials[0]);
            var expectedUsernameBytes = Encoding.UTF8.GetBytes(expectedUsername);
            var passwordBytes = Encoding.UTF8.GetBytes(credentials[1]);
            var expectedPasswordBytes = Encoding.UTF8.GetBytes(expectedPassword);

            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(usernameBytes, expectedUsernameBytes) &&
                   System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(passwordBytes, expectedPasswordBytes);
        }
        catch
        {
            return false;
        }
    }
}
