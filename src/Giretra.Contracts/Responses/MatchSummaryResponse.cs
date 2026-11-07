using Giretra.Core.GameModes;
using Giretra.Core.Players;

namespace Giretra.Web.Models.Responses;

/// <summary>
/// The scoreboard of a finished match: every deal with its contract, negotiation, card and
/// match points, running score and trick winners, plus match-level statistics derived from
/// them. Served to participants of the match from the history page.
/// </summary>
public sealed class MatchSummaryResponse
{
    public required Guid MatchId { get; init; }
    public required bool IsRanked { get; init; }
    public required bool WasAbandoned { get; init; }
    public required int TargetScore { get; init; }
    public required int TotalDeals { get; init; }
    public Team? WinnerTeam { get; init; }
    public required int Team1FinalScore { get; init; }
    public required int Team2FinalScore { get; init; }
    public int? DurationSeconds { get; init; }
    public required DateTimeOffset PlayedAt { get; init; }

    /// <summary>The seat of the requesting user.</summary>
    public required PlayerPosition ViewerPosition { get; init; }

    public required IReadOnlyList<MatchSummaryPlayerResponse> Players { get; init; }

    /// <summary>The deals in play order.</summary>
    public required IReadOnlyList<MatchSummaryDealResponse> Deals { get; init; }

    public required MatchSummaryStatsResponse Stats { get; init; }
}

public sealed class MatchSummaryPlayerResponse
{
    public required PlayerPosition Position { get; init; }
    public required Team Team { get; init; }
    public required string DisplayName { get; init; }
    public required bool IsBot { get; init; }

    /// <summary>Tricks won over the deals whose play record is complete.</summary>
    public required int TricksWon { get; init; }

    /// <summary>Deals whose final contract this player announced.</summary>
    public required int Contracts { get; init; }

    /// <summary>Of those contracts, how many the announcing team made.</summary>
    public required int ContractsMade { get; init; }
}

public sealed class MatchSummaryDealResponse
{
    public required int DealNumber { get; init; }
    public required PlayerPosition Dealer { get; init; }

    /// <summary>Null when the deal was not played out (e.g. the match was abandoned during it).</summary>
    public GameMode? GameMode { get; init; }
    public required MultiplierState Multiplier { get; init; }
    public Team? AnnouncerTeam { get; init; }

    /// <summary>The player who announced the final contract, when the negotiation was recorded.</summary>
    public PlayerPosition? Announcer { get; init; }

    /// <summary>Whether the announcing team made its contract; null when unknown.</summary>
    public bool? AnnouncerWon { get; init; }

    public int? Team1CardPoints { get; init; }
    public int? Team2CardPoints { get; init; }
    public required int Team1MatchPoints { get; init; }
    public required int Team2MatchPoints { get; init; }

    /// <summary>Match points of each team once this deal was scored.</summary>
    public required int Team1ScoreAfter { get; init; }
    public required int Team2ScoreAfter { get; init; }

    public required bool WasSweep { get; init; }
    public Team? SweepingTeam { get; init; }
    public required bool IsInstantWin { get; init; }

    /// <summary>The negotiation actions in order (announce, accept, double...).</summary>
    public required IReadOnlyList<NegotiationActionResponse> Negotiation { get; init; }

    /// <summary>The winner of each of the 8 tricks, empty when the play record is incomplete.</summary>
    public required IReadOnlyList<PlayerPosition> TrickWinners { get; init; }
    public required int Team1Tricks { get; init; }
    public required int Team2Tricks { get; init; }

    /// <summary>True when the deal can be opened in the replay viewer.</summary>
    public required bool IsReplayable { get; init; }
}

public sealed class MatchSummaryStatsResponse
{
    /// <summary>How many times the team in the lead changed (ties do not count).</summary>
    public required int LeadChanges { get; init; }

    /// <summary>The deal with the largest match-point swing, and that swing's size.</summary>
    public int? BiggestSwingDeal { get; init; }
    public required int BiggestSwingPoints { get; init; }
    public Team? BiggestSwingTeam { get; init; }

    /// <summary>Longest run of consecutive deals scored by one team.</summary>
    public Team? LongestRunTeam { get; init; }
    public required int LongestRunDeals { get; init; }

    /// <summary>Largest deficit the winning team came back from, 0 when it never trailed.</summary>
    public required int ComebackFrom { get; init; }

    public required int Team1Sweeps { get; init; }
    public required int Team2Sweeps { get; init; }
    public required int Team1Contracts { get; init; }
    public required int Team1ContractsMade { get; init; }
    public required int Team2Contracts { get; init; }
    public required int Team2ContractsMade { get; init; }
    public required int Team1Tricks { get; init; }
    public required int Team2Tricks { get; init; }

    /// <summary>Deals that were doubled at least once.</summary>
    public required int DoubledDeals { get; init; }

    /// <summary>How many deals were played in each game mode.</summary>
    public required IReadOnlyDictionary<GameMode, int> DealsByMode { get; init; }
}
