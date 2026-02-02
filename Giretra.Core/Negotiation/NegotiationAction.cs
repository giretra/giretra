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
/// For SansAs/ColourClubs, an opponent's accept counts as a Double.
/// </summary>
public sealed record AcceptAction(PlayerPosition Player)
    : NegotiationAction(Player)
{
    public override string ToString() => $"{Player} accepts";
}

/// <summary>
/// A player doubles the opponent's bid.
/// </summary>
public sealed record DoubleAction(PlayerPosition Player, GameMode TargetMode)
    : NegotiationAction(Player)
{
    public override string ToString() => $"{Player} doubles {TargetMode}";
}

/// <summary>
/// A player redoubles after their bid was doubled.
/// Only available for ToutAs and Colour (except Clubs).
/// </summary>
public sealed record RedoubleAction(PlayerPosition Player, GameMode TargetMode)
    : NegotiationAction(Player)
{
    public override string ToString() => $"{Player} redoubles {TargetMode}";
}
