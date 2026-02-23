// Bot.cs — Pure game logic. This is the only file you need to edit.
// See BotTypes.cs for full type definitions.

namespace RandomDotnetBot;

public static class Bot
{
    private static readonly Random Rng = new();

    public static CutResult ChooseCut(ChooseCutContext ctx)
    {
        var position = Rng.Next(6, 27); // 6..26 inclusive
        var fromTop = Rng.Next(2) == 0;
        return new CutResult { Position = position, FromTop = fromTop };
    }

    public static NegotiationActionChoice ChooseNegotiationAction(ChooseNegotiationActionContext ctx)
    {
        return ctx.ValidActions[Rng.Next(ctx.ValidActions.Count)];
    }

    public static Card ChooseCard(ChooseCardContext ctx)
    {
        return ctx.ValidPlays[Rng.Next(ctx.ValidPlays.Count)];
    }

    // Optional notification hooks — fill in the ones you need:
    public static void OnDealStarted(DealStartedContext ctx) { }
    public static void OnCardPlayed(CardPlayedContext ctx) { }
    public static void OnTrickCompleted(TrickCompletedContext ctx) { }
    public static void OnDealEnded(DealEndedContext ctx) { }
    public static void OnMatchEnded(MatchEndedContext ctx) { }
}
