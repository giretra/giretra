// Bot.cs — Pure game logic. This is the only file you need to edit.
// See BotTypes.cs for full type definitions.
//
// One Bot instance is created per game session.
// You are always the Bottom player. Your teammate sits across (Top),
// and your opponents are Left and Right.

namespace RandomDotnetBot;

public class Bot
{
    private readonly Random Rng = new();

    public string MatchId { get; }

    public Bot(string matchId)
    {
        MatchId = matchId;
    }

    /// <summary>
    /// Called when it's your turn to cut the deck before a deal.
    /// Return a position (6–26) and whether to cut from the top.
    /// </summary>
    public CutResult ChooseCut(ChooseCutContext ctx)
    {
        var position = Rng.Next(6, 27); // 6..26 inclusive
        var fromTop = Rng.Next(2) == 0;
        return new CutResult { Position = position, FromTop = fromTop };
    }

    /// <summary>
    /// Called during the negotiation (bidding) phase.
    /// Pick one action from <see cref="ChooseNegotiationActionContext.ValidActions"/>.
    /// </summary>
    public NegotiationActionChoice ChooseNegotiationAction(ChooseNegotiationActionContext ctx)
    {
        return ctx.ValidActions[Rng.Next(ctx.ValidActions.Count)];
    }

    /// <summary>
    /// Called when it's your turn to play a card.
    /// Pick one card from <see cref="ChooseCardContext.ValidPlays"/>.
    /// </summary>
    public Card ChooseCard(ChooseCardContext ctx)
    {
        return ctx.ValidPlays[Rng.Next(ctx.ValidPlays.Count)];
    }

    /// <summary>Called when a new deal begins.</summary>
    public virtual void OnDealStarted(DealStartedContext ctx) { }

    /// <summary>Called after any player (including you) plays a card.</summary>
    public virtual void OnCardPlayed(CardPlayedContext ctx) { }

    /// <summary>Called when a trick is completed, with the winner.</summary>
    public virtual void OnTrickCompleted(TrickCompletedContext ctx) { }

    /// <summary>Called when a deal ends, with scoring results.</summary>
    public virtual void OnDealEnded(DealEndedContext ctx) { }

    /// <summary>Called when the match is over.</summary>
    public virtual void OnMatchEnded(MatchEndedContext ctx) { }
}
