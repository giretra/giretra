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

    public CutResult ChooseCut(ChooseCutContext ctx)
    {
        var position = Rng.Next(6, 27); // 6..26 inclusive
        var fromTop = Rng.Next(2) == 0;
        return new CutResult { Position = position, FromTop = fromTop };
    }

    public NegotiationActionChoice ChooseNegotiationAction(ChooseNegotiationActionContext ctx)
    {
        return ctx.ValidActions[Rng.Next(ctx.ValidActions.Count)];
    }

    public Card ChooseCard(ChooseCardContext ctx)
    {
        return ctx.ValidPlays[Rng.Next(ctx.ValidPlays.Count)];
    }

    // Optional notification hooks — fill in the ones you need:
    public virtual void OnDealStarted(DealStartedContext ctx) { }
    public virtual void OnCardPlayed(CardPlayedContext ctx) { }
    public virtual void OnTrickCompleted(TrickCompletedContext ctx) { }
    public virtual void OnDealEnded(DealEndedContext ctx) { }
    public virtual void OnMatchEnded(MatchEndedContext ctx) { }
}
