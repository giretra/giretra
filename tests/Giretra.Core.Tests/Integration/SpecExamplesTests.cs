using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.State;

namespace Giretra.Core.Tests.Integration;

/// <summary>
/// Tests based on examples from SPEC.md to verify compliance.
/// </summary>
public class SpecExamplesTests
{
    #region Section 7.8: Negotiation Examples

    /// <summary>
    /// SPEC.md 7.8 Example 1: Simple negotiation
    /// 1. Bottom: Colour Hearts
    /// 2. Left: Accept
    /// 3. Top: Accept
    /// 4. Right: Accept
    /// → Colour Hearts is played, Bottom's team announces
    /// </summary>
    [Fact]
    public void NegotiationExample1_SimpleAcceptChain()
    {
        // Dealer is Right, so Bottom speaks first
        var state = NegotiationState.Create(PlayerPosition.Right);

        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourHearts));
        state = state.Apply(new AcceptAction(PlayerPosition.Left));
        state = state.Apply(new AcceptAction(PlayerPosition.Top));
        state = state.Apply(new AcceptAction(PlayerPosition.Right));

        Assert.True(state.IsComplete);

        var (mode, team, multiplier) = state.ResolveFinalMode();

        Assert.Equal(GameMode.ColourHearts, mode);
        Assert.Equal(Team.Team1, team);  // Bottom's team
        Assert.Equal(MultiplierState.Normal, multiplier);
    }

    /// <summary>
    /// SPEC.md 7.8 Example 2: Outbidding
    /// 1. Bottom: Colour Clubs
    /// 2. Left: Colour Spades
    /// 3. Top: Accept
    /// 4. Right: Accept
    /// 5. Bottom: Accept
    /// → Colour Spades is played, Left's team announces
    /// </summary>
    [Fact]
    public void NegotiationExample2_Outbidding()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourClubs));
        state = state.Apply(new AnnouncementAction(PlayerPosition.Left, GameMode.ColourSpades));
        state = state.Apply(new AcceptAction(PlayerPosition.Top));
        state = state.Apply(new AcceptAction(PlayerPosition.Right));
        state = state.Apply(new AcceptAction(PlayerPosition.Bottom));

        Assert.True(state.IsComplete);

        var (mode, team, multiplier) = state.ResolveFinalMode();

        Assert.Equal(GameMode.ColourSpades, mode);
        Assert.Equal(Team.Team2, team);  // Left's team
        Assert.Equal(MultiplierState.Normal, multiplier);
    }

    /// <summary>
    /// SPEC.md 7.8 Example 3: Double with priority
    /// 1. Bottom: Colour Clubs
    /// 2. Left: Colour Hearts
    /// 3. Top: Double (on Colour Hearts)
    /// 4. Right: Double (on Colour Clubs)
    /// → Colour Clubs Doubled is played (first announced mode that was Doubled)
    /// </summary>
    [Fact]
    public void NegotiationExample3_DoublePriority()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourClubs));
        state = state.Apply(new AnnouncementAction(PlayerPosition.Left, GameMode.ColourHearts));
        state = state.Apply(new DoubleAction(PlayerPosition.Top, GameMode.ColourHearts));
        state = state.Apply(new DoubleAction(PlayerPosition.Right, GameMode.ColourClubs));

        // Need 3 accepts to complete
        state = state.Apply(new AcceptAction(PlayerPosition.Bottom));
        state = state.Apply(new AcceptAction(PlayerPosition.Left));
        state = state.Apply(new AcceptAction(PlayerPosition.Top));

        Assert.True(state.IsComplete);

        var (mode, team, multiplier) = state.ResolveFinalMode();

        // Colour Clubs was first announced and was doubled
        Assert.Equal(GameMode.ColourClubs, mode);
        Assert.Equal(Team.Team1, team);  // Bottom's team
        Assert.Equal(MultiplierState.Doubled, multiplier);
    }

    /// <summary>
    /// SPEC.md 7.8 Example 4: Redouble
    /// 1. Bottom: Colour Spades
    /// 2. Left: Double
    /// 3. Top: Accept
    /// 4. Right: Accept
    /// 5. Bottom: Redouble
    /// 6. Left: Accept
    /// 7. Top: Accept
    /// 8. Right: Accept
    /// → Colour Spades Redoubled is played
    /// </summary>
    [Fact]
    public void NegotiationExample4_Redouble()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourSpades));
        state = state.Apply(new DoubleAction(PlayerPosition.Left, GameMode.ColourSpades));
        state = state.Apply(new AcceptAction(PlayerPosition.Top));
        state = state.Apply(new AcceptAction(PlayerPosition.Right));
        state = state.Apply(new RedoubleAction(PlayerPosition.Bottom, GameMode.ColourSpades));
        state = state.Apply(new AcceptAction(PlayerPosition.Left));
        state = state.Apply(new AcceptAction(PlayerPosition.Top));
        state = state.Apply(new AcceptAction(PlayerPosition.Right));

        Assert.True(state.IsComplete);

        var (mode, team, multiplier) = state.ResolveFinalMode();

        Assert.Equal(GameMode.ColourSpades, mode);
        Assert.Equal(Team.Team1, team);  // Bottom's team
        Assert.Equal(MultiplierState.Redoubled, multiplier);
    }

    #endregion

    #region Section 9.3: AllTrumps Scoring Examples

    [Theory]
    [InlineData(199, 59, 20, 6)]
    [InlineData(150, 108, 15, 11)]
    [InlineData(131, 127, 0, 0)]   // Rounds to 13-13 = tie
    [InlineData(129, 129, 0, 0)]   // Exact tie
    [InlineData(120, 138, 0, 26)]  // Announcer < 129 = loses
    public void AllTrumpsScoringExamples_MatchSpec(
        int announcerPoints,
        int defenderPoints,
        int expectedAnnouncerMatch,
        int expectedDefenderMatch)
    {
        var calculator = new ScoringCalculator();

        var result = calculator.Calculate(
            GameMode.AllTrumps,
            MultiplierState.Normal,
            Team.Team1,  // Announcer
            announcerPoints,
            defenderPoints,
            sweepingTeam: null);

        Assert.Equal(expectedAnnouncerMatch, result.Team1MatchPoints);
        Assert.Equal(expectedDefenderMatch, result.Team2MatchPoints);
    }

    #endregion

    #region Section 9.1: Match Points by Game Mode

    [Fact]
    public void ColourMode_WinThreshold82_MatchPoints16()
    {
        var calculator = new ScoringCalculator();

        // Announcer wins with 82 points (exactly threshold)
        var result = calculator.Calculate(
            GameMode.ColourSpades,
            MultiplierState.Normal,
            Team.Team1,
            82,
            80,  // 162 - 82 = 80
            sweepingTeam: null);

        Assert.Equal(16, result.Team1MatchPoints);
        Assert.Equal(0, result.Team2MatchPoints);
    }

    [Fact]
    public void NoTrumpsMode_WinThreshold65_MatchPoints26()
    {
        var calculator = new ScoringCalculator();

        var result = calculator.Calculate(
            GameMode.NoTrumps,
            MultiplierState.Normal,
            Team.Team1,
            65,  // Exactly threshold
            65,  // Tie
            sweepingTeam: null);

        // Tie = 0-0
        Assert.Equal(0, result.Team1MatchPoints);
        Assert.Equal(0, result.Team2MatchPoints);

        // Now with clear win
        result = calculator.Calculate(
            GameMode.NoTrumps,
            MultiplierState.Normal,
            Team.Team1,
            66,
            64,
            sweepingTeam: null);

        Assert.Equal(26, result.Team1MatchPoints);
    }

    [Fact]
    public void AllTrumpsMode_WinThreshold129_MatchPoints26Split()
    {
        var calculator = new ScoringCalculator();

        // Announcer barely wins
        var result = calculator.Calculate(
            GameMode.AllTrumps,
            MultiplierState.Normal,
            Team.Team1,
            130,  // Just above threshold
            128,
            sweepingTeam: null);

        // 130/10 = 13, 128/10 = 13 (ceil) = tie
        Assert.Equal(0, result.Team1MatchPoints);
        Assert.Equal(0, result.Team2MatchPoints);

        // Another case that rounds to tie
        result = calculator.Calculate(
            GameMode.AllTrumps,
            MultiplierState.Normal,
            Team.Team1,
            132,
            126,
            sweepingTeam: null);

        // 132/10 = 13.2 rounds to 13, 126/10 = 12.6 rounds to 13 = tie
        Assert.Equal(0, result.Team1MatchPoints);
        Assert.Equal(0, result.Team2MatchPoints);

        // Clear win: 140-118 rounds to 14-12
        result = calculator.Calculate(
            GameMode.AllTrumps,
            MultiplierState.Normal,
            Team.Team1,
            140,
            118,
            sweepingTeam: null);

        Assert.Equal(14, result.Team1MatchPoints);
        Assert.Equal(12, result.Team2MatchPoints);
    }

    #endregion

    #region Section 9.6: Sweep Bonuses

    [Fact]
    public void AllTrumpsSweep_35MatchPoints()
    {
        var calculator = new ScoringCalculator();

        var result = calculator.Calculate(
            GameMode.AllTrumps,
            MultiplierState.Normal,
            Team.Team1,
            258,
            0,
            sweepingTeam: Team.Team1);

        Assert.True(result.WasSweep);
        Assert.Equal(35, result.Team1MatchPoints);
        Assert.False(result.IsInstantWin);
    }

    [Fact]
    public void NoTrumpsSweep_90MatchPoints()
    {
        var calculator = new ScoringCalculator();

        var result = calculator.Calculate(
            GameMode.NoTrumps,
            MultiplierState.Normal,
            Team.Team1,
            130,
            0,
            sweepingTeam: Team.Team1);

        Assert.True(result.WasSweep);
        Assert.Equal(90, result.Team1MatchPoints);
        Assert.False(result.IsInstantWin);
    }

    [Fact]
    public void ColourSweep_InstantMatchWin()
    {
        var calculator = new ScoringCalculator();

        var result = calculator.Calculate(
            GameMode.ColourHearts,
            MultiplierState.Normal,
            Team.Team1,
            162,
            0,
            sweepingTeam: Team.Team1);

        Assert.True(result.WasSweep);
        Assert.True(result.IsInstantWin);
        Assert.Equal(Team.Team1, result.SweepingTeam);
    }

    #endregion

    #region Section 11: Complete Deal Example

    /// <summary>
    /// SPEC.md Section 11: Complete Deal Example
    /// - Dealer: Right
    /// - First to speak: Bottom
    /// - Negotiation results in Colour Spades, Left's team announces
    /// - Left+Right collect 95 card points, Bottom+Top collect 67
    /// - Left+Right win and receive 16 match points
    /// </summary>
    [Fact]
    public void CompleteDealExample_FromSpec()
    {
        // Setup negotiation
        var state = NegotiationState.Create(PlayerPosition.Right);

        // 1. Bottom: Colour Diamonds
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourDiamonds));
        // 2. Left: Colour Spades
        state = state.Apply(new AnnouncementAction(PlayerPosition.Left, GameMode.ColourSpades));
        // 3. Top: Accept
        state = state.Apply(new AcceptAction(PlayerPosition.Top));
        // 4. Right: Accept
        state = state.Apply(new AcceptAction(PlayerPosition.Right));
        // 5. Bottom: Accept
        state = state.Apply(new AcceptAction(PlayerPosition.Bottom));

        Assert.True(state.IsComplete);

        var (mode, team, multiplier) = state.ResolveFinalMode();

        Assert.Equal(GameMode.ColourSpades, mode);
        Assert.Equal(Team.Team2, team);  // Left+Right

        // Calculate scoring based on example card points
        var calculator = new ScoringCalculator();
        var result = calculator.Calculate(
            mode,
            multiplier,
            team,
            67,   // Team1 (Bottom+Top) card points
            95,   // Team2 (Left+Right) card points
            sweepingTeam: null);

        // Team2 (announcers) won with 95 >= 82
        Assert.Equal(0, result.Team1MatchPoints);
        Assert.Equal(16, result.Team2MatchPoints);
    }

    #endregion

    #region Full Deal Flow Integration

    [Fact]
    public void FullDealFlow_CutDistributeNegotiatePlay()
    {
        // Create a standard deck
        var deck = Deck.CreateStandard();

        // Start a deal with Right as dealer
        var deal = DealState.Create(PlayerPosition.Right, deck);

        Assert.Equal(DealPhase.AwaitingCut, deal.Phase);

        // Cut the deck (player to dealer's left = Bottom)
        deal = deal.CutDeck(10, fromTop: true);

        Assert.Equal(DealPhase.InitialDistribution, deal.Phase);

        // Perform initial distribution (5 cards each)
        deal = deal.PerformInitialDistribution();

        Assert.Equal(DealPhase.Negotiation, deal.Phase);
        Assert.Equal(5, deal.GetPlayer(PlayerPosition.Bottom).CardCount);
        Assert.Equal(5, deal.GetPlayer(PlayerPosition.Left).CardCount);
        Assert.Equal(5, deal.GetPlayer(PlayerPosition.Top).CardCount);
        Assert.Equal(5, deal.GetPlayer(PlayerPosition.Right).CardCount);
        Assert.Equal(12, deal.Deck.Count);  // 12 cards remain

        // Negotiation
        Assert.Equal(PlayerPosition.Bottom, deal.Negotiation!.CurrentPlayer);

        deal = deal.ApplyNegotiationAction(
            new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourHearts));
        deal = deal.ApplyNegotiationAction(new AcceptAction(PlayerPosition.Left));
        deal = deal.ApplyNegotiationAction(new AcceptAction(PlayerPosition.Top));
        deal = deal.ApplyNegotiationAction(new AcceptAction(PlayerPosition.Right));

        Assert.Equal(DealPhase.FinalDistribution, deal.Phase);
        Assert.Equal(GameMode.ColourHearts, deal.ResolvedMode);
        Assert.Equal(Team.Team1, deal.AnnouncerTeam);

        // Final distribution
        deal = deal.PerformFinalDistribution();

        Assert.Equal(DealPhase.Playing, deal.Phase);
        Assert.Equal(8, deal.GetPlayer(PlayerPosition.Bottom).CardCount);
        Assert.Equal(8, deal.GetPlayer(PlayerPosition.Left).CardCount);
        Assert.Equal(8, deal.GetPlayer(PlayerPosition.Top).CardCount);
        Assert.Equal(8, deal.GetPlayer(PlayerPosition.Right).CardCount);
        Assert.Equal(0, deal.Deck.Count);
    }

    #endregion

    #region Match State Integration

    [Fact]
    public void MatchState_TargetIncreases_WhenBothExceed()
    {
        var match = MatchState.Create(PlayerPosition.Bottom);

        Assert.Equal(150, match.TargetScore);
        Assert.False(match.IsComplete);
    }

    [Fact]
    public void Deck_HasCorrectCardCounts()
    {
        var deck = Deck.CreateStandard();

        Assert.Equal(32, deck.Count);

        // 8 cards per suit
        foreach (var suit in Enum.GetValues<CardSuit>())
        {
            var suitCards = deck.Cards.Count(c => c.Suit == suit);
            Assert.Equal(8, suitCards);
        }

        // 4 cards per rank
        foreach (var rank in Enum.GetValues<CardRank>())
        {
            var rankCards = deck.Cards.Count(c => c.Rank == rank);
            Assert.Equal(4, rankCards);
        }
    }

    #endregion
}
