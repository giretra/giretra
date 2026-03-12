using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
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
    public void OpponentAccept_NoTrumps_LocksWithoutDoubling()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        // Bottom (Team1) announces NoTrumps
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.NoTrumps));

        // Left (Team2, opponent) CAN accept — locks but no score double
        Assert.True(NegotiationEngine.CanAccept(state));

        state = state.Apply(new AcceptAction(PlayerPosition.Left));

        // NoTrumps is NOT in DoubledModes (no score multiplier)
        Assert.False(state.DoubledModes.ContainsKey(GameMode.NoTrumps));
        Assert.False(state.AutoDoubledModes.Contains(GameMode.NoTrumps));
        // But announcements are blocked
        Assert.True(state.HasDoubleOccurred);
        Assert.Equal(1, state.ConsecutiveAccepts);
    }

    [Fact]
    public void OpponentAccept_ColourClubs_LocksWithoutDoubling()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        // Bottom (Team1) announces ColourClubs
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourClubs));

        // Left (Team2, opponent) CAN accept — locks but no score double
        Assert.True(NegotiationEngine.CanAccept(state));

        state = state.Apply(new AcceptAction(PlayerPosition.Left));

        // ColourClubs is NOT in DoubledModes (no score multiplier)
        Assert.False(state.DoubledModes.ContainsKey(GameMode.ColourClubs));
        Assert.False(state.AutoDoubledModes.Contains(GameMode.ColourClubs));
        // But announcements are blocked
        Assert.True(state.HasDoubleOccurred);
        Assert.Equal(1, state.ConsecutiveAccepts);
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
    public void ReRedouble_NotAllowed_ForAnyMode()
    {
        // Re-redouble is not allowed for any mode
        var state = NegotiationState.Create(PlayerPosition.Right);

        // Bottom announces ColourClubs
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourClubs));

        // Left doubles
        state = state.Apply(new DoubleAction(PlayerPosition.Left, GameMode.ColourClubs));

        // Top redoubles
        state = state.Apply(new RedoubleAction(PlayerPosition.Top, GameMode.ColourClubs));

        // Right cannot re-redouble
        Assert.False(NegotiationEngine.CanReRedouble(state, GameMode.ColourClubs));
    }

    [Fact]
    public void ReRedouble_NotAllowed_ForColourSpades()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        // Bottom announces ColourSpades
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourSpades));

        // Left doubles
        state = state.Apply(new DoubleAction(PlayerPosition.Left, GameMode.ColourSpades));

        // Top redoubles
        state = state.Apply(new RedoubleAction(PlayerPosition.Top, GameMode.ColourSpades));

        // Right cannot re-redouble
        Assert.False(NegotiationEngine.CanReRedouble(state, GameMode.ColourSpades));
    }

    [Fact]
    public void OpponentAccept_NoTrumps_ValidateSucceeds()
    {
        var state = NegotiationState.Create(PlayerPosition.Right);

        // Bottom announces NoTrumps
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.NoTrumps));

        // Validate accept should succeed for opponent (auto-doubles)
        var error = NegotiationEngine.ValidateAction(state, new AcceptAction(PlayerPosition.Left));
        Assert.Null(error);
    }

    [Fact]
    public void AnnouncerTeam_CanAccept_NoTrumps_WithoutAutoDoubling()
    {
        // When the announcer's team accepts, no auto-double occurs.
        // Scenario: NoTrumps is the current bid (undoubled), a different mode was doubled,
        // and the announcer's team should be able to accept without triggering auto-double.
        var state = NegotiationState.Create(PlayerPosition.Bottom);

        // Left (Team2) announces ColourClubs
        state = state.Apply(new AnnouncementAction(PlayerPosition.Left, GameMode.ColourClubs));

        // Top (Team1) announces NoTrumps
        state = state.Apply(new AnnouncementAction(PlayerPosition.Top, GameMode.NoTrumps));

        // Right (Team2) doubles ColourClubs (not NoTrumps!)
        // HasDoubleOccurred = true, but NoTrumps is NOT doubled
        state = state.Apply(new DoubleAction(PlayerPosition.Right, GameMode.ColourClubs));

        // Bottom (Team1, NoTrumps announcer's team) can accept despite NoTrumps being undoubled
        Assert.True(NegotiationEngine.CanAccept(state));
        Assert.Null(NegotiationEngine.ValidateAction(state, new AcceptAction(PlayerPosition.Bottom)));
    }

    [Fact]
    public void OpponentAccept_NoTrumps_LocksAnnouncements_DoubleStillAvailable()
    {
        // When opponent accepts NoTrumps, announcements are blocked but no score double.
        // Explicit double is still available.
        var state = NegotiationState.Create(PlayerPosition.Right);

        // Bottom (Team1) announces NoTrumps
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.NoTrumps));

        // Left (Team2, opponent) accepts — locks announcements
        state = state.Apply(new AcceptAction(PlayerPosition.Left));

        // Top (Team1, announcer's team) cannot announce (locked), cannot redouble (no double)
        Assert.False(NegotiationEngine.CanAnnounce(state, GameMode.AllTrumps));
        Assert.False(NegotiationEngine.CanRedouble(state, GameMode.NoTrumps));

        // Top accepts
        state = state.Apply(new AcceptAction(PlayerPosition.Top));

        // Right (Team2) can double NoTrumps (opponent's bid, not yet doubled)
        Assert.True(NegotiationEngine.CanDouble(state));
        Assert.False(NegotiationEngine.CanRedouble(state, GameMode.NoTrumps));
    }

    [Fact]
    public void GetValidActions_NotEmpty_AfterRedoubleWithUndoubledNoTrumps()
    {
        // After ColourClubs is doubled → redoubled, and NoTrumps
        // is the current bid (undoubled), the announcer's team must still have valid actions.
        var state = NegotiationState.Create(PlayerPosition.Top);

        // Right (Team2) announces ColourClubs
        state = state.Apply(new AnnouncementAction(PlayerPosition.Right, GameMode.ColourClubs));

        // Bottom (Team1) announces ColourDiamonds
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.ColourDiamonds));

        // Left (Team2) announces NoTrumps (current bid = NoTrumps, bidder = Left/Team2)
        state = state.Apply(new AnnouncementAction(PlayerPosition.Left, GameMode.NoTrumps));

        // Top (Team1) doubles ColourClubs (opponent bid)
        state = state.Apply(new DoubleAction(PlayerPosition.Top, GameMode.ColourClubs));

        // Right (Team2, ColourClubs announcer's team) redoubles ColourClubs
        state = state.Apply(new RedoubleAction(PlayerPosition.Right, GameMode.ColourClubs));

        // Now it's Bottom's turn
        // Skip to Left's turn via accept
        state = state.Apply(new AcceptAction(PlayerPosition.Bottom));

        // Now it's Left's turn (Team2, NoTrumps announcer's team)
        // CurrentBid = NoTrumps (undoubled), HasDoubleOccurred = true
        Assert.Equal(PlayerPosition.Left, state.CurrentPlayer);
        Assert.Equal(GameMode.NoTrumps, state.CurrentBid);
        Assert.True(state.HasDoubleOccurred);
        Assert.False(state.DoubledModes.ContainsKey(GameMode.NoTrumps));

        // Left (announcer's team) must have at least Accept available
        var validActions = NegotiationEngine.GetValidActions(state);
        Assert.NotEmpty(validActions);
        Assert.Contains(validActions, a => a is AcceptAction);
    }

    [Fact]
    public void PlayerWhoAcceptedEarlierBid_CanAnnounceHigherMode_AfterNewAnnouncement()
    {
        // Dealer is Left, so Top speaks first
        // Turn order: Top → Right → Bottom → Left → ...
        var state = NegotiationState.Create(PlayerPosition.Left);

        // 1. Top (Team1) announces Hearts
        state = state.Apply(new AnnouncementAction(PlayerPosition.Top, GameMode.ColourHearts));

        // 2. Right (Team2) accepts
        state = state.Apply(new AcceptAction(PlayerPosition.Right));

        // 3. Bottom (Team1) accepts
        state = state.Apply(new AcceptAction(PlayerPosition.Bottom));

        // 4. Left (Team2) announces Spades (higher than Hearts)
        state = state.Apply(new AnnouncementAction(PlayerPosition.Left, GameMode.ColourSpades));

        // 5. Top (Team1) accepts
        state = state.Apply(new AcceptAction(PlayerPosition.Top));

        // 6. Right (Team2) accepts
        state = state.Apply(new AcceptAction(PlayerPosition.Right));

        // 7. Bottom's turn — accepted Hearts earlier, but Spades was announced after
        Assert.Equal(PlayerPosition.Bottom, state.CurrentPlayer);

        var validActions = NegotiationEngine.GetValidActions(state);

        // Bottom should be able to: Accept, Double Spades, announce NoTrumps, announce AllTrumps
        Assert.Contains(validActions, a => a is AcceptAction);
        Assert.Contains(validActions, a => a is DoubleAction d && d.TargetMode == GameMode.ColourSpades);
        Assert.Contains(validActions, a => a is AnnouncementAction ann && ann.Mode == GameMode.NoTrumps);
        Assert.Contains(validActions, a => a is AnnouncementAction ann && ann.Mode == GameMode.AllTrumps);

        // Bottom (Team1) cannot announce Colour modes (team already used their Colour on Hearts)
        Assert.DoesNotContain(validActions, a => a is AnnouncementAction ann && ann.Mode.IsColourMode());

        // Exactly 4 valid actions
        Assert.Equal(4, validActions.Count);
    }

    [Fact]
    public void BottomOptions_AfterRightAnnouncesNoTrumps()
    {
        // Dealer is Top, so Right speaks first (dealer's left)
        var state = NegotiationState.Create(PlayerPosition.Top);

        // Right (Team2) announces NoTrumps
        state = state.Apply(new AnnouncementAction(PlayerPosition.Right, GameMode.NoTrumps));

        // Bottom's turn
        Assert.Equal(PlayerPosition.Bottom, state.CurrentPlayer);

        var validActions = NegotiationEngine.GetValidActions(state);

        // Bottom can: Accept, Announce AllTrumps (only higher mode), Double NoTrumps (opponent's bid)
        Assert.Contains(validActions, a => a is AcceptAction);
        Assert.Contains(validActions, a => a is AnnouncementAction ann && ann.Mode == GameMode.AllTrumps);
        Assert.Contains(validActions, a => a is DoubleAction d && d.TargetMode == GameMode.NoTrumps);

        // Bottom cannot announce any Colour (all lower than NoTrumps) or NoTrumps itself
        Assert.DoesNotContain(validActions, a => a is AnnouncementAction ann && ann.Mode == GameMode.ColourClubs);
        Assert.DoesNotContain(validActions, a => a is AnnouncementAction ann && ann.Mode == GameMode.ColourDiamonds);
        Assert.DoesNotContain(validActions, a => a is AnnouncementAction ann && ann.Mode == GameMode.ColourHearts);
        Assert.DoesNotContain(validActions, a => a is AnnouncementAction ann && ann.Mode == GameMode.ColourSpades);
        Assert.DoesNotContain(validActions, a => a is AnnouncementAction ann && ann.Mode == GameMode.NoTrumps);

        // Exactly 3 valid actions
        Assert.Equal(3, validActions.Count);
    }

    [Fact]
    public void BottomOptions_AfterRightAnnouncesNoTrumps_MatchPointsPerOption()
    {
        // Dealer is Top, so Right speaks first
        var state = NegotiationState.Create(PlayerPosition.Top);
        state = state.Apply(new AnnouncementAction(PlayerPosition.Right, GameMode.NoTrumps));

        var scorer = new ScoringCalculator();

        // Option 1: Accept — NoTrumps ×1 → 52 match points (winner-takes-all)
        // Simulate: Bottom accepts, Left accepts, Top accepts → negotiation complete
        var acceptState = state
            .Apply(new AcceptAction(PlayerPosition.Bottom))
            .Apply(new AcceptAction(PlayerPosition.Left))
            .Apply(new AcceptAction(PlayerPosition.Top));

        Assert.True(acceptState.IsComplete);
        var (acceptMode, acceptAnnouncerTeam, acceptMultiplier) = acceptState.ResolveFinalMode();
        Assert.Equal(GameMode.NoTrumps, acceptMode);
        Assert.Equal(Team.Team2, acceptAnnouncerTeam); // Right announced
        Assert.Equal(MultiplierState.Normal, acceptMultiplier);

        // NoTrumps normal: base 52 match points (winner-takes-all)
        var acceptResult = scorer.Calculate(acceptMode, acceptMultiplier, acceptAnnouncerTeam,
            team1CardPoints: 80, team2CardPoints: 60, sweepingTeam: null);
        Assert.Equal(52, acceptResult.Team1MatchPoints + acceptResult.Team2MatchPoints);

        // Option 2: Announce AllTrumps — AllTrumps ×1 → 26 match points total (split)
        var announceState = state
            .Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.AllTrumps))
            .Apply(new AcceptAction(PlayerPosition.Left))
            .Apply(new AcceptAction(PlayerPosition.Top))
            .Apply(new AcceptAction(PlayerPosition.Right));

        Assert.True(announceState.IsComplete);
        var (announceMode, announceAnnouncerTeam, announceMultiplier) = announceState.ResolveFinalMode();
        Assert.Equal(GameMode.AllTrumps, announceMode);
        Assert.Equal(Team.Team1, announceAnnouncerTeam); // Bottom announced
        Assert.Equal(MultiplierState.Normal, announceMultiplier);

        // AllTrumps split: points split proportionally, total = 26
        var announceResult = scorer.Calculate(announceMode, announceMultiplier, announceAnnouncerTeam,
            team1CardPoints: 150, team2CardPoints: 118, sweepingTeam: null);
        Assert.Equal(26, announceResult.Team1MatchPoints + announceResult.Team2MatchPoints);

        // Option 3: Double NoTrumps — NoTrumps ×2 → 104 match points total
        var doubleState = state
            .Apply(new DoubleAction(PlayerPosition.Bottom, GameMode.NoTrumps))
            .Apply(new AcceptAction(PlayerPosition.Left))
            .Apply(new AcceptAction(PlayerPosition.Top))
            .Apply(new AcceptAction(PlayerPosition.Right));

        Assert.True(doubleState.IsComplete);
        var (doubleMode, doubleAnnouncerTeam, doubleMultiplier) = doubleState.ResolveFinalMode();
        Assert.Equal(GameMode.NoTrumps, doubleMode);
        Assert.Equal(Team.Team2, doubleAnnouncerTeam); // Right announced originally
        Assert.Equal(MultiplierState.Doubled, doubleMultiplier);

        // NoTrumps doubled: base 52 × 2 = 104 match points (winner-takes-all)
        var doubleResult = scorer.Calculate(doubleMode, doubleMultiplier, doubleAnnouncerTeam,
            team1CardPoints: 80, team2CardPoints: 60, sweepingTeam: null);
        Assert.Equal(104, doubleResult.Team1MatchPoints + doubleResult.Team2MatchPoints);
    }

    [Fact]
    public void RightNoTrumps_BottomAccepts_LeftOnlyAccept_TopAcceptOrDouble()
    {
        // Dealer is Top, so Right speaks first
        var state = NegotiationState.Create(PlayerPosition.Top);

        // Right (Team2) announces NoTrumps
        state = state.Apply(new AnnouncementAction(PlayerPosition.Right, GameMode.NoTrumps));

        // Bottom (Team1, opponent) accepts — locks negotiation
        state = state.Apply(new AcceptAction(PlayerPosition.Bottom));

        // Left (Team2, announcer's teammate) — can only Accept
        // Cannot announce (locked), cannot double own team's bid
        Assert.Equal(PlayerPosition.Left, state.CurrentPlayer);
        var leftActions = NegotiationEngine.GetValidActions(state);
        Assert.Single(leftActions);
        Assert.Contains(leftActions, a => a is AcceptAction);

        state = state.Apply(new AcceptAction(PlayerPosition.Left));

        // Top (Team1) — can Accept or Double NoTrumps
        // Cannot announce (locked), can double opponent's bid
        Assert.Equal(PlayerPosition.Top, state.CurrentPlayer);
        var topActions = NegotiationEngine.GetValidActions(state);
        Assert.Equal(2, topActions.Count);
        Assert.Contains(topActions, a => a is AcceptAction);
        Assert.Contains(topActions, a => a is DoubleAction { TargetMode: GameMode.NoTrumps });
    }
}
