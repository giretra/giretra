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

    // Track the last known hand for rendering during opponent turns
    private IReadOnlyList<Card>? _lastKnownHand;

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
        _lastKnownHand = hand;

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
                // Update last known hand after playing a card
                _lastKnownHand = hand.Where(c => !c.Equals(card.Value)).ToList();
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

    public Task OnDealEndedAsync(DealResult result, HandState handState, MatchState matchState)
    {
        AnsiConsole.Clear();
        var dealNum = matchState.CompletedDeals.Count;
        ScoreboardRenderer.RenderDealResult(result, dealNum);

        return Task.CompletedTask;
    }

    public Task ConfirmContinueDealAsync(MatchState matchState)
    {
        // Wait for user to confirm they're ready for the next deal
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

    public Task ConfirmContinueMatchAsync(MatchState matchState)
    {
        // Wait for user to confirm they're ready to continue after match ends
        AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
        Console.ReadKey(true);

        return Task.CompletedTask;
    }

    public async Task OnCardPlayedAsync(PlayerPosition player, Card card, HandState handState, MatchState matchState)
    {
        // Don't animate when we (Bottom) play - the user already knows what they played
        if (player == PlayerPosition.Bottom)
            return;

        // Get player name for display
        var playerName = player switch
        {
            PlayerPosition.Top => "[blue]Partner[/]",
            PlayerPosition.Left => "[green]Left[/]",
            PlayerPosition.Right => "[green]Right[/]",
            _ => player.ToString()
        };

        var cardMarkup = CardRenderer.ToMarkup(card, handState.GameMode);

        // Render the current game state with the played card
        RenderPlayScreenWithStatus(handState, matchState);

        // Show animated status for the play
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("yellow"))
            .StartAsync($"{playerName} thinking...", async ctx =>
            {
                await Task.Delay(500);
                ctx.Status($"{playerName} plays {cardMarkup}");
                await Task.Delay(500);
            });
    }

    private void RenderPlayScreenWithStatus(HandState handState, MatchState matchState)
    {
        AnsiConsole.Clear();

        var hand = _lastKnownHand ?? [];

        // Get card counts for all players
        var cardCounts = new Dictionary<PlayerPosition, int>
        {
            [PlayerPosition.Bottom] = hand.Count,
            [PlayerPosition.Left] = 8 - handState.CompletedTricks.Count - (handState.CurrentTrick?.PlayedCards.Any(pc => pc.Player == PlayerPosition.Left) == true ? 1 : 0),
            [PlayerPosition.Top] = 8 - handState.CompletedTricks.Count - (handState.CurrentTrick?.PlayedCards.Any(pc => pc.Player == PlayerPosition.Top) == true ? 1 : 0),
            [PlayerPosition.Right] = 8 - handState.CompletedTricks.Count - (handState.CurrentTrick?.PlayedCards.Any(pc => pc.Player == PlayerPosition.Right) == true ? 1 : 0)
        };

        MultiplierState? multiplier = matchState.CurrentDeal?.Multiplier;

        ScoreboardRenderer.RenderHeader(matchState, handState.GameMode, multiplier);
        TableRenderer.RenderTable(handState, cardCounts, matchState);
        HandRenderer.RenderHand(hand, handState.GameMode);
        AnsiConsole.WriteLine();
    }

    public Task OnTrickCompletedAsync(TrickState completedTrick, PlayerPosition winner, HandState handState, MatchState matchState)
    {
        AnsiConsole.Clear();

        var hand = _lastKnownHand ?? [];
        MultiplierState? multiplier = matchState.CurrentDeal?.Multiplier;

        // Card counts (cards already played this trick are now gone)
        var cardCounts = new Dictionary<PlayerPosition, int>
        {
            [PlayerPosition.Bottom] = hand.Count,
            [PlayerPosition.Left] = 8 - handState.CompletedTricks.Count,
            [PlayerPosition.Top] = 8 - handState.CompletedTricks.Count,
            [PlayerPosition.Right] = 8 - handState.CompletedTricks.Count
        };

        ScoreboardRenderer.RenderHeader(matchState, handState.GameMode, multiplier);

        // Render the table with the completed trick's cards
        TableRenderer.RenderTableWithTrick(completedTrick, winner, cardCounts, handState.GameMode);

        // Show current scores
        ScoreboardRenderer.RenderTrickPoints(handState);

        AnsiConsole.WriteLine();
        HandRenderer.RenderHand(hand, handState.GameMode);

        // Winner message
        var winnerName = winner switch
        {
            PlayerPosition.Bottom => "[blue bold]You[/]",
            PlayerPosition.Top => "[blue bold]Partner[/]",
            PlayerPosition.Left => "[green bold]Left[/]",
            PlayerPosition.Right => "[green bold]Right[/]",
            _ => winner.ToString()
        };

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  {winnerName} wins! [dim]Press any key to continue...[/]");
        Console.ReadKey(true);

        return Task.CompletedTask;
    }
}
