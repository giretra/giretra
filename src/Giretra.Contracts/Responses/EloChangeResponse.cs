namespace Giretra.Web.Models.Responses;

public sealed class EloChangeResponse
{
    public required int EloBefore { get; init; }
    public required int EloAfter { get; init; }
    public required int EloChange { get; init; }
}
