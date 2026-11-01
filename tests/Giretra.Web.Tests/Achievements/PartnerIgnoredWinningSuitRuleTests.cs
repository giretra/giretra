using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Play;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.State;
using Giretra.Web.Achievements;
using Giretra.Web.Achievements.Rules;
using CorePlayer = Giretra.Core.Players.Player;

namespace Giretra.Web.Tests.Achievements;

public sealed class PartnerIgnoredWinningSuitRuleTests
{
    private readonly PartnerIgnoredWinningSuitRule _rule = new();

    private const PlayerPosition Player = PlayerPosition.Bottom;
    private const PlayerPosition Partner = PlayerPosition.Top;
    private const PlayerPosition Left = PlayerPosition.Left;
    private const PlayerPosition Right = PlayerPosition.Right;

    // NoTrumps hands used by most scenarios (ranking A > 10 > K > Q > J > 9 > 8 > 7):
    //   Player:  AH 10H 8S 10S JD QD JC QC   -> hearts are a full winning suit
    //   Partner: 7S AD 7D 8D QH KH 7C 8C     -> one lone master (AD), otherwise weak
    //   Left:    AS QS JS KD 9H 8H AC KC
    //   Right:   KS 9S 9D 10D 7H JH 9C 10C

    /// <summary>Partner leads a losing spade, wins a trick, then leads a losing diamond: two lost opportunities.</summary>
    private static readonly (PlayerPosition Leader, string[] Cards)[] IgnoredTwice =
    [
        (Partner, ["7S", "KS", "8S", "AS"]),
        (Left, ["KD", "AD", "9D", "JD"]),
        (Partner, ["7D", "10D", "QD", "QS"]),
        (Right, ["9S", "10S", "JS", "7C"]),
        (Player, ["AH", "9H", "QH", "7H"]),
        (Player, ["10H", "8H", "KH", "JH"]),
        (Player, ["JC", "AC", "8C", "9C"]),
        (Left, ["KC", "8D", "10C", "QC"]),
    ];

