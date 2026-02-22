namespace Giretra.Core.State;

/// <summary>
/// Represents the current phase of a deal.
/// </summary>
public enum DealPhase
{
    /// <summary>
    /// Waiting for the player to dealer's left to cut the deck.
    /// </summary>
    AwaitingCut,

    /// <summary>
    /// Dealer is distributing initial 5 cards (3+2) to each player.
    /// </summary>
    InitialDistribution,

    /// <summary>
    /// Players are negotiating the game mode.
    /// </summary>
    Negotiation,

    /// <summary>
    /// Dealer is distributing final 3 cards to each player.
    /// </summary>
    FinalDistribution,

    /// <summary>
    /// Players are playing the 8 tricks.
    /// </summary>
    Playing,

    /// <summary>
    /// The deal is complete and scored.
    /// </summary>
    Completed
}
