using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace MyOnOff.HostAgent;

public static class ControlAuthentication
{
    public static bool IsConfigured(IOptions<AgentOptions> options) =>
        !string.IsNullOrWhiteSpace(options.Value.AuthToken);

    public static bool IsAuthorized(HttpRequest request, IOptions<AgentOptions> options)
    {
        var configuredToken = options.Value.AuthToken;
        if (string.IsNullOrWhiteSpace(configuredToken))
        {
            return false;
        }

        var authorization = request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suppliedToken = authorization[prefix.Length..].Trim();
        var expectedBytes = Encoding.UTF8.GetBytes(configuredToken);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedToken);
        return expectedBytes.Length == suppliedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
}
