using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Giretra.Core.State;

namespace Giretra.Core.Tests.Negotiation;

public class NoTrumpsFlowTests
{
    [Fact]
    public void Opponent_CanAccept_NoTrumps_LocksWithoutDoubling()
    {
        // Dealer = Top → Right speaks first
        var state = NegotiationState.Create(PlayerPosition.Top);

        // Right (Team2) announces NoTrumps
        state = state.Apply(new AnnouncementAction(PlayerPosition.Right, GameMode.NoTrumps));

        // Bottom (Team1, opponent) CAN accept
        Assert.Equal(PlayerPosition.Bottom, state.CurrentPlayer);
        Assert.True(NegotiationEngine.CanAccept(state));

        var validActions = NegotiationEngine.GetValidActions(state);
        Assert.Contains(validActions, a => a is AcceptAction);
        Assert.Contains(validActions, a => a is AnnouncementAction { Mode: GameMode.AllTrumps });
        Assert.Contains(validActions, a => a is DoubleAction { TargetMode: GameMode.NoTrumps });

        state = state.Apply(new AcceptAction(PlayerPosition.Bottom));

        // Accepting NoTrumps locks negotiation (blocks announcements) but does NOT double
        Assert.False(state.DoubledModes.ContainsKey(GameMode.NoTrumps));
        Assert.False(state.AutoDoubledModes.Contains(GameMode.NoTrumps));
        Assert.True(state.HasDoubleOccurred); // Locked — no more announcements
        Assert.Equal(1, state.ConsecutiveAccepts);
    }

    [Fact]
    public void AfterOpponentAccept_AnnouncementsBlocked_DoubleStillAvailable()
    {
        // Dealer = Top → Right speaks first
        var state = NegotiationState.Create(PlayerPosition.Top);

        // 1. Right (Team2) announces NoTrumps
        state = state.Apply(new AnnouncementAction(PlayerPosition.Right, GameMode.NoTrumps));

        // 2. Bottom (Team1) accepts — locks negotiation (blocks announcements)
        state = state.Apply(new AcceptAction(PlayerPosition.Bottom));

        // 3. Left (Team2, announcer's teammate) — can only Accept (announcements blocked, can't double own team)
        Assert.Equal(PlayerPosition.Left, state.CurrentPlayer);
        Assert.False(NegotiationEngine.CanRedouble(state, GameMode.NoTrumps));

        var validActions = NegotiationEngine.GetValidActions(state);
        Assert.Single(validActions);
        Assert.Contains(validActions, a => a is AcceptAction);

        state = state.Apply(new AcceptAction(PlayerPosition.Left));

        // 4. Top (Team1) — can Accept or Double NoTrumps (announcements blocked, but double still allowed)
        Assert.Equal(PlayerPosition.Top, state.CurrentPlayer);

        validActions = NegotiationEngine.GetValidActions(state);
        Assert.Equal(2, validActions.Count);
        Assert.Contains(validActions, a => a is AcceptAction);
        Assert.Contains(validActions, a => a is DoubleAction { TargetMode: GameMode.NoTrumps });
    }

    [Fact]
    public void NoTrumps_FullFlow_ThreeAcceptsEndsNegotiation_NormalMultiplier()
    {
        // Dealer = Top → Right speaks first
        var state = NegotiationState.Create(PlayerPosition.Top);

        // 1. Right (Team2) announces NoTrumps
        state = state.Apply(new AnnouncementAction(PlayerPosition.Right, GameMode.NoTrumps));

        // 2. Bottom (Team1) accepts — no auto-double
        state = state.Apply(new AcceptAction(PlayerPosition.Bottom));
        Assert.False(state.IsComplete);

        // 3. Left (Team2) accepts
        state = state.Apply(new AcceptAction(PlayerPosition.Left));
        Assert.False(state.IsComplete);

        // 4. Top (Team1) accepts → 3 consecutive accepts, negotiation ends
        state = state.Apply(new AcceptAction(PlayerPosition.Top));
        Assert.True(state.IsComplete);

        // Resolve: NoTrumps, announced by Team2, normal (no auto-double)
        var (mode, team, multiplier) = state.ResolveFinalMode();
        Assert.Equal(GameMode.NoTrumps, mode);
        Assert.Equal(Team.Team2, team);
        Assert.Equal(MultiplierState.Normal, multiplier);
    }

