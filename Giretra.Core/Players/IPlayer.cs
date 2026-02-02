using Giretra.Core.Cards;
using Giretra.Core.Negotiation;
using Giretra.Core.Scoring;
using Giretra.Core.State;

namespace Giretra.Core.Players;

/// <summary>
/// Represents a player that can participate in a full Belote match,
/// making decisions during all phases of the game.
/// </summary>
public interface IPlayer
{
    /// <summary>
    /// Gets the position this player occupies at the table.
    /// </summary>
    PlayerPosition Position { get; }

    /// <summary>
    /// Called when this player must cut the deck.
    /// </summary>
    /// <param name="deckSize">The number of cards in the deck (always 32).</param>
    /// <param name="matchState">The current state of the match.</param>
    /// <returns>
    /// A tuple containing:
    /// - position: The number of cards to cut (must be between 6 and 26 inclusive).
    /// - fromTop: True to cut from the top of the deck, false to cut from the bottom.
    /// </returns>
    Task<(int position, bool fromTop)> ChooseCutAsync(int deckSize, MatchState matchState);

    /// <summary>
    /// Called when this player must make a negotiation decision.
    /// Use <see cref="NegotiationEngine.GetValidActions"/> to get the list of valid actions.
    /// </summary>
    /// <param name="hand">The player's current hand (5 cards during initial negotiation).</param>
    /// <param name="negotiationState">The current state of the negotiation.</param>
    /// <param name="matchState">The current state of the match.</param>
    /// <returns>The negotiation action to take.</returns>
    Task<NegotiationAction> ChooseNegotiationActionAsync(
        IReadOnlyList<Card> hand,
        NegotiationState negotiationState,
        MatchState matchState);

    /// <summary>
    /// Called when this player must play a card.
    /// Use <see cref="Play.PlayValidator.GetValidPlays"/> to get the list of valid cards.
    /// </summary>
    /// <param name="hand">The player's current hand.</param>
    /// <param name="handState">The current state of the hand (tricks played, current trick).</param>
    /// <param name="matchState">The current state of the match.</param>
    /// <returns>The card to play.</returns>
    Task<Card> ChooseCardAsync(
        IReadOnlyList<Card> hand,
        HandState handState,
        MatchState matchState);

    /// <summary>
    /// Called when a deal starts, allowing the player to observe initial state.
    /// </summary>
    /// <param name="matchState">The current state of the match with the new deal.</param>
    Task OnDealStartedAsync(MatchState matchState);

    /// <summary>
    /// Called when a deal ends, allowing the player to observe the result.
    /// </summary>
    /// <param name="result">The result of the completed deal.</param>
    /// <param name="matchState">The current state of the match after the deal.</param>
    Task OnDealEndedAsync(DealResult result, MatchState matchState);

    /// <summary>
    /// Called when the match ends.
    /// </summary>
    /// <param name="matchState">The final state of the match.</param>
    Task OnMatchEndedAsync(MatchState matchState);
}
