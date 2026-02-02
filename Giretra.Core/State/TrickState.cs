using System.Collections.Immutable;
using Giretra.Core.Cards;
using Giretra.Core.Play;
using Giretra.Core.Players;

namespace Giretra.Core.State;

/// <summary>
/// Represents the immutable state of a single trick (4 cards played).
/// </summary>
public sealed class TrickState
{
    /// <summary>
    /// Gets the position of the player who led this trick.
    /// </summary>
    public PlayerPosition Leader { get; }

    /// <summary>
    /// Gets the cards played in this trick (in play order).
    /// </summary>
    public ImmutableList<PlayedCard> PlayedCards { get; }

    /// <summary>
    /// Gets the position of the player whose turn it is to play.
    /// Null if the trick is complete.
    /// </summary>
    public PlayerPosition? CurrentPlayer { get; }

    /// <summary>
    /// Gets the trick number (1-8).
    /// </summary>
    public int TrickNumber { get; }

    /// <summary>
    /// Gets whether this trick is complete (4 cards played).
    /// </summary>
    public bool IsComplete => PlayedCards.Count == 4;

    /// <summary>
    /// Gets the lead card (first card played), or null if no cards played.
    /// </summary>
    public Card? LeadCard => PlayedCards.Count > 0 ? PlayedCards[0].Card : null;

    /// <summary>
    /// Gets the lead suit, or null if no cards played.
    /// </summary>
    public CardSuit? LeadSuit => LeadCard?.Suit;

    private TrickState(
        PlayerPosition leader,
        ImmutableList<PlayedCard> playedCards,
        PlayerPosition? currentPlayer,
        int trickNumber)
    {
        Leader = leader;
        PlayedCards = playedCards;
        CurrentPlayer = currentPlayer;
        TrickNumber = trickNumber;
    }

    /// <summary>
    /// Creates a new trick with the specified leader.
    /// </summary>
    public static TrickState Create(PlayerPosition leader, int trickNumber)
        => new(leader, ImmutableList<PlayedCard>.Empty, leader, trickNumber);

    /// <summary>
    /// Returns a new trick state with the card played by the current player.
    /// </summary>
    public TrickState PlayCard(Card card)
    {
        if (IsComplete)
        {
            throw new InvalidOperationException("Trick is already complete.");
        }

        if (CurrentPlayer is null)
        {
            throw new InvalidOperationException("No current player.");
        }

        var playedCard = new PlayedCard(CurrentPlayer.Value, card);
        var newCards = PlayedCards.Add(playedCard);

        var nextPlayer = newCards.Count < 4
            ? CurrentPlayer.Value.Next()
            : (PlayerPosition?)null;

        return new TrickState(Leader, newCards, nextPlayer, TrickNumber);
    }

    /// <summary>
    /// Checks if a trump card has been played in this trick.
    /// </summary>
    public bool HasTrumpBeenPlayed(CardSuit trumpSuit)
        => PlayedCards.Any(pc => pc.Card.Suit == trumpSuit);

    /// <summary>
    /// Gets the highest trump card played so far, or null if none.
    /// </summary>
    public PlayedCard? GetHighestTrump(CardSuit trumpSuit, GameModes.GameMode gameMode)
    {
        var trumpCards = PlayedCards.Where(pc => pc.Card.Suit == trumpSuit).ToList();
        if (trumpCards.Count == 0) return null;

        return trumpCards.MaxBy(pc => pc.Card.GetStrength(gameMode));
    }
}
