// Bot.java — Pure game logic. This is the only file you need to edit.
// See BotTypes.java for full type definitions.
//
// One Bot instance is created per game session.
// You are always the Bottom player. Your teammate sits across (Top),
// and your opponents are Left and Right.

package randomjavabot;

import java.util.concurrent.ThreadLocalRandom;

public class Bot {

    private final String matchId;

    public Bot(String matchId) {
        this.matchId = matchId;
    }

    /**
     * Called when it's your turn to cut the deck before a deal.
     * Return a position (6–26) and whether to cut from the top.
     */
    public CutResult chooseCut(ChooseCutContext ctx) {
        int position = ThreadLocalRandom.current().nextInt(6, 27); // 6..26 inclusive
        boolean fromTop = ThreadLocalRandom.current().nextBoolean();
        return new CutResult(position, fromTop);
    }

    /**
     * Called during the negotiation (bidding) phase.
     * Pick one action from {@code ctx.validActions()}.
     */
    public NegotiationActionChoice chooseNegotiationAction(ChooseNegotiationActionContext ctx) {
        return ctx.validActions().get(ThreadLocalRandom.current().nextInt(ctx.validActions().size()));
    }

    /**
     * Called when it's your turn to play a card.
     * Pick one card from {@code ctx.validPlays()}.
     */
    public Card chooseCard(ChooseCardContext ctx) {
        return ctx.validPlays().get(ThreadLocalRandom.current().nextInt(ctx.validPlays().size()));
    }

    /** Called when a new deal begins. */
    public void onDealStarted(DealStartedContext ctx) {}

    /** Called after any player (including you) plays a card. */
    public void onCardPlayed(CardPlayedContext ctx) {}

    /** Called when a trick is completed, with the winner. */
    public void onTrickCompleted(TrickCompletedContext ctx) {}

    /** Called when a deal ends, with scoring results. */
    public void onDealEnded(DealEndedContext ctx) {}

    /** Called when the match is over. */
    public void onMatchEnded(MatchEndedContext ctx) {}
}
