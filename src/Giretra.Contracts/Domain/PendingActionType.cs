namespace Giretra.Web.Domain;

/// <summary>
/// The type of action the game is waiting for from a player.
/// </summary>
public enum PendingActionType
{
    /// <summary>
    /// Waiting for deck cut decision.
    /// </summary>
    Cut,

    /// <summary>
    /// Waiting for negotiation action.
    /// </summary>
    Negotiate,

    /// <summary>
    /// Waiting for card play.
    /// </summary>
    PlayCard,

    /// <summary>
    /// Waiting for confirmation to continue to the next deal.
    /// </summary>
    ContinueDeal,

    /// <summary>
    /// Waiting for confirmation to continue after match ends.
    /// </summary>
    ContinueMatch
}
