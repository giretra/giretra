using Giretra.Core.Cards;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.State;
using Giretra.Web.Players;

namespace Giretra.Web.Achievements;

/// <summary>
/// All the data an achievement rule needs to decide if it was earned.
/// Built by AchievementEvaluator before running rules.
/// </summary>
public sealed record AchievementContext
{
    /// <summary>
    /// Whether this evaluation is for a deal end or match end.
    /// </summary>
    public required AchievementTrigger Trigger { get; init; }

    /// <summary>
    /// The result of the deal that just ended (null when Trigger is MatchEnd only).
    /// </summary>
    public DealResult? DealResult { get; init; }

    /// <summary>
    /// The 1-indexed deal number (null when Trigger is MatchEnd only).
    /// </summary>
    public short? DealNumber { get; init; }

    /// <summary>
    /// Full match state at the time of evaluation.
    /// </summary>
    public required MatchState MatchState { get; init; }

    /// <summary>
    /// All deals completed so far in this match (including the current one if DealEnd).
    /// </summary>
    public required IReadOnlyList<DealResult> CompletedDeals { get; init; }

    /// <summary>
    /// The position of the player being evaluated.
    /// </summary>
    public required PlayerPosition PlayerPosition { get; init; }

    /// <summary>
    /// The team of the player being evaluated.
    /// </summary>
    public required Team PlayerTeam { get; init; }

    /// <summary>
    /// Database Player ID.
    /// </summary>
    public required Guid PlayerId { get; init; }

    /// <summary>
    /// The 5-card hand this player had during negotiation (current deal).
    /// </summary>
    public IReadOnlyList<Card> InitialHand { get; init; } = [];

    /// <summary>
    /// The 8-card hand this player had at the start of play (current deal).
    /// </summary>
    public IReadOnlyList<Card> FullHand { get; init; } = [];

    /// <summary>
    /// All negotiation actions in the current deal.
    /// </summary>
    public IReadOnlyList<RecordedAction> NegotiationActions { get; init; } = [];

    /// <summary>
    /// Achievement codes this player already has. Used to skip already-earned achievements.
    /// </summary>
    public required IReadOnlySet<string> AlreadyEarnedCodes { get; init; }
}
