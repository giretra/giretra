namespace Giretra.Web.Models.Requests;

public sealed class UpdateClipNameVisibilityRequest
{
    /// <summary>Whether the player's display name may appear in publicly shared clips.</summary>
    public required bool Visible { get; init; }
}
