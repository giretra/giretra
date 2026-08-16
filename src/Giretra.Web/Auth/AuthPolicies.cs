namespace Giretra.Web.Auth;

/// <summary>
/// Authorization policy names. Apply with [Authorize(Policy = AuthPolicies.Moderator)]
/// on admin/management endpoints.
/// </summary>
public static class AuthPolicies
{
    /// <summary>Satisfied by the "moderator" or "admin" Keycloak realm role.</summary>
    public const string Moderator = "Moderator";
}