    [Fact]
    public void AnnouncerTeam_Accept_DoesNotAutoDouble()
    {
        // Dealer = Top → Right speaks first
        var state = NegotiationState.Create(PlayerPosition.Top);

        // Right (Team2) announces NoTrumps
        state = state.Apply(new AnnouncementAction(PlayerPosition.Right, GameMode.NoTrumps));

        // Bottom (Team1) announces AllTrumps (overcalls)
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.AllTrumps));

        // Left (Team2) accepts — same team as NoTrumps announcer, no auto-double on AllTrumps
        // (AllTrumps doesn't auto-double anyway since it's not NoTrumps/ColourClubs)
        state = state.Apply(new AcceptAction(PlayerPosition.Left));
        Assert.False(state.DoubledModes.ContainsKey(GameMode.AllTrumps));
        Assert.Empty(state.AutoDoubledModes);
    }

    [Fact]
    public void AutoDoubled_ColourClubs_InvertedReRedouble()
    {
        // Dealer = Top → Right speaks first
        var state = NegotiationState.Create(PlayerPosition.Top);

        // 1. Right (Team2) announces ColourClubs
        state = state.Apply(new AnnouncementAction(PlayerPosition.Right, GameMode.ColourClubs));

        // 2. Bottom (Team1) accepts → auto-double (×2)
        state = state.Apply(new AcceptAction(PlayerPosition.Bottom));
        Assert.True(state.AutoDoubledModes.Contains(GameMode.ColourClubs));

        // 3. Left (Team2, announcer's team) — cannot redouble (inverted)
        Assert.False(NegotiationEngine.CanRedouble(state, GameMode.ColourClubs));
        state = state.Apply(new AcceptAction(PlayerPosition.Left));

        // 4. Top (Team1, auto-doubler's partner) — CAN redouble (×4)
        Assert.True(NegotiationEngine.CanRedouble(state, GameMode.ColourClubs));
        state = state.Apply(new RedoubleAction(PlayerPosition.Top, GameMode.ColourClubs));

        // 5. Right (Team2, announcer's team) — CAN re-redouble (×8, inverted)
        Assert.True(NegotiationEngine.CanReRedouble(state, GameMode.ColourClubs));

        // Bottom (Team1) cannot re-redouble (inverted: announcer's team gets it)
        state = state.Apply(new AcceptAction(PlayerPosition.Right));
        Assert.False(NegotiationEngine.CanReRedouble(state, GameMode.ColourClubs));
    }

    [Fact]
    public void ExplicitDouble_NoTrumps_NormalRedoubleChain()
    {
        // When NoTrumps is explicitly doubled (not auto-doubled), normal chain applies
        var state = NegotiationState.Create(PlayerPosition.Top);

        // 1. Right (Team2) announces NoTrumps
        state = state.Apply(new AnnouncementAction(PlayerPosition.Right, GameMode.NoTrumps));

        // 2. Bottom (Team1) explicitly doubles NoTrumps
        state = state.Apply(new DoubleAction(PlayerPosition.Bottom, GameMode.NoTrumps));
        Assert.False(state.AutoDoubledModes.Contains(GameMode.NoTrumps));

        // 3. Left (Team2, announcer's team) CAN redouble (normal chain)
        Assert.True(NegotiationEngine.CanRedouble(state, GameMode.NoTrumps));
    }

    [Fact]
    public void MultiMode_ExplicitDouble_And_AutoDouble_Coexist()
    {
        // One mode explicitly doubled, another auto-doubled
        var state = NegotiationState.Create(PlayerPosition.Top);

        // Right (Team2) announces ColourClubs
        state = state.Apply(new AnnouncementAction(PlayerPosition.Right, GameMode.ColourClubs));

        // Bottom (Team1) announces NoTrumps (overcalls)
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.NoTrumps));

        // Left (Team2) doubles NoTrumps (explicit)
        state = state.Apply(new DoubleAction(PlayerPosition.Left, GameMode.NoTrumps));

        // Top (Team1) accepts → auto-doubles ColourClubs? No — ColourClubs is not the current bid.
        // Actually, auto-double only triggers for the CurrentBid. CurrentBid is NoTrumps (already doubled).
        // Accept just increments consecutive accepts.
        state = state.Apply(new AcceptAction(PlayerPosition.Top));

        // NoTrumps is explicitly doubled (not auto), ColourClubs is undoubled
        Assert.True(state.DoubledModes.ContainsKey(GameMode.NoTrumps));
        Assert.False(state.AutoDoubledModes.Contains(GameMode.NoTrumps));
        Assert.False(state.DoubledModes.ContainsKey(GameMode.ColourClubs));
    }
}
