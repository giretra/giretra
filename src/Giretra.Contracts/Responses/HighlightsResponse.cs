using Giretra.Core.GameModes;

namespace Giretra.Web.Models.Responses;

public sealed class HighlightsResponse
{
    public string? PlayerName { get; init; }
    public required HighlightsHero Hero { get; init; }
    public required IReadOnlyList<HighlightsModeStats> ModeStats { get; init; }
    public required IReadOnlyList<HighlightsEloPoint> EloTrend { get; init; }
    public required HighlightsBidding Bidding { get; init; }
    public required HighlightsSweeps Sweeps { get; init; }
    public required HighlightsTricks Tricks { get; init; }
    public HighlightsPartner? BestPartner { get; init; }
    public HighlightsPartner? Nemesis { get; init; }
    public required IReadOnlyList<HighlightsCallout> Callouts { get; init; }
    public required IReadOnlyList<HighlightsActivityDay> Activity { get; init; }

    public static HighlightsResponse CreateEmpty()
    {
        return new HighlightsResponse
        {
            Hero = new HighlightsHero
            {
                EloRating = 1000,
                GamesPlayed = 0,
                GamesWon = 0,
                WinRate = 0,
                WinStreak = 0,
                BestWinStreak = 0,
                RecentForm = [],
            },
            ModeStats = ((GameMode[])Enum.GetValues(typeof(GameMode))).Select(HighlightsModeStats.Empty).ToList(),
            EloTrend = [],
            Bidding = new HighlightsBidding
            {
                DealsPlayed = 0,
                DealsAnnounced = 0,
                AnnounceRate = 0,
                AnnounceWins = 0,
                AnnounceWinRate = 0,
                DoublesMade = 0,
                DoublesWon = 0,
                RedoublesMade = 0,
                RedoublesWon = 0,
            },
            Sweeps = new HighlightsSweeps
            {
                SweepsFor = 0,
                SweepsAgainst = 0,
                InstantWinsFor = 0,
                InstantWinsAgainst = 0,
            },
            Tricks = new HighlightsTricks
            {
                AnalyzedDeals = 0,
                TricksPlayed = 0,
                TricksWon = 0,
                TrickWinRate = 0,
                LastTrickWins = 0,
                BestTricksInOneDeal = 0,
            },
            BestPartner = null,
            Nemesis = null,
            Callouts = [],
            Activity = [],
        };
    }
}

public sealed class HighlightsHero
{
    /// <summary>Null when viewing another player who keeps their Elo private.</summary>
    public required int? EloRating { get; init; }
    public required int GamesPlayed { get; init; }
    public required int GamesWon { get; init; }
    public required double WinRate { get; init; }
    public required int WinStreak { get; init; }
    public required int BestWinStreak { get; init; }

    /// <summary>Win/loss of the most recent matches, oldest first, at most 10 entries.</summary>
    public required IReadOnlyList<bool> RecentForm { get; init; }
}

public sealed class HighlightsModeStats
{
    public required GameMode Mode { get; init; }
    public required int DealsPlayed { get; init; }
    public required int DealsWon { get; init; }
    public required double DealWinRate { get; init; }
    public required int TimesAnnounced { get; init; }
    public required int AnnounceWins { get; init; }
    public required double AnnounceWinRate { get; init; }
    public required double AvgCardPoints { get; init; }

    public static HighlightsModeStats Empty(GameMode mode) => new()
    {
        Mode = mode,
        DealsPlayed = 0,
        DealsWon = 0,
        DealWinRate = 0,
        TimesAnnounced = 0,
        AnnounceWins = 0,
        AnnounceWinRate = 0,
        AvgCardPoints = 0,
    };
}

public sealed class HighlightsEloPoint
{
    public required DateTimeOffset RecordedAt { get; init; }
    public required int Elo { get; init; }
}

public sealed class HighlightsBidding
{
    public required int DealsPlayed { get; init; }
    public required int DealsAnnounced { get; init; }
    public required double AnnounceRate { get; init; }
    public required int AnnounceWins { get; init; }
    public required double AnnounceWinRate { get; init; }
    public required int DoublesMade { get; init; }
    public required int DoublesWon { get; init; }
    public required int RedoublesMade { get; init; }
    public required int RedoublesWon { get; init; }
}

public sealed class HighlightsSweeps
{
    public required int SweepsFor { get; init; }
    public required int SweepsAgainst { get; init; }
    public required int InstantWinsFor { get; init; }
    public required int InstantWinsAgainst { get; init; }
}

public sealed class HighlightsTricks
{
    /// <summary>Deals whose tricks were replayed (capped to the most recent ones).</summary>
    public required int AnalyzedDeals { get; init; }
    public required int TricksPlayed { get; init; }

    /// <summary>Tricks won by this player personally (not their team).</summary>
    public required int TricksWon { get; init; }
    public required double TrickWinRate { get; init; }
    public required int LastTrickWins { get; init; }
    public required int BestTricksInOneDeal { get; init; }
}

public sealed class HighlightsPartner
{
    public required Guid PlayerId { get; init; }
    public required string DisplayName { get; init; }
    public required bool IsBot { get; init; }
    public required int Games { get; init; }
    public required int Wins { get; init; }
    public required double WinRate { get; init; }
}

public sealed class HighlightsCallout
{
    /// <summary>Stable identifier the frontend maps to an i18n key (e.g. "bestMode").</summary>
    public required string Code { get; init; }

    /// <summary>"strength" or "weakness".</summary>
    public required string Kind { get; init; }

    public GameMode? Mode { get; init; }
    public double? Value { get; init; }
}

public sealed class HighlightsActivityDay
{
    public required string Date { get; init; }
    public required int Count { get; init; }
}
