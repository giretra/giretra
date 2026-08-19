namespace Giretra.Web.Models.Requests;

/// <summary>
/// Request to confirm continuation to the next deal.
/// </summary>
public sealed class ContinueDealRequest
{
    /// <summary>
    /// Client ID of the player confirming continuation.
    /// </summary>
    public required string ClientId { get; init; }
}
