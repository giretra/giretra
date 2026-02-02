using Giretra.Core.Cards;
using Giretra.Core.Negotiation;
using Giretra.Core.Play;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.State;
using Giretra.UI;

namespace Giretra.Players;

/// <summary>
/// An IPlayer implementation for a human player using the console.
/// Uses Spectre.Console for rendering and prompts.
/// </summary>
public sealed class HumanConsolePlayer : IPlayer
{
    public PlayerPosition Position => PlayerPosition.Bottom;

    public Task<(int position, bool fromTop)> ChooseCutAsync(int deckSize, MatchState matchState)
    {
        GameRenderer.RenderDealStart(matchState);

        // Only the player to the right of dealer cuts
        var deal = matchState.CurrentDeal!;
        var cutter = deal.Dealer.Previous();

        if (cutter != Position)
        {
            // Not our turn to cut, AI will handle it
            return Task.FromResult((16, true));
        }

        var result = Prompts.SelectCutPosition();
        return Task.FromResult(result);
    }

    public Task<NegotiationAction> ChooseNegotiationActionAsync(
        IReadOnlyList<Card> hand,
        NegotiationState negotiationState,
        MatchState matchState)
    {
        // Render the current state
        GameRenderer.RenderNegotiationState(matchState, hand);

        // Get valid actions
        var validActions = NegotiationEngine.GetValidActions(negotiationState);

        // Show prompt and get selection
        var action = Prompts.SelectNegotiationAction(validActions, Position);

        return Task.FromResult(action);
    }

    public Task<Card> ChooseCardAsync(
        IReadOnlyList<Card> hand,
        HandState handState,
        MatchState matchState)
    {
        // Get valid plays
        var player = Player.Create(Position, hand);
        var validPlays = PlayValidator.GetValidPlays(player, handState.CurrentTrick!, handState.GameMode);

        // Render current state with valid plays highlighted
        GameRenderer.RenderPlayState(matchState, hand, validPlays);

        // Show prompt and get selection
        var card = Prompts.SelectCard(validPlays, handState.GameMode);

        return Task.FromResult(card);
    }

    public Task OnDealStartedAsync(MatchState matchState)
    {
        GameRenderer.RenderDealStart(matchState);
        Prompts.WaitForEnter("Press [bold]Enter[/] to begin the deal...");
        return Task.CompletedTask;
    }

    public Task OnDealEndedAsync(DealResult result, MatchState matchState)
    {
        GameRenderer.RenderDealResult(result, matchState);
        Prompts.WaitForEnter();
        return Task.CompletedTask;
    }

    public Task OnMatchEndedAsync(MatchState matchState)
    {
        GameRenderer.RenderMatchResult(matchState);
        Prompts.WaitForEnter("Press [bold]Enter[/] to exit...");
        return Task.CompletedTask;
    }
}