    [Fact]
    public async Task Earned_WhenPartnerTwiceLosesATrickInsteadOfLeadingTheWinningSuit()
    {
        var context = BuildContext(GameMode.NoTrumps, IgnoredTwice);

        Assert.Equal(2, CountLostOpportunities(context));
        Assert.True(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task Earned_InAllTrumps()
    {
        // AllTrumps ranking J > 9 > A > 10 > K > Q > 8 > 7; the player holds JH 9H.
        //   Player:  JH 9H 8S 10S 10D KD KC 10C
        //   Partner: 7S JD 7D 8D 8H QH 7C 8C
        //   Left:    JS QS KS AD 7H KH JC AC
        //   Right:   AS 9S QD 9D AH 10H QC 9C
        (PlayerPosition Leader, string[] Cards)[] tricks =
        [
            (Partner, ["7S", "AS", "8S", "JS"]),
            (Left, ["AD", "JD", "QD", "10D"]),
            (Partner, ["7D", "9D", "KD", "7H"]),
            (Right, ["9S", "10S", "QS", "7C"]),
            (Right, ["AH", "JH", "KH", "8H"]),
            (Player, ["9H", "KS", "QH", "10H"]),
            (Player, ["KC", "JC", "8C", "QC"]),
            (Left, ["AC", "8D", "9C", "10C"]),
        ];

        var context = BuildContext(GameMode.AllTrumps, tricks);

        Assert.Equal(2, CountLostOpportunities(context));
        Assert.True(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task Earned_WhenSuitIsWinningThroughRelativeMasters()
    {
        // Player holds AH KH while the 10H sits in the partner's hand: from the player's point of
        // view KH is a relative master (AH draws the only stronger unseen heart).
        (PlayerPosition Leader, string[] Cards)[] tricks =
        [
            (Partner, ["7S", "KS", "8S", "AS"]),
            (Left, ["KD", "AD", "9D", "JD"]),
            (Partner, ["7D", "10D", "QD", "QS"]),
            (Right, ["9S", "10S", "JS", "7C"]),
            (Player, ["AH", "9H", "QH", "7H"]),
            (Player, ["KH", "8H", "10H", "JH"]),
            (Partner, ["8C", "9C", "JC", "AC"]),
            (Left, ["KC", "8D", "10C", "QC"]),
        ];

        var context = BuildContext(GameMode.NoTrumps, tricks);

        Assert.Equal(2, CountLostOpportunities(context));
        Assert.True(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenPartnerLeadsTheWinningSuitTheSecondTime()
    {
        (PlayerPosition Leader, string[] Cards)[] tricks =
        [
            (Partner, ["7S", "KS", "8S", "AS"]),
            (Left, ["KD", "AD", "9D", "JD"]),
            (Partner, ["QH", "7H", "AH", "8H"]),
            (Player, ["10H", "9H", "KH", "JH"]),
            (Player, ["10S", "JS", "7D", "9S"]),
            (Player, ["QD", "QS", "8D", "10D"]),
            (Right, ["9C", "JC", "AC", "7C"]),
            (Left, ["KC", "8C", "10C", "QC"]),
        ];

        var context = BuildContext(GameMode.NoTrumps, tricks);

        Assert.Equal(1, CountLostOpportunities(context));
        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenPartnerWinsTheTrickHeLedWithAnotherSuit()
    {
        // The partner cashes AD first: another suit, but the trick is not lost, so it is not an
        // opportunity thrown away. Only the losing 7S lead counts.
        (PlayerPosition Leader, string[] Cards)[] tricks =
        [
            (Partner, ["AD", "9D", "JD", "KD"]),
            (Partner, ["7S", "KS", "8S", "AS"]),
            (Left, ["QS", "7D", "9S", "10S"]),
            (Player, ["AH", "9H", "QH", "7H"]),
            (Player, ["10H", "8H", "KH", "JH"]),
            (Player, ["JC", "AC", "7C", "9C"]),
            (Left, ["KC", "8C", "10C", "QC"]),
            (Right, ["10D", "QD", "JS", "8D"]),
        ];

        var context = BuildContext(GameMode.NoTrumps, tricks);

        Assert.Equal(1, CountLostOpportunities(context));
        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenThePlayersSuitIsNotFullyWinning()
    {
        // Player holds AH 8H: the 8H is no master while 10H, KH, QH, JH and 9H are unseen.
        (PlayerPosition Leader, string[] Cards)[] tricks =
        [
            (Partner, ["7S", "KS", "8S", "AS"]),
            (Left, ["KD", "AD", "9D", "JD"]),
            (Partner, ["7D", "10D", "QD", "QS"]),
            (Right, ["9S", "10S", "JS", "7C"]),
            (Player, ["AH", "10H", "QH", "7H"]),
            (Player, ["8H", "9H", "KH", "JH"]),
            (Partner, ["8C", "9C", "JC", "AC"]),
            (Left, ["KC", "8D", "10C", "QC"]),
        ];

        var context = BuildContext(GameMode.NoTrumps, tricks);

        Assert.Equal(0, CountLostOpportunities(context));
        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenPartnerIsVoidInThePlayersWinningSuit()
    {
        // Partner holds no heart at all (7S AD 7D 8D QS JS 7C 8C), so he never had the
        // opportunity to lead the player's hearts, however many tricks he loses.
        (PlayerPosition Leader, string[] Cards)[] tricks =
        [
            (Partner, ["7S", "KS", "8S", "AS"]),
            (Left, ["KD", "AD", "9D", "JD"]),
            (Partner, ["7D", "10D", "QD", "8H"]),
            (Right, ["9S", "10S", "9H", "JS"]),
            (Player, ["AH", "QH", "7C", "7H"]),
            (Player, ["10H", "KH", "8C", "JH"]),
            (Player, ["JC", "AC", "QS", "9C"]),
            (Left, ["KC", "8D", "10C", "QC"]),
        ];

        var context = BuildContext(GameMode.NoTrumps, tricks);

        Assert.Equal(0, CountLostOpportunities(context));
        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_InColourMode()
    {
        // Same plays, but the deal was a Colour: the rule only applies to AllTrumps and NoTrumps.
        var context = BuildContext(GameMode.NoTrumps, IgnoredTwice);
        context = context with { DealResult = context.DealResult! with { GameMode = GameMode.ColourClubs } };

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_ByThePartnerWhoThrewTheOpportunitiesAway()
    {
        var context = BuildContext(GameMode.NoTrumps, IgnoredTwice) with
        {
            PlayerPosition = Partner,
            PlayerTeam = Partner.GetTeam()
        };

        Assert.Equal(0, CountLostOpportunities(context));
        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenTricksAreIncomplete()
    {
        var context = BuildContext(GameMode.NoTrumps, IgnoredTwice, trickCount: 7);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenThereIsNoDealResult()
    {
        var context = BuildContext(GameMode.NoTrumps, IgnoredTwice) with { DealResult = null };

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Theory]
    [InlineData(GameMode.NoTrumps)]
    [InlineData(GameMode.AllTrumps)]
    public async Task RuleAgreesWithValueBasedOracleOnRandomDeals(GameMode gameMode)
    {
        var random = new Random(gameMode == GameMode.NoTrumps ? 20261101 : 20261102);
        var earned = 0;

        for (var i = 0; i < 400; i++)
        {
            var context = BuildRandomContext(gameMode, random);
            var expected = CountLostOpportunities(context) >= 2;

            Assert.Equal(expected, await _rule.IsEarnedAsync(context));
            if (expected)
                earned++;
        }

        // Sanity check that the random deals exercise both outcomes.
        Assert.InRange(earned, 1, 399);
    }

    // ------------------------------------------------------------------ value-based oracle

    private static readonly CardRank[] NoTrumpsOrder =
        [CardRank.Ace, CardRank.Ten, CardRank.King, CardRank.Queen, CardRank.Jack, CardRank.Nine, CardRank.Eight, CardRank.Seven];

    private static readonly CardRank[] AllTrumpsOrder =
        [CardRank.Jack, CardRank.Nine, CardRank.Ace, CardRank.Ten, CardRank.King, CardRank.Queen, CardRank.Eight, CardRank.Seven];

    /// <summary>Strength from card values only: 8 for the strongest rank, 1 for the weakest.</summary>
    private static int Strength(Card card, GameMode gameMode)
    {
        var order = gameMode == GameMode.AllTrumps ? AllTrumpsOrder : NoTrumpsOrder;
        return 8 - Array.IndexOf(order, card.Rank);
    }

    /// <summary>
    /// Master from card values: no unseen card of the suit is stronger. Relative master: the hand
    /// holds at least as many true masters above the card as there are stronger unseen cards.
    /// </summary>
    private static bool IsMasterByValues(Card card, GameMode gameMode, IReadOnlyList<Card> hand, IReadOnlySet<Card> played)
    {
        var unseen = Enum.GetValues<CardRank>()
            .Select(r => new Card(r, card.Suit))
            .Where(c => c != card && !hand.Contains(c) && !played.Contains(c))
            .ToList();

        var strength = Strength(card, gameMode);
        var strongerUnseen = unseen.Count(c => Strength(c, gameMode) > strength);
        if (strongerUnseen == 0)
            return true;

        var trueMastersAbove = hand.Count(h =>
            h.Suit == card.Suit
            && Strength(h, gameMode) > strength
            && unseen.All(u => Strength(u, gameMode) < Strength(h, gameMode)));

        return trueMastersAbove >= strongerUnseen;
    }

    /// <summary>Winner from card values: the strongest card of the led suit (no trumps in AllTrumps/NoTrumps).</summary>
    private static PlayerPosition WinnerByValues(IReadOnlyList<PlayedCard> plays, GameMode gameMode)
        => plays.Where(p => p.Card.Suit == plays[0].Card.Suit).MaxBy(p => Strength(p.Card, gameMode)).Player;

    /// <summary>
    /// Independent reimplementation of the rule on card values: the partner leads, the player
    /// holds a 2+ card suit that is entirely master/relative master, the partner still holds a
    /// card of that suit but led another suit, and the opponents won the trick.
    /// </summary>
    private static int CountLostOpportunities(AchievementContext context)
    {
        var gameMode = context.DealResult!.GameMode;
        var player = context.PlayerPosition;
        var partner = player.Teammate();
        var played = new HashSet<Card>();
        var count = 0;

        for (var i = 0; i < context.Tricks.Count; i++)
        {
            var plays = context.Tricks[i].Plays;
            if (plays[0].Player == partner && WinnerByValues(plays, gameMode).GetTeam() != player.GetTeam())
            {
                var hand = context.Tricks.Skip(i).Select(t => t.Plays.First(p => p.Player == player).Card).ToList();
                var partnerHand = context.Tricks.Skip(i).Select(t => t.Plays.First(p => p.Player == partner).Card).ToList();
                var winningSuits = Enum.GetValues<CardSuit>()
                    .Where(s => hand.Count(c => c.Suit == s) >= 2)
                    .Where(s => partnerHand.Any(c => c.Suit == s))
                    .Where(s => hand.Where(c => c.Suit == s).All(c => IsMasterByValues(c, gameMode, hand, played)))
                    .ToList();

                if (winningSuits.Count > 0 && !winningSuits.Contains(plays[0].Card.Suit))
                    count++;
            }

            foreach (var play in plays)
                played.Add(play.Card);
        }

        return count;
    }

    // ------------------------------------------------------------------ deal builders

    private static Card ParseCard(string text)
    {
        var rank = text[..^1] switch
        {
            "A" => CardRank.Ace, "10" => CardRank.Ten, "K" => CardRank.King, "Q" => CardRank.Queen,
            "J" => CardRank.Jack, "9" => CardRank.Nine, "8" => CardRank.Eight, "7" => CardRank.Seven,
            var r => throw new ArgumentException($"Unknown rank {r}")
        };
        var suit = text[^1] switch
        {
            'S' => CardSuit.Spades, 'H' => CardSuit.Hearts, 'D' => CardSuit.Diamonds, 'C' => CardSuit.Clubs,
            var s => throw new ArgumentException($"Unknown suit {s}")
        };
        return new Card(rank, suit);
    }

    /// <summary>Builds a deal from tricks given as (leader, cards in play order).</summary>
    private static AchievementContext BuildContext(
        GameMode gameMode,
        (PlayerPosition Leader, string[] Cards)[] trickSpecs,
        int trickCount = 8)
    {
        var tricks = trickSpecs.Select(spec =>
        {
            var position = spec.Leader;
            var plays = new List<PlayedCard>();
            foreach (var card in spec.Cards)
            {
                plays.Add(new PlayedCard(position, ParseCard(card)));
                position = position.Next();
            }
            return (IReadOnlyList<PlayedCard>)plays;
        }).ToList();

        return BuildContext(gameMode, tricks, trickCount);
    }

    /// <summary>Deals a shuffled deck and plays 8 tricks with random legal plays.</summary>
    private static AchievementContext BuildRandomContext(GameMode gameMode, Random random)
    {
        var deck = Deck.CreateStandard().Cards.OrderBy(_ => random.Next()).ToList();
        var hands = Enum.GetValues<PlayerPosition>()
            .ToDictionary(p => p, p => CorePlayer.Create(p, deck.Skip(8 * (int)p).Take(8)));

        var leader = (PlayerPosition)random.Next(4);
        var tricks = new List<IReadOnlyList<PlayedCard>>();
        for (var trickNumber = 1; trickNumber <= 8; trickNumber++)
        {
            var trick = TrickState.Create(leader, trickNumber);
            var position = leader;
            for (var k = 0; k < 4; k++)
            {
                var valid = PlayValidator.GetValidPlays(hands[position], trick, gameMode);
                var card = valid[random.Next(valid.Count)];
                hands[position] = hands[position].RemoveCard(card);
                trick = trick.PlayCard(card);
                position = position.Next();
            }
            tricks.Add(trick.PlayedCards);
            leader = PlayerAgentHelper.DetermineCurrentWinner(trick, gameMode).winner!.Value;
        }

        return BuildContext(gameMode, tricks, 8);
    }

    /// <summary>
    /// Builds the context from complete tricks. Winners come from the engine's comparer, and the
    /// deal is checked to be a consistent 32-card deal where every play is legal and each trick
    /// is led by the previous winner.
    /// </summary>
    private static AchievementContext BuildContext(GameMode gameMode, List<IReadOnlyList<PlayedCard>> plays, int trickCount)
    {
        var allPlays = plays.SelectMany(t => t).ToList();
        Assert.Equal(32, allPlays.Select(p => p.Card).Distinct().Count());
        var hands = Enum.GetValues<PlayerPosition>()
            .ToDictionary(p => p, p => CorePlayer.Create(p, allPlays.Where(x => x.Player == p).Select(x => x.Card)));
        Assert.All(hands.Values, h => Assert.Equal(8, h.CardCount));

        var tricks = new List<CompletedTrick>();
        for (var i = 0; i < plays.Count; i++)
        {
            if (i > 0)
                Assert.Equal(tricks[i - 1].Winner, plays[i][0].Player);

            var trick = TrickState.Create(plays[i][0].Player, i + 1);
            foreach (var play in plays[i])
            {
                Assert.Equal(trick.CurrentPlayer, play.Player);
                Assert.True(PlayValidator.IsValidPlay(hands[play.Player], play.Card, trick, gameMode),
                    $"Illegal play {play.Card} by {play.Player} in trick {i + 1}");
                hands[play.Player] = hands[play.Player].RemoveCard(play.Card);
                trick = trick.PlayCard(play.Card);
            }

            var winner = PlayerAgentHelper.DetermineCurrentWinner(trick, gameMode).winner!.Value;
            tricks.Add(new CompletedTrick(i + 1, plays[i], trick.LeadSuit!.Value, winner));
        }

        var dealResult = new DealResult
        {
            GameMode = gameMode,
            Multiplier = MultiplierState.Normal,
            AnnouncerTeam = Team.Team1,
            Team1CardPoints = 100,
            Team2CardPoints = 62,
            Team1MatchPoints = 2,
            Team2MatchPoints = 0,
            WasSweep = false,
            SweepingTeam = null
        };

        return new AchievementContext
        {
            Trigger = AchievementTrigger.DealEnd,
            DealResult = dealResult,
            DealNumber = 1,
            MatchState = MatchState.Create(PlayerPosition.Left),
            CompletedDeals = [dealResult],
            PlayerPosition = Player,
            PlayerTeam = Player.GetTeam(),
            PlayerId = Guid.NewGuid(),
            Tricks = tricks.Take(trickCount).ToList(),
            AlreadyEarnedCodes = new HashSet<string>()
        };
    }
}
