using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Play;
using Giretra.Core.Players;
using Giretra.Core.Solving;
using Giretra.Core.State;
using Giretra.Web.Achievements;
using Giretra.Web.Models.Responses;
using Giretra.Web.Services.Clips;

namespace Giretra.Web.Tests.Services;

public sealed class ClipMomentDetectorTests
{
    /// <summary>A complete, legal deal played at random (lots of mistakes, so lots of swings).</summary>
    private static ClipMomentDeal RandomDeal(int dealNumber, int seed, bool sweep = false, Team? sweeper = null, MultiplierState multiplier = MultiplierState.Normal, GameMode? mode = null)
    {
        var random = new Random(seed);
        var gameMode = mode ?? (GameMode)random.Next(0, 6);
        var firstLeader = (PlayerPosition)random.Next(0, 4);

        var deck = Deck.CreateShuffled(random);
        var players = new Dictionary<PlayerPosition, Player>();
        foreach (var position in Enum.GetValues<PlayerPosition>())
        {
            var (cards, rest) = deck.Deal(8);
            deck = rest;
            players[position] = Player.Create(position, cards);
        }

        var state = HandState.Create(gameMode, firstLeader);
        var plays = new List<PlayedCard>(32);
        while (state.CurrentTrick is { CurrentPlayer: { } current } trick)
        {
            var valid = PlayValidator.GetValidPlays(players[current], trick, gameMode);
            var card = valid[random.Next(valid.Count)];
            players[current] = players[current].RemoveCard(card);
            plays.Add(new PlayedCard(current, card));
            state = state.PlayCard(card);
        }

        return new ClipMomentDeal(dealNumber, gameMode, (Team)random.Next(0, 2), multiplier, sweep, sweeper, plays);
    }

    [Fact]
    public void Detect_FlagsASweepForTheSweepingTeam_WholeDeal()
    {
        var deal = RandomDeal(3, seed: 1, sweep: true, sweeper: Team.Team2, multiplier: MultiplierState.Doubled);

        var moments = ClipMomentDetector.Detect([deal], []);

        var sweep = Assert.Single(moments, m => m.Kind == ClipMomentKind.Sweep);
        Assert.Equal(3, sweep.DealNumber);
        Assert.Equal((1, 8), (sweep.FromTrick, sweep.ToTrick));
        Assert.Equal(Team.Team2, sweep.Team);
        Assert.Null(sweep.Player);
        Assert.Equal(110, sweep.Score);
        Assert.Same(sweep, moments[0]); // a sweep outranks any solver-found moment
    }

    [Fact]
    public void Detect_MapsAchievementsToTheirDeal_IgnoresMatchLevelOnes()
    {
        var deals = new[] { RandomDeal(1, seed: 2), RandomDeal(2, seed: 3) };
        EarnedAchievementInfo[] achievements =
        [
            new(PlayerPosition.Left, "tapakazo", "Tapakazo", "style", 3, null, false, 2),
            new(PlayerPosition.Bottom, "marathon", "Marathon", "grind", 1, null, false, null),
        ];

        var moments = ClipMomentDetector.Detect(deals, achievements);

        var earned = Assert.Single(moments, m => m.Kind == ClipMomentKind.Achievement);
        Assert.Equal(2, earned.DealNumber);
        Assert.Equal("tapakazo", earned.AchievementCode);
        Assert.Equal(PlayerPosition.Left, earned.Player);
        Assert.Equal(Team.Team2, earned.Team);
        Assert.Equal(86, earned.Score);
    }

    [Fact]
    public void Detect_SkipsIncompleteDeals()
    {
        var full = RandomDeal(1, seed: 4);
        var allPass = full with { DealNumber = 2, Plays = [] };
        var truncated = full with { DealNumber = 3, Plays = full.Plays.Take(20).ToList() };

        var moments = ClipMomentDetector.Detect([allPass, truncated], []);

        Assert.Empty(moments);
    }

