using Giretra.Core.GameModes;
using Giretra.Core.Players;

namespace Giretra.Core.Negotiation;

/// <summary>
/// Base type for all negotiation actions.
/// </summary>
public abstract record NegotiationAction(PlayerPosition Player);

/// <summary>
/// A player announces a game mode.
/// </summary>
public sealed record AnnouncementAction(PlayerPosition Player, GameMode Mode)
    : NegotiationAction(Player)
{
    public override string ToString() => $"{Player} announces {Mode}";
}

/// <summary>
/// A player accepts the current bid.
/// For NoTrumps/ColourClubs, opponents cannot accept until the mode has been explicitly doubled.
/// </summary>
public sealed record AcceptAction(PlayerPosition Player, NegotiationAction AcceptedAction)
    : NegotiationAction(Player)
{
    public override string ToString() => $"{Player} accepts {AcceptedAction}";
}

/// <summary>
/// A player doubles the opponent's bid.
/// </summary>
public sealed record DoubleAction(PlayerPosition Player, AnnouncementAction Announcement)
    : NegotiationAction(Player)
{
    public GameMode TargetMode => Announcement.Mode;
    public override string ToString() => $"{Player} doubles {TargetMode}";
}

/// <summary>
/// A player redoubles after their bid was doubled.
/// Available for all modes.
/// </summary>
public sealed record RedoubleAction(PlayerPosition Player, DoubleAction Double)
    : NegotiationAction(Player)
{
    public GameMode TargetMode => Double.TargetMode;
    public override string ToString() => $"{Player} redoubles {TargetMode}";
}

/// <summary>
/// A player re-redoubles after their opponent's bid was redoubled.
/// Only available for ColourClubs.
/// </summary>
public sealed record ReRedoubleAction(PlayerPosition Player, RedoubleAction Redouble)
    : NegotiationAction(Player)
{
    public GameMode TargetMode => Redouble.TargetMode;
    public override string ToString() => $"{Player} re-redoubles {TargetMode}";
}
