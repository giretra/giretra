using Giretra.Core.Players;

namespace Giretra.Web.Models.Responses;

/// <summary>
/// What made a stretch of a deal worth sharing as a clip.
/// </summary>
public enum ClipMomentKind
{
    /// <summary>The team won all eight tricks of the deal.</summary>
    Sweep,
    /// <summary>A player earned an achievement in the deal.</summary>
    Achievement,
    /// <summary>A play that was the only good card: every alternative would have cost match points.</summary>
    BrilliantPlay,
    /// <summary>A trick where the opponents' mistakes handed the team a lot of match points.</summary>
    Swing,
}

/// <summary>
/// A suggested clip: a trick range of one deal and why it is notable, from the point of
/// view of <see cref="Team"/>. Computed by the solver once the match is persisted.
/// </summary>
public sealed class ClipMomentResponse
{
    public required ClipMomentKind Kind { get; init; }
    public required int DealNumber { get; init; }
    public required int FromTrick { get; init; }
    public required int ToTrick { get; init; }

    /// <summary>The team this moment is good news for.</summary>
    public required Team Team { get; init; }

    /// <summary>The player who made the play, earned the achievement or won the trick; null for a whole-team moment.</summary>
    public PlayerPosition? Player { get; init; }

    /// <summary>For <see cref="ClipMomentKind.Achievement"/>: the achievement code.</summary>
    public string? AchievementCode { get; init; }

    /// <summary>Match points involved: the swing of the trick, or what the alternatives would have lost.</summary>
    public int MatchPoints { get; init; }

    /// <summary>Ranking score, higher is more notable. Comparable across kinds.</summary>
    public required int Score { get; init; }
}
