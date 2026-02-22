using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Giretra.Core.State;
using Spectre.Console;

namespace Giretra.UI;

/// <summary>
/// Renders negotiation state and bid history.
/// </summary>
public static class NegotiationRenderer
{
    /// <summary>
    /// Renders the full negotiation state including bid history.
    /// </summary>
    public static void RenderNegotiationState(NegotiationState state, MatchState matchState)
    {
        AnsiConsole.Clear();
        ScoreboardRenderer.RenderHeader(matchState);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold]NEGOTIATION PHASE[/]").RuleStyle("yellow"));
        AnsiConsole.WriteLine();

        // Show table positions
        TableRenderer.RenderNegotiationPositions(state.CurrentPlayer, state.Dealer);

        // Show bid history
        if (state.Actions.Count > 0)
        {
            RenderBidHistory(state);
        }

        // Show current state summary
        RenderCurrentBidState(state);

        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Renders the bid history as a table.
    /// </summary>
    public static void RenderBidHistory(NegotiationState state)
    {
        AnsiConsole.Write(new Rule("[bold]Bid History[/]").RuleStyle("grey"));

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("#")
            .AddColumn("Player")
            .AddColumn("Action");

        var actionNum = 1;
        foreach (var action in state.Actions)
        {
            var playerText = GetPlayerDisplay(action.Player);
            var actionText = GetActionDisplay(action);
            table.AddRow($"{actionNum++}", playerText, actionText);
        }

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Renders the current bid state summary.
    /// </summary>
    private static void RenderCurrentBidState(NegotiationState state)
    {
        AnsiConsole.WriteLine();

        if (state.CurrentBid.HasValue)
        {
            var bidder = state.CurrentBidder!.Value;
            var bidderText = GetPlayerDisplay(bidder);
            var modeText = CardRenderer.GameModeToMarkup(state.CurrentBid.Value);

            AnsiConsole.MarkupLine($"Current bid: {modeText} by {bidderText}");
            AnsiConsole.MarkupLine($"Consecutive accepts: {state.ConsecutiveAccepts}/3");

            // Show doubled modes
            foreach (var (mode, doublerTeamInt) in state.DoubledModes)
            {
                var doublerTeam = (Team)doublerTeamInt;
                var teamText = doublerTeam == Team.Team1 ? "[blue]Team 1[/]" : "[green]Team 2[/]";
                AnsiConsole.MarkupLine($"[yellow]Doubled:[/] {CardRenderer.GameModeToMarkup(mode)} by {teamText}");
            }

            // Show redoubled modes
            foreach (var mode in state.RedoubledModes)
            {
                AnsiConsole.MarkupLine($"[red]Redoubled:[/] {CardRenderer.GameModeToMarkup(mode)}");
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]No bid yet. First player must announce a mode.[/]");
        }
    }

    private static string GetPlayerDisplay(PlayerPosition player)
    {
        return player switch
        {
            PlayerPosition.Bottom => "[blue]You[/]",
            PlayerPosition.Top => "[blue]Top (Partner)[/]",
            PlayerPosition.Left => "[green]Left[/]",
            PlayerPosition.Right => "[green]Right[/]",
            _ => player.ToString()
        };
    }

    private static string GetActionDisplay(NegotiationAction action)
    {
        return action switch
        {
            AnnouncementAction a => $"Announces {CardRenderer.GameModeToMarkup(a.Mode)}",
            AcceptAction => "[dim]Accept[/]",
            DoubleAction d => $"[yellow]Double[/] {CardRenderer.GameModeToMarkup(d.TargetMode)}",
            RedoubleAction r => $"[red]Redouble[/] {CardRenderer.GameModeToMarkup(r.TargetMode)}",
            _ => action.ToString() ?? "Unknown"
        };
    }

    /// <summary>
    /// Renders the available actions for the current player.
    /// </summary>
    public static void RenderAvailableActions(IReadOnlyList<NegotiationAction> validActions)
    {
        AnsiConsole.Write(new Rule("[bold]Your Options[/]").RuleStyle("grey"));

        foreach (var action in validActions)
        {
            var display = action switch
            {
                AnnouncementAction a => $"  Announce: {CardRenderer.GameModeToMarkup(a.Mode)}",
                AcceptAction => "  Accept (pass)",
                DoubleAction d => $"  [yellow]Double[/] {CardRenderer.GameModeToMarkup(d.TargetMode)}",
                RedoubleAction r => $"  [red]Redouble[/] {CardRenderer.GameModeToMarkup(r.TargetMode)}",
                _ => $"  {action}"
            };
            AnsiConsole.MarkupLine(display);
        }

        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Renders the resolved mode after negotiation completes.
    /// </summary>
    public static void RenderNegotiationResult(GameMode mode, Team announcerTeam, MultiplierState multiplier)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold green]NEGOTIATION COMPLETE[/]").RuleStyle("green"));
        AnsiConsole.WriteLine();

        var teamText = announcerTeam == Team.Team1 ? "[blue]Team 1 (You + Top)[/]" : "[green]Team 2 (Left + Right)[/]";
        var modeText = CardRenderer.GameModeToMarkup(mode);

        AnsiConsole.MarkupLine($"Final Mode: {modeText}");
        AnsiConsole.MarkupLine($"Announced by: {teamText}");

        if (multiplier != MultiplierState.Normal)
        {
            var multText = multiplier == MultiplierState.Doubled
                ? "[yellow]DOUBLED (x2)[/]"
                : "[red]REDOUBLED (x4)[/]";
            AnsiConsole.MarkupLine($"Multiplier: {multText}");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key to start playing...[/]");
        Console.ReadKey(true);
    }
}
