using System.Collections.Immutable;

namespace Giretra.Core.Cards;

/// <summary>
/// Represents an immutable deck of cards.
/// </summary>
public sealed class Deck
{
    private readonly ImmutableList<Card> _cards;

    /// <summary>
    /// Gets the number of cards remaining in the deck.
    /// </summary>
    public int Count => _cards.Count;

    /// <summary>
    /// Gets the cards in the deck (read-only).
    /// </summary>
    public IReadOnlyList<Card> Cards => _cards;

    private Deck(ImmutableList<Card> cards)
    {
        _cards = cards;
    }

    /// <summary>
    /// Creates a new standard 32-card deck in a specific order.
    /// Cards are ordered by suit (Clubs, Diamonds, Hearts, Spades) then by rank (7-Ace).
    /// </summary>
    public static Deck CreateStandard()
    {
        var cards = ImmutableList.CreateBuilder<Card>();

        foreach (var suit in Enum.GetValues<CardSuit>())
        {
            foreach (var rank in Enum.GetValues<CardRank>())
            {
                cards.Add(new Card(rank, suit));
            }
        }

        return new Deck(cards.ToImmutable());
    }

    /// <summary>
    /// Creates a new shuffled 32-card deck using Fisher-Yates shuffle.
    /// Uses <see cref="Random.Shared"/> for randomness.
    /// </summary>
    public static Deck CreateShuffled() => CreateShuffled(Random.Shared);

    /// <summary>
    /// Creates a new shuffled 32-card deck using Fisher-Yates shuffle
    /// with the specified random number generator.
    /// </summary>
    public static Deck CreateShuffled(Random random)
    {
        var cards = CreateStandard().Cards.ToArray();

        for (int i = cards.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }

        return FromCards(cards);
    }

    /// <summary>
    /// Creates a deck from an existing collection of cards.
    /// </summary>
    public static Deck FromCards(IEnumerable<Card> cards)
        => new(cards.ToImmutableList());

    /// <summary>
    /// Cuts the deck at the specified position.
    /// The bottom portion is placed on top of the top portion.
    /// </summary>
    /// <param name="position">Number of cards to take from one end (6-26).</param>
    /// <param name="fromTop">If true, takes from top; if false, takes from bottom.</param>
    /// <returns>A new deck with the cut applied.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If position is not between 6 and 26.</exception>
    public Deck Cut(int position, bool fromTop = true)
    {
        if (position < 6 || position > 26)
        {
            throw new ArgumentOutOfRangeException(
                nameof(position),
                position,
                "Cut position must be between 6 and 26 cards.");
        }

        if (_cards.Count != 32)
        {
            throw new InvalidOperationException(
                "Can only cut a full 32-card deck.");
        }

        int splitPoint = fromTop ? position : _cards.Count - position;

        var topPortion = _cards.Take(splitPoint);
        var bottomPortion = _cards.Skip(splitPoint);

        // Bottom portion goes on top
        return new Deck(bottomPortion.Concat(topPortion).ToImmutableList());
    }

    /// <summary>
    /// Deals the specified number of cards from the top of the deck.
    /// </summary>
    /// <param name="count">Number of cards to deal.</param>
    /// <returns>A tuple containing the dealt cards and the remaining deck.</returns>
    /// <exception cref="InvalidOperationException">If not enough cards remain.</exception>
    public (IReadOnlyList<Card> DealtCards, Deck RemainingDeck) Deal(int count)
    {
        if (count > _cards.Count)
        {
            throw new InvalidOperationException(
                $"Cannot deal {count} cards when only {_cards.Count} remain.");
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                "Cannot deal a negative number of cards.");
        }

        var dealtCards = _cards.Take(count).ToList();
        var remaining = _cards.Skip(count).ToImmutableList();

        return (dealtCards, new Deck(remaining));
    }

    /// <summary>
    /// Checks if the deck contains the specified card.
    /// </summary>
    public bool Contains(Card card) => _cards.Contains(card);

    /// <summary>
    /// Gets the card at the specified index (0 = top of deck).
    /// </summary>
    public Card this[int index] => _cards[index];
}
