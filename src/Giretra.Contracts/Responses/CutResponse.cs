namespace Giretra.Web.Models.Responses;

/// <summary>
/// Response DTO for a submitted cut.
/// </summary>
public sealed class CutResponse
{
    /// <summary>
    /// The final cut position after the server's random -1/0/+1 nudge.
    /// </summary>
    public required int Position { get; init; }
}
