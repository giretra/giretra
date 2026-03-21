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
        var announceNT = new AnnouncementAction(PlayerPosition.Right, GameMode.NoTrumps);
        state = state.Apply(announceNT);

        // Bottom (Team1, opponent) CAN accept
        Assert.Equal(PlayerPosition.Bottom, state.CurrentPlayer);
        Assert.True(NegotiationEngine.CanAccept(state));

        var validActions = NegotiationEngine.GetValidActions(state);
        Assert.Contains(validActions, a => a is AcceptAction);
        Assert.Contains(validActions, a => a is AnnouncementAction { Mode: GameMode.AllTrumps });
        Assert.Contains(validActions, a => a is DoubleAction { TargetMode: GameMode.NoTrumps });

        state = state.Apply(new AcceptAction(PlayerPosition.Bottom, announceNT));

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
        var announceNT = new AnnouncementAction(PlayerPosition.Right, GameMode.NoTrumps);
        state = state.Apply(announceNT);

        // 2. Bottom (Team1) accepts — locks negotiation (blocks announcements)
        state = state.Apply(new AcceptAction(PlayerPosition.Bottom, announceNT));

        // 3. Left (Team2, announcer's teammate) — can only Accept (announcements blocked, can't double own team)
        Assert.Equal(PlayerPosition.Left, state.CurrentPlayer);
        Assert.False(NegotiationEngine.CanRedouble(state, GameMode.NoTrumps));

        var validActions = NegotiationEngine.GetValidActions(state);
        Assert.Single(validActions);
        Assert.Contains(validActions, a => a is AcceptAction);

        state = state.Apply(new AcceptAction(PlayerPosition.Left, announceNT));

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
        var announceNT = new AnnouncementAction(PlayerPosition.Right, GameMode.NoTrumps);
        state = state.Apply(announceNT);

        // 2. Bottom (Team1) accepts — no auto-double
        state = state.Apply(new AcceptAction(PlayerPosition.Bottom, announceNT));
        Assert.False(state.IsComplete);

        // 3. Left (Team2) accepts
        state = state.Apply(new AcceptAction(PlayerPosition.Left, announceNT));
        Assert.False(state.IsComplete);

        // 4. Top (Team1) accepts → 3 consecutive accepts, negotiation ends
        state = state.Apply(new AcceptAction(PlayerPosition.Top, announceNT));
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
        var announceAT = new AnnouncementAction(PlayerPosition.Bottom, GameMode.AllTrumps);
        state = state.Apply(announceAT);

        // Left (Team2) accepts — same team as NoTrumps announcer, no auto-double on AllTrumps
        // (AllTrumps doesn't auto-double anyway since it's not NoTrumps/ColourClubs)
        state = state.Apply(new AcceptAction(PlayerPosition.Left, announceAT));
        Assert.False(state.DoubledModes.ContainsKey(GameMode.AllTrumps));
        Assert.Empty(state.AutoDoubledModes);
    }

    [Fact]
    public void OpponentAccept_ColourClubs_LocksWithoutDoubling()
    {
        // Dealer = Top → Right speaks first
        var state = NegotiationState.Create(PlayerPosition.Top);

        // 1. Right (Team2) announces ColourClubs
        var announceClubs = new AnnouncementAction(PlayerPosition.Right, GameMode.ColourClubs);
        state = state.Apply(announceClubs);

        // 2. Bottom (Team1, opponent) accepts → locks but no score double
        state = state.Apply(new AcceptAction(PlayerPosition.Bottom, announceClubs));
        Assert.False(state.DoubledModes.ContainsKey(GameMode.ColourClubs));
        Assert.False(state.AutoDoubledModes.Contains(GameMode.ColourClubs));
        Assert.True(state.HasDoubleOccurred); // Locked — no more announcements

        // 3. Left (Team2, announcer's teammate) — can only Accept
        Assert.Equal(PlayerPosition.Left, state.CurrentPlayer);
        var leftActions = NegotiationEngine.GetValidActions(state);
        Assert.Single(leftActions);
        Assert.Contains(leftActions, a => a is AcceptAction);

        state = state.Apply(new AcceptAction(PlayerPosition.Left, announceClubs));

        // 4. Top (Team1, opponent) — can Accept or Double ColourClubs
        Assert.Equal(PlayerPosition.Top, state.CurrentPlayer);
        var topActions = NegotiationEngine.GetValidActions(state);
        Assert.Equal(2, topActions.Count);
        Assert.Contains(topActions, a => a is AcceptAction);
        Assert.Contains(topActions, a => a is DoubleAction { TargetMode: GameMode.ColourClubs });
    }

    [Fact]
    public void ColourClubs_FullFlow_ThreeAccepts_NormalMultiplier()
    {
        // Dealer = Top → Right speaks first
        var state = NegotiationState.Create(PlayerPosition.Top);

        // 1. Right (Team2) announces ColourClubs
        var announceClubs = new AnnouncementAction(PlayerPosition.Right, GameMode.ColourClubs);
        state = state.Apply(announceClubs);

        // 2. Bottom (Team1) accepts — locks but no auto-double
        state = state.Apply(new AcceptAction(PlayerPosition.Bottom, announceClubs));
        Assert.False(state.IsComplete);

        // 3. Left (Team2) accepts
        state = state.Apply(new AcceptAction(PlayerPosition.Left, announceClubs));
        Assert.False(state.IsComplete);

        // 4. Top (Team1) accepts → 3 consecutive accepts, negotiation ends
        state = state.Apply(new AcceptAction(PlayerPosition.Top, announceClubs));
        Assert.True(state.IsComplete);

        // Resolve: ColourClubs, announced by Team2, normal (no auto-double)
        var (mode, team, multiplier) = state.ResolveFinalMode();
        Assert.Equal(GameMode.ColourClubs, mode);
        Assert.Equal(Team.Team2, team);
        Assert.Equal(MultiplierState.Normal, multiplier);
    }

    [Fact]
    public void ColourClubs_ExplicitDouble_NormalRedoubleChain()
    {
        // When ColourClubs is explicitly doubled, normal redouble chain applies
        var state = NegotiationState.Create(PlayerPosition.Top);

        // 1. Right (Team2) announces ColourClubs
        var annClubs = new AnnouncementAction(PlayerPosition.Right, GameMode.ColourClubs);
        state = state.Apply(annClubs);

        // 2. Bottom (Team1) explicitly doubles ColourClubs
        var dblClubs = new DoubleAction(PlayerPosition.Bottom, annClubs);
        state = state.Apply(dblClubs);
        Assert.False(state.AutoDoubledModes.Contains(GameMode.ColourClubs));

        // 3. Left (Team2, announcer's team) CAN redouble (normal chain)
        Assert.True(NegotiationEngine.CanRedouble(state, GameMode.ColourClubs));

        // But no re-redouble allowed for any mode
        state = state.Apply(new RedoubleAction(PlayerPosition.Left, dblClubs));
        Assert.False(NegotiationEngine.CanReRedouble(state, GameMode.ColourClubs));
    }

    [Fact]
    public void ExplicitDouble_NoTrumps_RedoubleNotAllowed()
    {
        // When NoTrumps is explicitly doubled, redouble is still not allowed
        var state = NegotiationState.Create(PlayerPosition.Top);

        // 1. Right (Team2) announces NoTrumps
        var annNT = new AnnouncementAction(PlayerPosition.Right, GameMode.NoTrumps);
        state = state.Apply(annNT);

        // 2. Bottom (Team1) explicitly doubles NoTrumps
        state = state.Apply(new DoubleAction(PlayerPosition.Bottom, annNT));
        Assert.False(state.AutoDoubledModes.Contains(GameMode.NoTrumps));

        // 3. Left (Team2, announcer's team) cannot redouble NoTrumps
        Assert.False(NegotiationEngine.CanRedouble(state, GameMode.NoTrumps));
    }

    [Fact]
    public void MultiMode_ExplicitDoubles_Coexist()
    {
        // Multiple modes can be explicitly doubled
        var state = NegotiationState.Create(PlayerPosition.Top);

        // Right (Team2) announces ColourClubs
        state = state.Apply(new AnnouncementAction(PlayerPosition.Right, GameMode.ColourClubs));

        // Bottom (Team1) announces NoTrumps (overcalls)
        state = state.Apply(new AnnouncementAction(PlayerPosition.Bottom, GameMode.NoTrumps));

        // Left (Team2) doubles NoTrumps (explicit)
        var annNT2 = state.Actions.OfType<AnnouncementAction>().First(a => a.Mode == GameMode.NoTrumps);
        var doubleNT = new DoubleAction(PlayerPosition.Left, annNT2);
        state = state.Apply(doubleNT);

        // Top (Team1) accepts
        state = state.Apply(new AcceptAction(PlayerPosition.Top, doubleNT));

        // NoTrumps is explicitly doubled, ColourClubs is undoubled
        Assert.True(state.DoubledModes.ContainsKey(GameMode.NoTrumps));
        Assert.False(state.AutoDoubledModes.Contains(GameMode.NoTrumps));
        Assert.False(state.DoubledModes.ContainsKey(GameMode.ColourClubs));
    }
}
