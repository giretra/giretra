namespace Giretra.Web.Models.Events;

/// <summary>
/// Classifies how a card play relates to the current trick state.
/// </summary>
public enum CardPlayType
{
    /// <summary>
    /// Standard card play (lead card, or card that takes the lead).
    /// </summary>
    Normal,

    /// <summary>
    /// The played card is weaker than the current trick winner.
    /// </summary>
    Under,

    /// <summary>
    /// The played card is a master card and all remaining cards in the player's hand are also master.
    /// </summary>
    Master
}
