namespace Giretra.Web.Services.Clips;

public sealed class ClipOptions
{
    public const string Section = "Clips";

    /// <summary>Directory holding rendered posters and videos (a cache, see the plan).</summary>
    public string RootPath { get; set; } = "data/clips";

    /// <summary>
    /// Absolute origin used in public URLs (Open Graph tags need absolute URLs), e.g.
    /// https://play.giretra.com. When null the request's scheme and host are used.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>New clips a player may create per rolling 24 hours.</summary>
    public int MaxPerDay { get; set; } = 10;
}