    [Fact]
    public void Detect_SolverMoments_AreWellFormedRankedAndVerifiable()
    {
        var deals = Enumerable.Range(1, 6).Select(n => RandomDeal(n, seed: 100 + n)).ToList();

        var moments = ClipMomentDetector.Detect(deals, []);

        Assert.NotEmpty(moments); // random play hands over plenty of match points
        Assert.True(moments.Count <= ClipMomentDetector.MaxMoments);
        Assert.Equal(moments.OrderByDescending(m => m.Score).Select(m => m.Score), moments.Select(m => m.Score));
        Assert.Equal(moments.Count, moments.Select(m => (m.DealNumber, m.Kind, m.Team, m.Player)).Distinct().Count());

        foreach (var moment in moments)
        {
            Assert.InRange(moment.FromTrick, 1, 8);
            Assert.Equal(moment.FromTrick, moment.ToTrick);
            Assert.Contains(moment.Kind, new[] { ClipMomentKind.Swing, ClipMomentKind.BrilliantPlay });
            if (moment.Player is { } player)
                Assert.Equal(moment.Team, player.GetTeam());

            // Cross-check against the solver directly.
            var deal = deals[moment.DealNumber - 1];
            var scoring = new SolverScoring(deal.AnnouncerTeam, deal.Multiplier);
            var hand = deal.Plays.Aggregate(HandState.Create(deal.GameMode, deal.Plays[0].Player), (s, p) => s.PlayCard(p.Card));
            var analysis = HandAnalyzer.Analyze(hand, scoring);
            var trickPlays = analysis.Plays.Where(p => p.TrickNumber == moment.FromTrick).ToList();

            if (moment.Kind == ClipMomentKind.Swing)
            {
                var swing = trickPlays.Where(p => p.Player.GetTeam() != moment.Team).Sum(p => p.MatchPointLoss ?? 0)
                            - trickPlays.Where(p => p.Player.GetTeam() == moment.Team).Sum(p => p.MatchPointLoss ?? 0);
                Assert.Equal(swing, moment.MatchPoints);
                Assert.True(ClipMomentDetector.BasePoints(swing, deal.Multiplier) >= ClipMomentDetector.SwingThreshold);
                Assert.Equal(40 + 2 * ClipMomentDetector.BasePoints(swing, deal.Multiplier), moment.Score);
            }
            else
            {
                var play = Assert.Single(trickPlays, p => p.Player == moment.Player && p.IsOptimal && !p.IsForced
                    && p.Evaluation.Cards.Where(c => !c.IsOptimal).Min(c => p.Evaluation.GetMatchPointLoss(c.Card, scoring)) == moment.MatchPoints);
                Assert.True(ClipMomentDetector.BasePoints(moment.MatchPoints, deal.Multiplier) >= ClipMomentDetector.BrilliantThreshold);
                Assert.Equal(50 + 2 * ClipMomentDetector.BasePoints(moment.MatchPoints, deal.Multiplier), moment.Score);
                Assert.True(play.Evaluation.Cards.Count > 1);
            }
        }
    }

    [Fact]
    public void Detect_RanksOnBasePoints_SoMultipliedDealsDoNotDominate()
    {
        // Not every random deal has a swing or an only-move; take the first seed that does. No-trumps:
        // in a colour deal a sweep is an instant match win, whose value does not scale with the multiplier.
        var normal = Enumerable.Range(300, 40).Select(seed => RandomDeal(1, seed, mode: GameMode.NoTrumps)).First(d => ClipMomentDetector.Detect([d], []).Count > 0);
        var redoubled = normal with { DealNumber = 2, Multiplier = MultiplierState.ReRedoubled };

        var moments = ClipMomentDetector.Detect([normal, redoubled], []);

        var byDeal = moments.ToLookup(m => m.DealNumber);
        Assert.NotEmpty(byDeal[1]);
        foreach (var a in byDeal[1])
        {
            var b = Assert.Single(byDeal[2], m => m.Kind == a.Kind && m.Team == a.Team && m.Player == a.Player);
            Assert.Equal(a.FromTrick, b.FromTrick);
            Assert.Equal(a.MatchPoints * 8, b.MatchPoints); // what the scoreboard would show
            Assert.Equal(a.Score, b.Score);                 // same rank
        }
        Assert.Equal(8, ClipMomentDetector.BasePoints(64, MultiplierState.ReRedoubled));
        Assert.Equal(ClipMomentDetector.MaxBasePoints, ClipMomentDetector.BasePoints(150, MultiplierState.Normal));
    }

    [Fact]
    public void Detect_KeepsOnePlayPerPlayerAndOneSwingPerTeamPerDeal()
    {
        var deals = Enumerable.Range(1, 6).Select(n => RandomDeal(n, seed: 400 + n)).ToList();

        var moments = ClipMomentDetector.Detect(deals, []);

        Assert.All(moments.GroupBy(m => (m.DealNumber, m.Kind, m.Team, m.Player)), g => Assert.Single(g));
    }

    [Fact]
    public void Detect_IsDeterministic()
    {
        var deals = Enumerable.Range(1, 3).Select(n => RandomDeal(n, seed: 200 + n)).ToList();

        var first = ClipMomentDetector.Detect(deals, []);
        var second = ClipMomentDetector.Detect(deals, []);

        Assert.Equal(
            first.Select(m => (m.Kind, m.DealNumber, m.FromTrick, m.Team, m.Player, m.MatchPoints, m.Score)),
            second.Select(m => (m.Kind, m.DealNumber, m.FromTrick, m.Team, m.Player, m.MatchPoints, m.Score)));
    }
}
