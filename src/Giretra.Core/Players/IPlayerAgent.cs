using Giretra.Core.Cards;
using Giretra.Core.Negotiation;
using Giretra.Core.Scoring;
using Giretra.Core.State;

namespace Giretra.Core.Players;

/// <summary>
/// Represents a player that can participate in a full Belote match,
/// making decisions during all phases of the game.
/// </summary>
public interface IPlayerAgent
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
    /// </summary>
    /// <param name="hand">The player's current hand (5 cards during initial negotiation).</param>
    /// <param name="negotiationState">The current state of the negotiation.</param>
    /// <param name="matchState">The current state of the match.</param>
    /// <param name="validActions">The list of valid negotiation actions the player can take.</param>
    /// <returns>The negotiation action to take (must be one of the valid actions).</returns>
    Task<NegotiationAction> ChooseNegotiationActionAsync(
        IReadOnlyList<Card> hand,
        NegotiationState negotiationState,
        MatchState matchState,
        IReadOnlyList<NegotiationAction> validActions);

    /// <summary>
    /// Called when this player must play a card.
    /// </summary>
    /// <param name="hand">The player's current hand.</param>
    /// <param name="handState">The current state of the hand (tricks played, current trick).</param>
    /// <param name="matchState">The current state of the match.</param>
    /// <param name="validPlays">The list of valid cards the player can play.</param>
    /// <returns>The card to play (must be one of the valid plays).</returns>
    Task<Card> ChooseCardAsync(
        IReadOnlyList<Card> hand,
        HandState handState,
        MatchState matchState,
        IReadOnlyList<Card> validPlays);

    /// <summary>
    /// Called when a deal starts, allowing the player to observe initial state.
    /// </summary>
    /// <param name="matchState">The current state of the match with the new deal.</param>
    Task OnDealStartedAsync(MatchState matchState);

    /// <summary>
    /// Called when a deal ends, allowing the player to observe the result.
    /// </summary>
    /// <param name="result">The result of the completed deal.</param>
    /// <param name="handState">The final state of the hand with all completed tricks.</param>
    /// <param name="matchState">The current state of the match after the deal.</param>
    Task OnDealEndedAsync(DealResult result, HandState handState, MatchState matchState);

    /// <summary>
    /// Called when any player plays a card, allowing observation of the play.
    /// </summary>
    /// <param name="player">The player who played the card.</param>
    /// <param name="card">The card that was played.</param>
    /// <param name="handState">The current state of the hand after the card was played.</param>
    /// <param name="matchState">The current state of the match.</param>
    Task OnCardPlayedAsync(PlayerPosition player, Card card, HandState handState, MatchState matchState);

    /// <summary>
    /// Called when a trick is completed, allowing observation before the next trick starts.
    /// </summary>
    /// <param name="completedTrick">The trick that was just completed.</param>
    /// <param name="winner">The player who won the trick.</param>
    /// <param name="handState">The current state of the hand after the trick.</param>
    /// <param name="matchState">The current state of the match.</param>
    Task OnTrickCompletedAsync(TrickState completedTrick, PlayerPosition winner, HandState handState, MatchState matchState);

    /// <summary>
    /// Called when the match ends.
    /// </summary>
    /// <param name="matchState">The final state of the match.</param>
    Task OnMatchEndedAsync(MatchState matchState);

    /// <summary>
    /// Called after a deal ends to confirm the player is ready to continue to the next deal.
    /// This is used to wait for user confirmation before starting the next deal.
    /// </summary>
    /// <param name="matchState">The current state of the match after the deal.</param>
    Task ConfirmContinueDealAsync(MatchState matchState);

    /// <summary>
    /// Called after a match ends to confirm the player is ready to continue.
    /// This is used to wait for user confirmation before returning from the match.
    /// </summary>
    /// <param name="matchState">The final state of the match.</param>
    Task ConfirmContinueMatchAsync(MatchState matchState);
}
