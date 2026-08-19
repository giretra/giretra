using CoreGameModes = Giretra.Core.GameModes;
using Giretra.Core.Play;
using Giretra.Model;
using Giretra.Model.Entities;
using Giretra.Model.Enums;
using Giretra.Web.Models.Responses;
using Microsoft.EntityFrameworkCore;
using CoreCard = Giretra.Core.Cards.Card;
using CoreCardRank = Giretra.Core.Cards.CardRank;
using CoreCardSuit = Giretra.Core.Cards.CardSuit;
using CoreGameMode = Giretra.Core.GameModes.GameMode;

namespace Giretra.Web.Services;

public sealed class HighlightsService : IHighlightsService
{
    private const int MinGamesWithPlayer = 5;
    private const int MaxEloPoints = 200;
    private const int RecentFormSize = 10;
    private const int ActivityDays = 365;
    private const int MaxCallouts = 4;
    private const int MaxDealsForTricks = 400;

    private readonly GiretraDbContext _db;

    public HighlightsService(GiretraDbContext db)
    {
        _db = db;
    }

    public async Task<HighlightsResponse> GetHighlightsAsync(Guid userId)
    {
        var player = await _db.Players
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == userId);
        if (player == null)
            return HighlightsResponse.CreateEmpty();

        return await BuildAsync(player, player.User?.EffectiveDisplayName, includeElo: true);
    }

    public async Task<HighlightsResponse?> GetPlayerHighlightsAsync(Guid playerId)
    {
        var player = await _db.Players
            .Include(p => p.User)
            .Include(p => p.Bot)
            .FirstOrDefaultAsync(p => p.Id == playerId);
        if (player == null)
            return null;

        var name = player.PlayerType == PlayerType.Bot
            ? player.Bot?.DisplayName ?? "Bot"
            : player.User?.EffectiveDisplayName ?? "Unknown";

        return await BuildAsync(player, name, includeElo: player.EloIsPublic);
    }

    private async Task<HighlightsResponse> BuildAsync(Player player, string? playerName, bool includeElo)
    {
        var playerId = player.Id;

        var participations = await _db.MatchPlayers
            .Where(mp => mp.PlayerId == playerId && mp.Match.CompletedAt != null)
            .Select(mp => new { mp.MatchId, mp.Team, mp.IsWinner, CompletedAt = mp.Match.CompletedAt!.Value })
            .ToListAsync();

        var deals = await _db.Deals
            .Where(d => d.GameMode != null)
            .Join(
                _db.MatchPlayers.Where(mp => mp.PlayerId == playerId),
                d => d.MatchId,
                mp => mp.MatchId,
                (d, mp) => new DealRow(
                    d.GameMode!.Value,
                    d.Team1CardPoints,
                    d.Team2CardPoints,
                    d.Team1MatchPoints,
                    d.Team2MatchPoints,
                    d.WasSweep,
                    d.SweepingTeam,
                    d.IsInstantWin,
                    mp.Team))
            .ToListAsync();

        // My negotiation actions on the FINAL mode of each deal. Per NegotiationState.ResolveFinalMode,
        // the final announcer is the player who announced Deal.GameMode on Deal.AnnouncerTeam (when a mode
        // was doubled that is the first doubled announcement, not the last bid), so matching the action's
        // GameMode against the deal's final mode attributes announces/doubles/redoubles correctly.
        var negotiationActions = await _db.DealActions
            .Where(da => da.ActionType == ActionType.Announce
                || da.ActionType == ActionType.Double
                || da.ActionType == ActionType.Redouble)
            .Join(_db.Deals.Where(d => d.GameMode != null), da => da.DealId, d => d.Id, (da, d) => new { da, d })
            .Join(
                _db.MatchPlayers.Where(mp => mp.PlayerId == playerId),
                x => x.d.MatchId,
                mp => mp.MatchId,
                (x, mp) => new { x.da, x.d, mp })
            .Where(x => x.da.PlayerPosition == x.mp.Position && x.da.GameMode == x.d.GameMode)
            .Select(x => new
            {
                x.da.DealId,
                x.da.ActionType,
                Mode = x.d.GameMode!.Value,
                x.d.AnnouncerTeam,
                x.d.AnnouncerWon,
                MyTeam = x.mp.Team,
            })
            .ToListAsync();

        List<HighlightsEloPoint> eloTrend = [];
        if (includeElo)
        {
            var eloPoints = await _db.EloHistories
                .Where(h => h.PlayerId == playerId)
                .OrderByDescending(h => h.RecordedAt)
                .Take(MaxEloPoints)
                .Select(h => new { h.RecordedAt, h.EloAfter })
                .ToListAsync();
            eloPoints.Reverse();
            eloTrend = eloPoints
                .Select(p => new HighlightsEloPoint { RecordedAt = p.RecordedAt, Elo = p.EloAfter })
                .ToList();
        }

        // Co-participant rows are grouped in memory (keeps the query InMemory-provider friendly).
        var coRows = await _db.MatchPlayers
            .Where(me => me.PlayerId == playerId)
            .Join(_db.MatchPlayers, me => me.MatchId, other => other.MatchId, (me, other) => new { me, other })
            .Where(x => x.other.PlayerId != playerId)
            .Select(x => new
            {
                OtherPlayerId = x.other.PlayerId,
                SameTeam = x.other.Team == x.me.Team,
                MyWin = x.me.IsWinner,
            })
            .ToListAsync();

        var tricks = await ComputeTricksAsync(playerId);

        // ── Hero ──
        var recentForm = participations
            .OrderByDescending(x => x.CompletedAt)
            .Take(RecentFormSize)
            .OrderBy(x => x.CompletedAt)
            .Select(x => x.IsWinner)
            .ToList();

        var hero = new HighlightsHero
        {
            EloRating = includeElo ? player.EloRating : null,
            GamesPlayed = player.GamesPlayed,
            GamesWon = player.GamesWon,
            WinRate = Percent(player.GamesWon, player.GamesPlayed),
            WinStreak = player.WinStreak,
            BestWinStreak = player.BestWinStreak,
            RecentForm = recentForm,
        };

        // ── Per-mode deal stats + sweeps ──
        var announcesByDeal = negotiationActions
            .Where(a => a.ActionType == ActionType.Announce && a.MyTeam == a.AnnouncerTeam)
            .GroupBy(a => a.DealId)
            .Select(g => g.First())
            .ToList();

        var announcedByMode = announcesByDeal
            .GroupBy(a => a.Mode)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Wins: g.Count(a => a.AnnouncerWon == true)));

        var modeStats = new List<HighlightsModeStats>();
        foreach (var mode in Enum.GetValues<GameMode>())
        {
            var modeDeals = deals.Where(d => d.Mode == mode).ToList();
            var wins = modeDeals.Count(d => d.Won);
            var announced = announcedByMode.GetValueOrDefault(mode);
            var cardPoints = modeDeals
                .Select(d => d.MyCardPoints)
                .Where(p => p.HasValue)
                .Select(p => p!.Value)
                .ToList();

            modeStats.Add(new HighlightsModeStats
            {
                Mode = (CoreGameModes.GameMode)(int)mode,
                DealsPlayed = modeDeals.Count,
                DealsWon = wins,
                DealWinRate = Percent(wins, modeDeals.Count),
                TimesAnnounced = announced.Count,
                AnnounceWins = announced.Wins,
                AnnounceWinRate = Percent(announced.Wins, announced.Count),
                AvgCardPoints = cardPoints.Count > 0 ? Math.Round(cardPoints.Average(), 1) : 0,
            });
        }

        var sweeps = new HighlightsSweeps
        {
            SweepsFor = deals.Count(d => d.WasSweep && d.SweepingTeam == d.MyTeam),
            SweepsAgainst = deals.Count(d => d.WasSweep && d.SweepingTeam != null && d.SweepingTeam != d.MyTeam),
            InstantWinsFor = deals.Count(d => d.IsInstantWin && d.Won),
            InstantWinsAgainst = deals.Count(d => d.IsInstantWin && !d.Won),
        };

        // ── Bidding ──
        var doubleDeals = negotiationActions
            .Where(a => a.ActionType == ActionType.Double)
            .GroupBy(a => a.DealId)
            .Select(g => g.First())
            .ToList();
        var redoubleDeals = negotiationActions
            .Where(a => a.ActionType == ActionType.Redouble)
            .GroupBy(a => a.DealId)
            .Select(g => g.First())
            .ToList();

        var announceWins = announcesByDeal.Count(a => a.AnnouncerWon == true);
        var bidding = new HighlightsBidding
        {
            DealsPlayed = deals.Count,
            DealsAnnounced = announcesByDeal.Count,
            AnnounceRate = Percent(announcesByDeal.Count, deals.Count),
            AnnounceWins = announceWins,
            AnnounceWinRate = Percent(announceWins, announcesByDeal.Count),
            DoublesMade = doubleDeals.Count,
            // A doubler is on the non-announcer team, so the double pays off when the announcer loses.
            DoublesWon = doubleDeals.Count(a => a.AnnouncerWon == false),
            RedoublesMade = redoubleDeals.Count,
            RedoublesWon = redoubleDeals.Count(a => a.AnnouncerWon == true),
        };

        // ── Partner / nemesis ──
        var coStats = coRows
            .GroupBy(r => new { r.OtherPlayerId, r.SameTeam })
            .Select(g => new
            {
                g.Key.OtherPlayerId,
                g.Key.SameTeam,
                Games = g.Count(),
                MyWins = g.Count(r => r.MyWin),
            })
            .Where(s => s.Games >= MinGamesWithPlayer)
            .ToList();

        var bestPartnerStats = coStats
            .Where(s => s.SameTeam)
            .OrderByDescending(s => Percent(s.MyWins, s.Games))
            .ThenByDescending(s => s.Games)
            .FirstOrDefault();
        var nemesisStats = coStats
            .Where(s => !s.SameTeam)
            .OrderBy(s => Percent(s.MyWins, s.Games))
            .ThenByDescending(s => s.Games)
            .FirstOrDefault();

        var partnerIds = new[] { bestPartnerStats?.OtherPlayerId, nemesisStats?.OtherPlayerId }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var partnerPlayers = partnerIds.Count > 0
            ? await _db.Players
                .Include(p => p.User)
                .Include(p => p.Bot)
                .Where(p => partnerIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id)
            : [];

        HighlightsPartner? ToPartner(Guid otherId, int games, int myWins)
        {
            if (!partnerPlayers.TryGetValue(otherId, out var other))
                return null;

            var isBot = other.PlayerType == PlayerType.Bot;
            return new HighlightsPartner
            {
                PlayerId = other.Id,
                DisplayName = isBot
                    ? other.Bot?.DisplayName ?? "Bot"
                    : other.User?.EffectiveDisplayName ?? "Unknown",
                IsBot = isBot,
                Games = games,
                Wins = myWins,
                WinRate = Percent(myWins, games),
            };
        }

        var bestPartner = bestPartnerStats != null
            ? ToPartner(bestPartnerStats.OtherPlayerId, bestPartnerStats.Games, bestPartnerStats.MyWins)
            : null;
        var nemesis = nemesisStats != null
            ? ToPartner(nemesisStats.OtherPlayerId, nemesisStats.Games, nemesisStats.MyWins)
            : null;

        // ── Activity (last 365 days) ──
        var activityCutoff = DateTimeOffset.UtcNow.AddDays(-ActivityDays);
        var activity = participations
            .Where(x => x.CompletedAt >= activityCutoff)
            .GroupBy(x => DateOnly.FromDateTime(x.CompletedAt.UtcDateTime))
            .OrderBy(g => g.Key)
            .Select(g => new HighlightsActivityDay { Date = g.Key.ToString("yyyy-MM-dd"), Count = g.Count() })
            .ToList();

        var callouts = BuildCallouts(hero, modeStats, bidding, sweeps, tricks);

        return new HighlightsResponse
        {
            PlayerName = playerName,
            Hero = hero,
            ModeStats = modeStats,
            EloTrend = eloTrend,
            Bidding = bidding,
            Sweeps = sweeps,
            Tricks = tricks,
            BestPartner = bestPartner,
            Nemesis = nemesis,
            Callouts = callouts,
            Activity = activity,
        };
    }

    /// <summary>
    /// Replays the play actions of the player's most recent deals to count tricks won
    /// personally (trick winners are not persisted).
    /// </summary>
    private async Task<HighlightsTricks> ComputeTricksAsync(Guid playerId)
    {
        var trickDeals = await _db.Deals
            .Where(d => d.GameMode != null)
            .Join(
                _db.MatchPlayers.Where(mp => mp.PlayerId == playerId),
                d => d.MatchId,
                mp => mp.MatchId,
                (d, mp) => new { d.Id, Mode = d.GameMode!.Value, MyPosition = mp.Position, d.CompletedAt })
            .OrderByDescending(x => x.CompletedAt)
            .Take(MaxDealsForTricks)
            .ToListAsync();

        var empty = new HighlightsTricks
        {
            AnalyzedDeals = 0,
            TricksPlayed = 0,
            TricksWon = 0,
            TrickWinRate = 0,
            LastTrickWins = 0,
            BestTricksInOneDeal = 0,
        };

        if (trickDeals.Count == 0)
            return empty;

        var dealInfo = trickDeals.ToDictionary(x => x.Id, x => (x.Mode, x.MyPosition));
        var dealIds = trickDeals.Select(x => x.Id).ToList();

        // Play actions are the only ones carrying a TrickNumber, so selecting on it avoids
        // depending on the action_type strings (legacy rows carry "6" instead of "play_card").
        var playRows = await _db.DealActions
            .Where(da => dealIds.Contains(da.DealId)
                && da.TrickNumber != null && da.CardRank != null && da.CardSuit != null)
            .Select(da => new
            {
                da.DealId,
                da.ActionOrder,
                da.PlayerPosition,
                Rank = da.CardRank!.Value,
                Suit = da.CardSuit!.Value,
                Trick = da.TrickNumber!.Value,
            })
            .ToListAsync();

        if (playRows.Count == 0)
            return empty;

        var analyzedDeals = 0;
        var tricksPlayed = 0;
        var tricksWon = 0;
        var lastTrickWins = 0;
        var bestInOneDeal = 0;

        foreach (var dealGroup in playRows.GroupBy(r => r.DealId))
        {
            var (mode, myPosition) = dealInfo[dealGroup.Key];
            var coreMode = (CoreGameMode)(int)mode;
            var myTricksThisDeal = 0;
            var dealHadTricks = false;

            foreach (var trickGroup in dealGroup.GroupBy(r => r.Trick).OrderBy(g => g.Key))
            {
                var plays = trickGroup.OrderBy(r => r.ActionOrder).ToList();
                if (plays.Count != 4)
                    continue;

                dealHadTricks = true;
                tricksPlayed++;

                var cards = plays
                    .Select(p => new CoreCard((CoreCardRank)(int)p.Rank, (CoreCardSuit)(int)p.Suit))
                    .ToList();
                var leadSuit = cards[0].Suit;

                var winnerIndex = 0;
                for (var i = 1; i < cards.Count; i++)
                {
                    if (CardComparer.Beats(cards[i], cards[winnerIndex], leadSuit, coreMode))
                        winnerIndex = i;
                }

                if (plays[winnerIndex].PlayerPosition == myPosition)
                {
                    tricksWon++;
                    myTricksThisDeal++;
                    if (trickGroup.Key == 8)
                        lastTrickWins++;
                }
            }

            if (dealHadTricks)
            {
                analyzedDeals++;
                bestInOneDeal = Math.Max(bestInOneDeal, myTricksThisDeal);
            }
        }

        return new HighlightsTricks
        {
            AnalyzedDeals = analyzedDeals,
            TricksPlayed = tricksPlayed,
            TricksWon = tricksWon,
            TrickWinRate = Percent(tricksWon, tricksPlayed),
            LastTrickWins = lastTrickWins,
            BestTricksInOneDeal = bestInOneDeal,
        };
    }

    private sealed record DealRow(
        GameMode Mode,
        int? Team1CardPoints,
        int? Team2CardPoints,
        int? Team1MatchPoints,
        int? Team2MatchPoints,
        bool WasSweep,
        Team? SweepingTeam,
        bool IsInstantWin,
        Team MyTeam)
    {
        public int? MyCardPoints => MyTeam == Team.Team1 ? Team1CardPoints : Team2CardPoints;

        public bool Won => (MyTeam == Team.Team1 ? Team1MatchPoints ?? 0 : Team2MatchPoints ?? 0)
            > (MyTeam == Team.Team1 ? Team2MatchPoints ?? 0 : Team1MatchPoints ?? 0);
    }

    private static IReadOnlyList<HighlightsCallout> BuildCallouts(
        HighlightsHero hero,
        IReadOnlyList<HighlightsModeStats> modeStats,
        HighlightsBidding bidding,
        HighlightsSweeps sweeps,
        HighlightsTricks tricks)
    {
        var strengths = new List<HighlightsCallout>();
        var weaknesses = new List<HighlightsCallout>();

        if (hero.WinStreak >= 3)
        {
            strengths.Add(new HighlightsCallout
            {
                Code = "onFire",
                Kind = "strength",
                Value = hero.WinStreak,
            });
        }

        var eligibleModes = modeStats.Where(m => m.DealsPlayed >= 10).ToList();
        var bestMode = eligibleModes.OrderByDescending(m => m.DealWinRate).FirstOrDefault();
        if (bestMode != null && bestMode.DealWinRate >= 55)
        {
            strengths.Add(new HighlightsCallout
            {
                Code = "bestMode",
                Kind = "strength",
                Mode = bestMode.Mode,
                Value = bestMode.DealWinRate,
            });
        }

        var worstMode = eligibleModes.OrderBy(m => m.DealWinRate).FirstOrDefault();
        if (worstMode != null && worstMode.DealWinRate <= 45 && worstMode != bestMode)
        {
            weaknesses.Add(new HighlightsCallout
            {
                Code = "worstMode",
                Kind = "weakness",
                Mode = worstMode.Mode,
                Value = worstMode.DealWinRate,
            });
        }

        if (bidding.DealsAnnounced >= 10)
        {
            if (bidding.AnnounceWinRate >= 60)
            {
                strengths.Add(new HighlightsCallout
                {
                    Code = "strongAnnouncer",
                    Kind = "strength",
                    Value = bidding.AnnounceWinRate,
                });
            }
            else if (bidding.AnnounceWinRate < 45)
            {
                weaknesses.Add(new HighlightsCallout
                {
                    Code = "riskyAnnouncer",
                    Kind = "weakness",
                    Value = bidding.AnnounceWinRate,
                });
            }
        }

        // A quarter of all tricks is the neutral expectation for one of four players.
        if (tricks.TricksPlayed >= 80)
        {
            if (tricks.TrickWinRate >= 30)
            {
                strengths.Add(new HighlightsCallout
                {
                    Code = "trickMaster",
                    Kind = "strength",
                    Value = tricks.TrickWinRate,
                });
            }
            else if (tricks.TrickWinRate <= 20)
            {
                weaknesses.Add(new HighlightsCallout
                {
                    Code = "trickShy",
                    Kind = "weakness",
                    Value = tricks.TrickWinRate,
                });
            }
        }

        if (bidding.DoublesMade >= 5 && Percent(bidding.DoublesWon, bidding.DoublesMade) >= 60)
        {
            strengths.Add(new HighlightsCallout
            {
                Code = "sharpDoubler",
                Kind = "strength",
                Value = Percent(bidding.DoublesWon, bidding.DoublesMade),
            });
        }

        if (sweeps.SweepsFor >= 5 && sweeps.SweepsFor > sweeps.SweepsAgainst)
        {
            strengths.Add(new HighlightsCallout
            {
                Code = "sweepArtist",
                Kind = "strength",
                Value = sweeps.SweepsFor,
            });
        }
        else if (sweeps.SweepsAgainst >= 5 && sweeps.SweepsAgainst > sweeps.SweepsFor)
        {
            weaknesses.Add(new HighlightsCallout
            {
                Code = "sweepVictim",
                Kind = "weakness",
                Value = sweeps.SweepsAgainst,
            });
        }

        // Keep the list balanced: up to two of each, backfilling from whichever side has more.
        var callouts = strengths.Take(2).Concat(weaknesses.Take(2)).ToList();
        callouts.AddRange(strengths.Skip(2).Concat(weaknesses.Skip(2)).Take(MaxCallouts - callouts.Count));
        return callouts.Take(MaxCallouts).ToList();
    }

    private static double Percent(int part, int total)
    {
        return total > 0
            ? Math.Round((double)part / total * 100, 1)
            : 0;
    }
}
