namespace Giretra.Web.Models;

/// <summary>
/// Mobile client version gate settings, bound from the "MobileClient" configuration section.
/// </summary>
public sealed class MobileClientOptions
{
    public const string SectionName = "MobileClient";

    /// <summary>
    /// Oldest mobile app version the API still supports.
    /// </summary>
    public string MinSupportedVersion { get; set; } = "0.1.0";

    /// <summary>
    /// Latest published mobile app version.
    /// </summary>
    public string LatestVersion { get; set; } = "0.1.0";

    /// <summary>
    /// Store page URLs, once the app is published.
    /// </summary>
    public MobileStoreUrls StoreUrls { get; set; } = new();
}

/// <summary>
/// Per-platform store page URLs.
/// </summary>
public sealed class MobileStoreUrls
{
    public string? Android { get; set; }
    public string? Ios { get; set; }
}
