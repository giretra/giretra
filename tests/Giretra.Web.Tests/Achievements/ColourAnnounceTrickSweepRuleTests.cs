using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Play;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.State;
using Giretra.Web.Achievements;
using Giretra.Web.Achievements.Rules;
using Giretra.Web.Players;

namespace Giretra.Web.Tests.Achievements;

public sealed class ColourAnnounceTrickSweepRuleTests
{
    private readonly ColourAnnounceTrickSweepRule _rule = new();

    private const PlayerPosition Player = PlayerPosition.Bottom;
    private const PlayerPosition Opponent = PlayerPosition.Left;

    [Fact]
    public async Task Earned_WhenFiveTricksWonInAnotherSuitInAllTrumps()
    {
        var context = BuildContext(
            finalMode: GameMode.AllTrumps,
            actions: [(Player, RecordedActionType.Announce, GameMode.ColourHearts)],
            playerWonSuits: [CardSuit.Spades, CardSuit.Spades, CardSuit.Spades, CardSuit.Spades, CardSuit.Spades]);

        Assert.True(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task Earned_InNoTrumps()
    {
        var context = BuildContext(
            finalMode: GameMode.NoTrumps,
            actions: [(Player, RecordedActionType.Announce, GameMode.ColourSpades)],
            playerWonSuits: [CardSuit.Hearts, CardSuit.Hearts, CardSuit.Hearts, CardSuit.Hearts, CardSuit.Hearts]);

        Assert.True(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task Earned_WhenThePlayerThemselfEscalatedToAllTrumps()
    {
        var context = BuildContext(
            finalMode: GameMode.AllTrumps,
            actions:
            [
                (Player, RecordedActionType.Announce, GameMode.ColourHearts),
                (Opponent, RecordedActionType.Announce, GameMode.NoTrumps),
                (Player, RecordedActionType.Announce, GameMode.AllTrumps)
            ],
            playerWonSuits: [CardSuit.Clubs, CardSuit.Clubs, CardSuit.Clubs, CardSuit.Clubs, CardSuit.Clubs]);

        Assert.True(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task Earned_WhenSixTricksWonAndFiveShareANonAnnouncedSuit()
    {
        var context = BuildContext(
            finalMode: GameMode.AllTrumps,
            actions: [(Player, RecordedActionType.Announce, GameMode.ColourClubs)],
            playerWonSuits:
            [
                CardSuit.Spades, CardSuit.Spades, CardSuit.Spades,
                CardSuit.Clubs, CardSuit.Spades, CardSuit.Spades
            ]);

        Assert.True(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenFiveTricksWonWithTheAnnouncedSuit()
    {
        var context = BuildContext(
            finalMode: GameMode.AllTrumps,
            actions: [(Player, RecordedActionType.Announce, GameMode.ColourHearts)],
            playerWonSuits: [CardSuit.Hearts, CardSuit.Hearts, CardSuit.Hearts, CardSuit.Hearts, CardSuit.Hearts]);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenFiveTricksSpreadOverMixedNonAnnouncedSuits()
    {
        var context = BuildContext(
            finalMode: GameMode.AllTrumps,
            actions: [(Player, RecordedActionType.Announce, GameMode.ColourHearts)],
            playerWonSuits:
            [
                CardSuit.Spades, CardSuit.Spades, CardSuit.Spades,
                CardSuit.Clubs, CardSuit.Clubs
            ]);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenDealEndedInAColourMode()
    {
        var context = BuildContext(
            finalMode: GameMode.ColourHearts,
            actions: [(Player, RecordedActionType.Announce, GameMode.ColourHearts)],
            playerWonSuits: [CardSuit.Spades, CardSuit.Spades, CardSuit.Spades, CardSuit.Spades, CardSuit.Spades]);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenPlayerNeverAnnouncedAColour()
    {
        var context = BuildContext(
            finalMode: GameMode.AllTrumps,
            actions:
            [
                (Opponent, RecordedActionType.Announce, GameMode.ColourHearts),
                (Player, RecordedActionType.Accept, null)
            ],
            playerWonSuits: [CardSuit.Spades, CardSuit.Spades, CardSuit.Spades, CardSuit.Spades, CardSuit.Spades]);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenPlayerOnlyDoubledAColour()
    {
        var context = BuildContext(
            finalMode: GameMode.AllTrumps,
            actions:
            [
                (Opponent, RecordedActionType.Announce, GameMode.ColourHearts),
                (Player, RecordedActionType.Double, GameMode.ColourHearts)
            ],
            playerWonSuits: [CardSuit.Spades, CardSuit.Spades, CardSuit.Spades, CardSuit.Spades, CardSuit.Spades]);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenOnlyFourTricksWonInTheOtherSuit()
    {
        var context = BuildContext(
            finalMode: GameMode.AllTrumps,
            actions: [(Player, RecordedActionType.Announce, GameMode.ColourHearts)],
            playerWonSuits:
            [
                CardSuit.Spades, CardSuit.Spades, CardSuit.Spades, CardSuit.Spades,
                CardSuit.Hearts, CardSuit.Clubs
            ]);

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    [Fact]
    public async Task NotEarned_WhenThereIsNoDealResult()
    {
        var context = BuildContext(
            finalMode: GameMode.AllTrumps,
            actions: [(Player, RecordedActionType.Announce, GameMode.ColourHearts)],
            playerWonSuits: [CardSuit.Spades, CardSuit.Spades, CardSuit.Spades, CardSuit.Spades, CardSuit.Spades])
            with
        { DealResult = null };

        Assert.False(await _rule.IsEarnedAsync(context));
    }

    /// <summary>
    /// Builds a deal where the player wins one trick per entry of
    /// <paramref name="playerWonSuits"/> (playing a card of that suit), and the
    /// opponent wins the remaining tricks up to 8.
    /// </summary>
    private static AchievementContext BuildContext(
        GameMode finalMode,
        (PlayerPosition Position, RecordedActionType ActionType, GameMode? Mode)[] actions,
        CardSuit[] playerWonSuits)
    {
        var dealResult = new DealResult
        {
            GameMode = finalMode,
            Multiplier = MultiplierState.Normal,
            AnnouncerTeam = Team.Team1,
            Team1CardPoints = 100,
            Team2CardPoints = 62,
            Team1MatchPoints = 2,
            Team2MatchPoints = 0
        };

        var tricks = new List<CompletedTrick>();
        foreach (var suit in playerWonSuits)
        {
            tricks.Add(new CompletedTrick(
                tricks.Count + 1,
                [
                    new PlayedCard(Player, new Card(CardRank.Ace, suit)),
                    new PlayedCard(PlayerPosition.Left, new Card(CardRank.Seven, suit)),
                    new PlayedCard(PlayerPosition.Top, new Card(CardRank.Eight, suit)),
                    new PlayedCard(PlayerPosition.Right, new Card(CardRank.Nine, suit))
                ],
                suit,
                Player));
        }

        while (tricks.Count < 8)
        {
            tricks.Add(new CompletedTrick(
                tricks.Count + 1,
                [
                    new PlayedCard(Player, new Card(CardRank.Seven, CardSuit.Diamonds)),
                    new PlayedCard(PlayerPosition.Left, new Card(CardRank.Ace, CardSuit.Diamonds)),
                    new PlayedCard(PlayerPosition.Top, new Card(CardRank.Eight, CardSuit.Diamonds)),
                    new PlayedCard(PlayerPosition.Right, new Card(CardRank.Nine, CardSuit.Diamonds))
                ],
                CardSuit.Diamonds,
                Opponent));
        }

        var negotiationActions = actions
            .Select((a, i) => new RecordedAction
            {
                ActionOrder = i,
                ActionType = a.ActionType,
                PlayerPosition = a.Position,
                GameMode = a.Mode
            })
            .ToList();

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
            NegotiationActions = negotiationActions,
            Tricks = tricks,
            AlreadyEarnedCodes = new HashSet<string>()
        };
    }
}
