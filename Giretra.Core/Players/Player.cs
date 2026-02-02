using System.Collections.Immutable;
using Giretra.Core.Cards;

namespace Giretra.Core.Players;

/// <summary>
/// Represents an immutable player with their current hand.
/// </summary>
public sealed class Player
{
    /// <summary>
    /// Gets the player's position at the table.
    /// </summary>
    public PlayerPosition Position { get; }

    /// <summary>
    /// Gets the player's team.
    /// </summary>
    public Team Team => Position.GetTeam();

    /// <summary>
    /// Gets the cards currently in the player's hand.
    /// </summary>
    public ImmutableList<Card> Hand { get; }

    /// <summary>
    /// Gets the number of cards in hand.
    /// </summary>
    public int CardCount => Hand.Count;

    private Player(PlayerPosition position, ImmutableList<Card> hand)
    {
        Position = position;
        Hand = hand;
    }

    /// <summary>
    /// Creates a new player with an empty hand.
    /// </summary>
    public static Player Create(PlayerPosition position)
        => new(position, ImmutableList<Card>.Empty);

    /// <summary>
    /// Creates a new player with the specified hand.
    /// </summary>
    public static Player Create(PlayerPosition position, IEnumerable<Card> hand)
        => new(position, hand.ToImmutableList());

    /// <summary>
    /// Returns a new player with the specified cards added to hand.
    /// </summary>
    public Player AddCards(IEnumerable<Card> cards)
        => new(Position, Hand.AddRange(cards));

    /// <summary>
    /// Returns a new player with a single card added to hand.
    /// </summary>
    public Player AddCard(Card card)
        => new(Position, Hand.Add(card));

    /// <summary>
    /// Returns a new player with the specified card removed from hand.
    /// </summary>
    /// <exception cref="InvalidOperationException">If the card is not in hand.</exception>
    public Player RemoveCard(Card card)
    {
        var index = Hand.IndexOf(card);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Card {card} is not in {Position}'s hand.");
        }

        return new Player(Position, Hand.RemoveAt(index));
    }

    /// <summary>
    /// Checks if the player has any cards of the specified suit.
    /// </summary>
    public bool HasSuit(CardSuit suit)
        => Hand.Any(c => c.Suit == suit);

    /// <summary>
    /// Gets all cards of the specified suit in hand.
    /// </summary>
    public IEnumerable<Card> GetCardsOfSuit(CardSuit suit)
        => Hand.Where(c => c.Suit == suit);

    /// <summary>
    /// Checks if the player holds the specified card.
    /// </summary>
    public bool HasCard(Card card)
        => Hand.Contains(card);

    /// <summary>
    /// Returns a new player with an empty hand.
    /// </summary>
    public Player ClearHand()
        => new(Position, ImmutableList<Card>.Empty);

    public override string ToString()
        => $"{Position} ({CardCount} cards)";
}
