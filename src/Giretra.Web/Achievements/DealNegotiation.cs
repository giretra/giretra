using Giretra.Web.Players;

namespace Giretra.Web.Achievements;

/// <summary>
/// The negotiation actions of one deal, so match-level rules can look at bidding
/// behaviour across the whole match rather than just the current deal.
/// </summary>
public sealed record DealNegotiation(int DealNumber, IReadOnlyList<RecordedAction> Actions);

internal static class RecordedDealExtensions
{
    /// <summary>
    /// The announce/accept/double actions of a deal, excluding cuts and card plays.
    /// </summary>
    internal static IReadOnlyList<RecordedAction> NegotiationActions(this RecordedDeal deal)
        => deal.Actions
            .Where(a => a.ActionType is RecordedActionType.Announce
                or RecordedActionType.Accept
                or RecordedActionType.Double
                or RecordedActionType.Redouble
                or RecordedActionType.ReRedouble)
            .ToList();
}
