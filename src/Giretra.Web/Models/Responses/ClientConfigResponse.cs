using Giretra.Web.Models;

namespace Giretra.Web.Models.Responses;

/// <summary>
/// Response DTO for client configuration (mobile version gate).
/// </summary>
public sealed class ClientConfigResponse
{
    /// <summary>
    /// Oldest mobile app version the API still supports (semver).
    /// </summary>
    public required string MinSupportedMobileVersion { get; init; }

    /// <summary>
    /// Latest published mobile app version (semver).
    /// </summary>
    public required string LatestMobileVersion { get; init; }

    /// <summary>
    /// Store page URLs, null until the app is published.
    /// </summary>
    public required StoreUrlsResponse StoreUrls { get; init; }

    public static ClientConfigResponse FromOptions(MobileClientOptions options)
    {
        return new ClientConfigResponse
        {
            MinSupportedMobileVersion = options.MinSupportedVersion,
            LatestMobileVersion = options.LatestVersion,
            StoreUrls = new StoreUrlsResponse
            {
                Android = options.StoreUrls.Android,
                Ios = options.StoreUrls.Ios
            }
        };
    }
}

/// <summary>
/// Per-platform store page URLs.
/// </summary>
public sealed class StoreUrlsResponse
{
    public required string? Android { get; init; }
    public required string? Ios { get; init; }
}
