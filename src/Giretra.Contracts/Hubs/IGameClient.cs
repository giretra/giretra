using Giretra.Web.Models.Events;

namespace Giretra.Web.Hubs;

/// <summary>
/// Strongly-typed SignalR client contract for <see cref="GameHub"/>.
/// Method names are the wire event names — SignalR invokes clients by the
/// interface method's name verbatim, so renaming a method here renames the
/// event for every connected client.
/// </summary>
public interface IGameClient
{
    Task YourTurn(YourTurnEvent ev);
    Task PlayerTurn(PlayerTurnEvent ev);
    Task DealStarted(DealStartedEvent ev);
    Task NegotiationCompleted(NegotiationCompletedEvent ev);
    Task DealEnded(DealEndedEvent ev);
    Task CardPlayed(CardPlayedEvent ev);
    Task TrickCompleted(TrickCompletedEvent ev);
    Task MatchEnded(MatchEndedEvent ev);
    Task AchievementsEarned(AchievementsEarnedEvent ev);
    Task PlayerJoined(PlayerJoinedEvent ev);
    Task PlayerLeft(PlayerLeftEvent ev);
    Task GameStarted(GameStartedEvent ev);
    Task PlayerKicked(PlayerKickedEvent ev);
    Task SeatModeChanged(SeatModeChangedEvent ev);
    Task MatchAbandoned(MatchAbandonedEvent ev);
    Task RoomIdleClosed(RoomIdleClosedEvent ev);
    Task RoomReset(RoomResetEvent ev);
    Task RoomsChanged();
    Task PendingFriendCountChanged(PendingFriendCountChangedEvent ev);
    Task ChatMessageReceived(ChatMessageEvent ev);
    Task ChatStatusChanged(ChatStatusChangedEvent ev);
}
