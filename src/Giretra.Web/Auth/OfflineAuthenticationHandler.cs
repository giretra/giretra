using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Giretra.Web.Auth;

/// <summary>
/// Authentication handler for offline/dev mode.
/// Reads username from "Authorization: Offline {username}" header,
/// or from the "access_token" query string parameter (for SignalR WebSocket connections).
/// </summary>
public sealed class OfflineAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string OfflinePrefix = "Offline ";
    private const string BearerPrefix = "Bearer ";

    public OfflineAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? username = null;

        // 1. Try Authorization header: "Offline <username>" (from Angular interceptor)
        var authHeader = Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith(OfflinePrefix, StringComparison.OrdinalIgnoreCase))
        {
            username = authHeader[OfflinePrefix.Length..].Trim();
        }

        // 2. SignalR sends token via "Bearer <token>" for HTTP requests (negotiate)
        //    and via access_token query param for WebSocket connections.
        if (string.IsNullOrEmpty(username) && Request.Path.StartsWithSegments("/hubs"))
        {
            if (authHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                username = authHeader[BearerPrefix.Length..].Trim();
            }

            if (string.IsNullOrEmpty(username))
            {
                username = Request.Query["access_token"].ToString();
            }
        }

        if (string.IsNullOrEmpty(username))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Generate deterministic GUID from username
        var userId = GenerateDeterministicGuid(username);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString()),
            new Claim("preferred_username", username),
            new Claim(ClaimTypes.Name, username),
            new Claim("name", username),
            new Claim(ClaimTypes.Email, $"{username}@offline"),
            new Claim("email", $"{username}@offline"),
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    /// Generates a deterministic GUID from a username string using a simple hash.
    /// </summary>
    internal static Guid GenerateDeterministicGuid(string username)
    {
        var bytes = Encoding.UTF8.GetBytes($"giretra-offline:{username.ToLowerInvariant()}");
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        // Use first 16 bytes of SHA-256 as GUID
        return new Guid(hash.AsSpan(0, 16));
    }
}
