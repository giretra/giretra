using Giretra.Core.Players;

namespace Giretra.Web.Models.Requests;

public sealed class CreateClipRequest
{
    public required Guid MatchId { get; init; }
    public required int DealNumber { get; init; }
    /// <summary>First trick of the clip, 1-based inclusive.</summary>
    public required int FromTrick { get; init; }
    /// <summary>Last trick of the clip, 1-based inclusive.</summary>
    public required int ToTrick { get; init; }
    /// <summary>Seat drawn at the bottom; defaults to the creator's seat.</summary>
    public PlayerPosition? ViewPosition { get; init; }
    public bool ShowAllHands { get; init; }
    /// <summary>UI language of the clip: en, fr or mg.</summary>
    public string Locale { get; init; } = "en";
}
