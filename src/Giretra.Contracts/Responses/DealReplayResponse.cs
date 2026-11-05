using Giretra.Core.GameModes;
using Giretra.Core.Players;

namespace Giretra.Web.Models.Responses;

/// <summary>
/// Everything needed to replay one deal of a finished match as a pure function of time:
/// the four starting hands, the eight tricks with their winners and running card points,
/// and the match context (players, scores before and after, game mode).
/// </summary>
public sealed class DealReplayResponse
{
    public required Guid MatchId { get; init; }
    public required int DealNumber { get; init; }
    public required int TotalDeals { get; init; }
    public required bool IsRanked { get; init; }
    public required PlayerPosition Dealer { get; init; }
    public required GameMode GameMode { get; init; }
    public required MultiplierState Multiplier { get; init; }
    public Team? AnnouncerTeam { get; init; }

    /// <summary>Match points of each team before this deal was played.</summary>
    public required int Team1ScoreBefore { get; init; }
    public required int Team2ScoreBefore { get; init; }

    /// <summary>Match points each team earned from this deal.</summary>
    public required int Team1MatchPointsEarned { get; init; }
    public required int Team2MatchPointsEarned { get; init; }

    /// <summary>Final card points of the deal (includes the last-trick bonus).</summary>
    public required int Team1CardPoints { get; init; }
    public required int Team2CardPoints { get; init; }
    public required bool WasSweep { get; init; }
    public Team? SweepingTeam { get; init; }
    public required bool IsInstantWin { get; init; }

    public required IReadOnlyList<DealReplayPlayerResponse> Players { get; init; }

    /// <summary>The 8 cards each player held before the first trick, rebuilt from the plays.</summary>
    public required IReadOnlyList<DealReplayHandResponse> Hands { get; init; }

    /// <summary>The 8 tricks in play order.</summary>
    public required IReadOnlyList<DealReplayTrickResponse> Tricks { get; init; }
}

public sealed class DealReplayPlayerResponse
{
    public required PlayerPosition Position { get; init; }
    public required Team Team { get; init; }
    public required string DisplayName { get; init; }
    public required bool IsBot { get; init; }
    public Guid? PlayerId { get; init; }
}

public sealed class DealReplayHandResponse
{
    public required PlayerPosition Position { get; init; }
    public required IReadOnlyList<CardResponse> Cards { get; init; }
}

public sealed class DealReplayTrickResponse
{
    public required int TrickNumber { get; init; }
    public required PlayerPosition Leader { get; init; }
    public required IReadOnlyList<PlayedCardResponse> Plays { get; init; }
    public required PlayerPosition Winner { get; init; }

    /// <summary>Card points of each team once this trick is collected (cumulative).</summary>
    public required int Team1CumulativeCardPoints { get; init; }
    public required int Team2CumulativeCardPoints { get; init; }
}
