using Giretra.Core.Cards;
using Giretra.Core.Players;

namespace Giretra.Core.Play;

/// <summary>
/// Represents a card played by a specific player in a trick.
/// </summary>
public readonly record struct PlayedCard(PlayerPosition Player, Card Card)
{
    /// <summary>
    /// Gets the team of the player who played this card.
    /// </summary>
    public Team Team => Player.GetTeam();

    public override string ToString()
        => $"{Player}: {Card}";
}
