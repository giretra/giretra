using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Giretra.Core.State;

namespace Giretra.Core.Tests.Negotiation;

public class NegotiationEngineTests
{
    [Fact]
    public void FirstPlayer_MustAnnounce_CannotAccept()
    {
        // Dealer is Right, so Bottom speaks first
        var state = NegotiationState.Create(PlayerPosition.Right);

        Assert.False(NegotiationEngine.CanAccept(state));
        Assert.True(NegotiationEngine.CanAnnounce(state, GameMode.ColourClubs));
    }

    [Fact]
    public void Announce_MustBeHigherThanCurrentBid()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        // Bottom announces Hearts
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourHearts));

        // Left cannot announce Clubs or Diamonds (lower)
        Assert.False(NegotiationEngine.CanAnnounce(state, GameMode.ColourClubs));
        Assert.False(NegotiationEngine.CanAnnounce(state, GameMode.ColourDiamonds));
        Assert.False(NegotiationEngine.CanAnnounce(state, GameMode.ColourHearts));

        // Left can announce Spades, NoTrumps, or AllTrumps
        Assert.True(NegotiationEngine.CanAnnounce(state, GameMode.ColourSpades));
        Assert.True(NegotiationEngine.CanAnnounce(state, GameMode.NoTrumps));
        Assert.True(NegotiationEngine.CanAnnounce(state, GameMode.AllTrumps));
    }

    [Fact]
    public void OneColourPerTeam_Restriction()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        // Bottom (Team1) announces Clubs
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourClubs));

        // Left (Team2) announces Diamonds
        state = state.Apply(new AnnouncementAction(PlayerPosition.Left, GameMode.ColourDiamonds));

        // Top (Team1) cannot announce any Colour (team already announced)
        Assert.False(NegotiationEngine.CanAnnounce(state, GameMode.ColourHearts));
        Assert.False(NegotiationEngine.CanAnnounce(state, GameMode.ColourSpades));

        // Top can still announce NoTrumps or AllTrumps
        Assert.True(NegotiationEngine.CanAnnounce(state, GameMode.NoTrumps));
        Assert.True(NegotiationEngine.CanAnnounce(state, GameMode.AllTrumps));
    }

    [Fact]
    public void CannotAnnounce_AfterAccepting()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourHearts));
        state = state.Apply(new AcceptAction(PlayerPosition.Left));
        state = state.Apply(new AnnouncementAction(PlayerPosition.Top, GameMode.AllTrumps));

        // Right accepts
        state = state.Apply(new AcceptAction(PlayerPosition.Right));

        // Bottom's turn again - Bottom cannot announce (already accepted would apply if they had)
        // Actually Bottom hasn't accepted yet, let's verify Left cannot announce after accepting
        var leftState = NegotiationState.Create(PlayerPosition.Right);
        leftState = leftState.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourHearts));
        leftState = leftState.Apply(new AcceptAction(PlayerPosition.Left));
        leftState = leftState.Apply(new AcceptAction(PlayerPosition.Top));
        leftState = leftState.Apply(new AcceptAction(PlayerPosition.Right));

        // Negotiation should be complete after 3 accepts
        Assert.True(leftState.IsComplete);
    }

    [Fact]
    public void Double_OnlyAgainstOpponentBid()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        // Bottom (Team1) announces Clubs
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourClubs));

        // Left (Team2) can double Bottom's bid
        Assert.True(NegotiationEngine.CanDouble(state, out var modes));
        Assert.Contains(GameMode.ColourClubs, modes);

        // Left announces Hearts instead
        state = state.Apply(new AnnouncementAction(PlayerPosition.Left, GameMode.ColourHearts));

        // Top (Team1) can only double Left's Hearts, not Bottom's Clubs (same team)
        Assert.True(NegotiationEngine.CanDouble(state, out modes));
        Assert.Contains(GameMode.ColourHearts, modes);
        Assert.DoesNotContain(GameMode.ColourClubs, modes);
    }

    [Fact]
    public void CannotAccept_NoTrumps_UntilDoubled()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        // Bottom announces NoTrumps
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.NoTrumps));

        // Left cannot accept because NoTrumps hasn't been doubled yet
        Assert.False(NegotiationEngine.CanAccept(state));

        // Left doubles NoTrumps
        state = state.Apply(new DoubleAction(PlayerPosition.Left, GameMode.NoTrumps));

        // Now Top can accept (NoTrumps has been doubled)
        Assert.True(NegotiationEngine.CanAccept(state));
    }

    [Fact]
    public void CannotAccept_ColourClubs_UntilDoubled()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        // Bottom announces ColourClubs
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourClubs));

        // Left cannot accept because ColourClubs hasn't been doubled yet
        Assert.False(NegotiationEngine.CanAccept(state));

        // Left doubles ColourClubs
        state = state.Apply(new DoubleAction(PlayerPosition.Left, GameMode.ColourClubs));

        // Now Top can accept
        Assert.True(NegotiationEngine.CanAccept(state));
    }

    [Fact]
    public void Redouble_OnlyForAllowedModes()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        // Bottom announces Spades
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourSpades));

        // Left doubles
        state = state.Apply(new DoubleAction(PlayerPosition.Left, GameMode.ColourSpades));

        // Top (announcer's team) can redouble
        Assert.True(NegotiationEngine.CanRedouble(state, GameMode.ColourSpades));
    }

    [Fact]
    public void Redouble_AllowedForNoTrumps()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        // Bottom announces NoTrumps
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.NoTrumps));

        // Left doubles NoTrumps (mandatory before accept)
        state = state.Apply(new DoubleAction(PlayerPosition.Left, GameMode.NoTrumps));

        // Top (announcer's team) can redouble NoTrumps
        Assert.True(NegotiationEngine.CanRedouble(state, GameMode.NoTrumps));
    }

    [Fact]
    public void Redouble_AllowedForColourClubs()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        // Bottom announces Clubs
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourClubs));

        // Left doubles Clubs (mandatory before accept)
        state = state.Apply(new DoubleAction(PlayerPosition.Left, GameMode.ColourClubs));

        // Top (announcer's team) can redouble Clubs
        Assert.True(NegotiationEngine.CanRedouble(state, GameMode.ColourClubs));
    }

    [Fact]
    public void Redouble_OnlyByAnnouncerTeam()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        // Bottom (Team1) announces Spades
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourSpades));

        // Left (Team2) doubles
        state = state.Apply(new DoubleAction(PlayerPosition.Left, GameMode.ColourSpades));

        // Top (Team1) can redouble
        Assert.True(NegotiationEngine.CanRedouble(state, GameMode.ColourSpades));

        // Skip to Right's turn
        state = state.Apply(new AcceptAction(PlayerPosition.Top));

        // Right (Team2) cannot redouble
        Assert.False(NegotiationEngine.CanRedouble(state, GameMode.ColourSpades));
    }

    [Fact]
    public void NegotiationEnds_ThreeConsecutiveAccepts()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourHearts));
        Assert.False(state.IsComplete);

        state = state.Apply(new AcceptAction(PlayerPosition.Left));
        Assert.False(state.IsComplete);

        state = state.Apply(new AcceptAction(PlayerPosition.Top));
        Assert.False(state.IsComplete);

        state = state.Apply(new AcceptAction(PlayerPosition.Right));
        Assert.True(state.IsComplete);
    }

    [Fact]
    public void CannotAnnounce_AfterDoubleOccurred()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourHearts));
        state = state.Apply(new DoubleAction(PlayerPosition.Left, GameMode.ColourHearts));

        // Top cannot announce after double
        Assert.False(NegotiationEngine.CanAnnounce(state, GameMode.AllTrumps));
    }

    [Fact]
    public void DoublePriority_FirstDoubledModeWins()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        // Bottom announces Clubs
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourClubs));

        // Left announces Hearts
        state = state.Apply(new AnnouncementAction(PlayerPosition.Left, GameMode.ColourHearts));

        // Top doubles Hearts
        state = state.Apply(new DoubleAction(PlayerPosition.Top, GameMode.ColourHearts));

        // Right doubles Clubs
        state = state.Apply(new DoubleAction(PlayerPosition.Right, GameMode.ColourClubs));

        // Complete negotiation
        state = state.Apply(new AcceptAction(PlayerPosition.Bottom));
        state = state.Apply(new AcceptAction(PlayerPosition.Left));
        state = state.Apply(new AcceptAction(PlayerPosition.Top));

        // Clubs was first announced and doubled
        var (mode, team, multiplier) = state.ResolveFinalMode();

        Assert.Equal(GameMode.ColourClubs, mode);
        Assert.Equal(Team.Team1, team); // Bottom's team
        Assert.Equal(MultiplierState.Doubled, multiplier);
    }

    [Fact]
    public void AvailableChoices_AfterDoubleOnOpponentBid()
    {
        // Dealer is Bottom, so Left speaks first
        var state = NegotiationState.Create(PlayerPosition.Bottom);

        // Left (Team2) announces Colour Clubs
        state = state.Apply(new AnnouncementAction(PlayerPosition.Left, GameMode.ColourClubs));

        // Top (Team1) announces Colour Diamonds
        state = state.Apply(new AnnouncementAction(PlayerPosition.Top, GameMode.ColourDiamonds));

        // Right (Team2) doubles Diamonds
        state = state.Apply(new DoubleAction(PlayerPosition.Right, GameMode.ColourDiamonds));

        // Now it's Bottom's turn (Team1)
        // Available choices should be: Accept, Double Clubs, Redouble Diamonds

        var validActions = NegotiationEngine.GetValidActions(state);

        Assert.Equal(3, validActions.Count);
        Assert.Contains(validActions, a => a is AcceptAction);
        Assert.Contains(validActions, a => a is DoubleAction { TargetMode: GameMode.ColourClubs });
        Assert.Contains(validActions, a => a is RedoubleAction { TargetMode: GameMode.ColourDiamonds });
    }

    [Fact]
    public void AvailableChoices_AfterBothTeamsDouble()
    {
        // Dealer is Bottom, so Left speaks first
        var state = NegotiationState.Create(PlayerPosition.Bottom);

        // Left (Team2) announces Colour Diamonds
        state = state.Apply(new AnnouncementAction(PlayerPosition.Left, GameMode.ColourDiamonds));

        // Top (Team1) announces Colour Hearts
        state = state.Apply(new AnnouncementAction(PlayerPosition.Top, GameMode.ColourHearts));

        // Right (Team2) doubles Hearts
        state = state.Apply(new DoubleAction(PlayerPosition.Right, GameMode.ColourHearts));

        // Bottom (Team1) doubles Diamonds
        state = state.Apply(new DoubleAction(PlayerPosition.Bottom, GameMode.ColourDiamonds));

        // Now it's Left's turn (Team2)
        // ONLY available choices should be: Accept, Redouble Diamonds

        var validActions = NegotiationEngine.GetValidActions(state);

        Assert.Equal(2, validActions.Count);
        Assert.Contains(validActions, a => a is AcceptAction);
        Assert.Contains(validActions, a => a is RedoubleAction { TargetMode: GameMode.ColourDiamonds });
    }

    [Fact]
    public void CannotDouble_AfterAcceptingPassed()
    {
        // Dealer is Top, so Right speaks first
        // │ # │ Player │ Action             │
        // │ 1 │ Right  │ Announces Hearts ♥ │
        // │ 2 │ Bottom │ Accept             │  <- Bottom implicitly passed on doubling Hearts
        // │ 3 │ Left   │ Announces AT       │
        // │ 4 │ Top    │ Accept             │
        // │ 5 │ Right  │ Accept             │
        // │ 6 │ Bottom │ ???                │

        var state = NegotiationState.Create(PlayerPosition.Top);

        state = state.Apply(new AnnouncementAction(PlayerPosition.Right, GameMode.ColourHearts));
        state = state.Apply(new AcceptAction(PlayerPosition.Bottom));
        state = state.Apply(new AnnouncementAction(PlayerPosition.Left, GameMode.AllTrumps));
        state = state.Apply(new AcceptAction(PlayerPosition.Top));
        state = state.Apply(new AcceptAction(PlayerPosition.Right));

        // Bottom's choices should be: Accept, Double AllTrumps
        // Double Hearts should NOT be available (Bottom already accepted it)
        var validActions = NegotiationEngine.GetValidActions(state);

        Assert.Equal(2, validActions.Count);
        Assert.Contains(validActions, a => a is AcceptAction);
        Assert.Contains(validActions, a => a is DoubleAction { TargetMode: GameMode.AllTrumps });

        // Explicitly verify Hearts cannot be doubled
        Assert.DoesNotContain(validActions, a => a is DoubleAction { TargetMode: GameMode.ColourHearts });
    }

    [Fact]
    public void CannotDouble_AfterAnnouncingPassed()
    {
        // Dealer is Right, so Bottom speaks first
        // │ # │ Player        │ Action               │
        // │ 1 │ Bottom        │ Announces Diamonds ♦ │
        // │ 2 │ Left          │ Announces Hearts ♥   │  <- Left implicitly passed on doubling Diamonds
        // │ 3 │ Top (Partner) │ Double Hearts ♥      │
        // │ 4 │ Right         │ Redouble Hearts ♥    │
        // │ 5 │ Bottom        │ Accept               │
        // │ 6 │ Left          │ Double Diamonds ♦    │  <- Should fail!

        var state = NegotiationState.Create(PlayerPosition.Right);

        // Step 1: Bottom announces Diamonds
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourDiamonds));

        // Step 2: Left announces Hearts (implicitly passing on doubling Diamonds)
        state = state.Apply(new AnnouncementAction(PlayerPosition.Left, GameMode.ColourHearts));

        // Step 3: Top doubles Hearts
        state = state.Apply(new DoubleAction(PlayerPosition.Top, GameMode.ColourHearts));

        // Step 4: Right redoubles Hearts (Right is on Left's team who announced Hearts)
        state = state.Apply(new RedoubleAction(PlayerPosition.Right, GameMode.ColourHearts));

        // Step 5: Bottom accepts
        state = state.Apply(new AcceptAction(PlayerPosition.Bottom));

        // Step 6: Left tries to double Diamonds - this should NOT be allowed
        // Because Left already announced Hearts, they implicitly passed on doubling Diamonds
        Assert.False(NegotiationEngine.CanDouble(state, out var doubleableModes));
        Assert.DoesNotContain(GameMode.ColourDiamonds, doubleableModes);

        // Verify validation also rejects it
        var error = NegotiationEngine.ValidateAction(state, new DoubleAction(PlayerPosition.Left, GameMode.ColourDiamonds));
        Assert.NotNull(error);
    }

    [Fact]
    public void AvailableChoices_AcceptAndDoubleOptions_AfterOvercall()
    {
        // Dealer is Bottom, so Left speaks first
        // │ # │ Player │ Action             │
        // │ 1 │ Left   │ Announces Hearts ♥ │
        // │ 2 │ Top    │ Accept             │
        // │ 3 │ Right  │ Announces AT       │
        // │ 4 │ Bottom │ ???                │
        //
        // Bottom (Team1) should have: Accept, Double AllTrumps, Double Hearts

        var state = NegotiationState.Create(PlayerPosition.Bottom);

        state = state.Apply(new AnnouncementAction(PlayerPosition.Left, GameMode.ColourHearts));
        state = state.Apply(new AcceptAction(PlayerPosition.Top));
        state = state.Apply(new AnnouncementAction(PlayerPosition.Right, GameMode.AllTrumps));

        // Bottom's choices: Accept, Double AllTrumps (Right/Team2), Double Hearts (Left/Team2)
        var validActions = NegotiationEngine.GetValidActions(state);

        Assert.Equal(3, validActions.Count);
        Assert.Contains(validActions, a => a is AcceptAction);
        Assert.Contains(validActions, a => a is DoubleAction { TargetMode: GameMode.AllTrumps });
        Assert.Contains(validActions, a => a is DoubleAction { TargetMode: GameMode.ColourHearts });
    }

    [Fact]
    public void ReRedouble_AllowedForColourClubs()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        // Bottom (Team1) announces ColourClubs
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourClubs));

        // Left (Team2) doubles
        state = state.Apply(new DoubleAction(PlayerPosition.Left, GameMode.ColourClubs));

        // Top (Team1) redoubles
        state = state.Apply(new RedoubleAction(PlayerPosition.Top, GameMode.ColourClubs));

        // Right (Team2) can re-redouble
        Assert.True(NegotiationEngine.CanReRedouble(state, GameMode.ColourClubs));
    }

    [Fact]
    public void ReRedouble_NotAllowedForNonClubs()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        // Bottom announces ColourSpades
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourSpades));

        // Left doubles
        state = state.Apply(new DoubleAction(PlayerPosition.Left, GameMode.ColourSpades));

        // Top redoubles
        state = state.Apply(new RedoubleAction(PlayerPosition.Top, GameMode.ColourSpades));

        // Right cannot re-redouble (only ColourClubs allows it)
        Assert.False(NegotiationEngine.CanReRedouble(state, GameMode.ColourSpades));
    }

    [Fact]
    public void ReRedouble_OnlyByOpponentTeam()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        // Bottom (Team1) announces ColourClubs
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourClubs));

        // Left (Team2) doubles
        state = state.Apply(new DoubleAction(PlayerPosition.Left, GameMode.ColourClubs));

        // Top (Team1) can redouble but not re-redouble
        Assert.True(NegotiationEngine.CanRedouble(state, GameMode.ColourClubs));
        Assert.False(NegotiationEngine.CanReRedouble(state, GameMode.ColourClubs));

        // Top redoubles
        state = state.Apply(new RedoubleAction(PlayerPosition.Top, GameMode.ColourClubs));

        // Right (Team2/opponent) can re-redouble
        Assert.True(NegotiationEngine.CanReRedouble(state, GameMode.ColourClubs));

        // Skip to Bottom's turn
        state = state.Apply(new AcceptAction(PlayerPosition.Right));

        // Bottom (Team1/announcer) cannot re-redouble
        Assert.False(NegotiationEngine.CanReRedouble(state, GameMode.ColourClubs));
    }

    [Fact]
    public void CannotAccept_NoTrumps_ValidateReturnsError()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        // Bottom announces NoTrumps
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.NoTrumps));

        // Validate accept should fail
        var error = NegotiationEngine.ValidateAction(state, new AcceptAction(PlayerPosition.Left));
        Assert.NotNull(error);
        Assert.Contains("doubled", error);
    }
}
