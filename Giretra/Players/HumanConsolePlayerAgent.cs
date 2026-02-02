using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.State;
using Giretra.Input;
using Giretra.UI;
using Spectre.Console;

namespace Giretra.Players;

/// <summary>
/// Human player agent using Spectre.Console for interaction.
/// </summary>
public sealed class HumanConsolePlayerAgent : IPlayerAgent
{
    public PlayerPosition Position { get; }

    public HumanConsolePlayerAgent(PlayerPosition position)
    {
        Position = position;
    }

    public Task<(int position, bool fromTop)> ChooseCutAsync(int deckSize, MatchState matchState)
    {
        AnsiConsole.Clear();
        ScoreboardRenderer.RenderHeader(matchState);
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold]DECK CUT[/]").RuleStyle("yellow"));

        var result = Prompts.PromptCutPosition(deckSize);
        Prompts.ShowCutResult(result.position, result.fromTop);

        return Task.FromResult(result);
    }

    public Task<NegotiationAction> ChooseNegotiationActionAsync(
        IReadOnlyList<Card> hand,
        NegotiationState negotiationState,
        MatchState matchState,
        IReadOnlyList<NegotiationAction> validActions)
    {
        NegotiationRenderer.RenderNegotiationState(negotiationState, matchState);

        // Show player's hand
        AnsiConsole.WriteLine();
        HandRenderer.RenderHand(hand, null);

        // Show available actions and prompt
        NegotiationRenderer.RenderAvailableActions(validActions);
        var action = Prompts.PromptNegotiationAction(validActions, Position);

        return Task.FromResult(action);
    }

    public Task<Card> ChooseCardAsync(
        IReadOnlyList<Card> hand,
        HandState handState,
        MatchState matchState,
        IReadOnlyList<Card> validPlays)
    {
        while (true)
        {
            RenderPlayScreen(hand, handState, matchState, validPlays);

            var (card, command) = Prompts.PromptCardSelection(hand, validPlays, handState.GameMode);

            if (command != null)
            {
                CommandHandler.HandleCommand(command, handState, matchState);
                continue; // Redraw and prompt again
            }

            if (card.HasValue)
            {
                return Task.FromResult(card.Value);
            }
        }
    }

    private void RenderPlayScreen(
        IReadOnlyList<Card> hand,
        HandState handState,
        MatchState matchState,
        IReadOnlyList<Card> validPlays)
    {
        AnsiConsole.Clear();

        // Get card counts for all players
        var cardCounts = new Dictionary<PlayerPosition, int>
        {
            [PlayerPosition.Bottom] = hand.Count,
            [PlayerPosition.Left] = 8 - handState.CompletedTricks.Count - (handState.CurrentTrick?.PlayedCards.Any(pc => pc.Player == PlayerPosition.Left) == true ? 1 : 0),
            [PlayerPosition.Top] = 8 - handState.CompletedTricks.Count - (handState.CurrentTrick?.PlayedCards.Any(pc => pc.Player == PlayerPosition.Top) == true ? 1 : 0),
            [PlayerPosition.Right] = 8 - handState.CompletedTricks.Count - (handState.CurrentTrick?.PlayedCards.Any(pc => pc.Player == PlayerPosition.Right) == true ? 1 : 0)
        };

        // Get multiplier from match state if available
        MultiplierState? multiplier = matchState.CurrentDeal?.Multiplier;

        ScoreboardRenderer.RenderHeader(matchState, handState.GameMode, multiplier);
        TableRenderer.RenderTable(handState, cardCounts, matchState);
        HandRenderer.RenderHand(hand, handState.GameMode, validPlays);
    }

    public Task OnDealStartedAsync(MatchState matchState)
    {
        AnsiConsole.Clear();
        ScoreboardRenderer.RenderHeader(matchState);
        AnsiConsole.WriteLine();

        var dealNum = matchState.CompletedDeals.Count + 1;
        var dealerText = matchState.CurrentDealer switch
        {
            PlayerPosition.Bottom => "[blue]You[/]",
            PlayerPosition.Top => "[blue]Top (Partner)[/]",
            PlayerPosition.Left => "[green]Left[/]",
            PlayerPosition.Right => "[green]Right[/]",
            _ => matchState.CurrentDealer.ToString()
        };

        AnsiConsole.Write(
            new Panel(
                new Markup($"[bold]Deal #{dealNum}[/]\n\nDealer: {dealerText}"))
            .Header("[bold yellow]NEW DEAL[/]")
            .Border(BoxBorder.Rounded)
            .Padding(2, 1));

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
        Console.ReadKey(true);

        return Task.CompletedTask;
    }

    public Task OnDealEndedAsync(DealResult result, MatchState matchState)
    {
        AnsiConsole.Clear();
        var dealNum = matchState.CompletedDeals.Count;
        ScoreboardRenderer.RenderDealResult(result, dealNum);

        AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
        Console.ReadKey(true);

        return Task.CompletedTask;
    }

    public Task OnMatchEndedAsync(MatchState matchState)
    {
        AnsiConsole.Clear();
        ScoreboardRenderer.RenderMatchResult(matchState);

        return Task.CompletedTask;
    }
}
