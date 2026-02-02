using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.State;
using Spectre.Console;

namespace Giretra.UI;

/// <summary>
/// Renders the game state using Spectre.Console panels and tables.
/// </summary>
public static class GameRenderer
{
    /// <summary>
    /// Renders the match header with scores and current mode.
    /// </summary>
    public static void RenderMatchHeader(MatchState matchState, GameMode? currentMode = null)
    {
        var modeText = currentMode.HasValue
            ? $"Mode: {CardRenderer.RenderGameMode(currentMode.Value)}"
            : "Mode: --";

        var header = new Rule($"[bold yellow]GIRETRA[/]  |  " +
                             $"Score: [blue]{matchState.Team1MatchPoints}[/]-[green]{matchState.Team2MatchPoints}[/]  |  " +
                             $"Target: {matchState.TargetScore}  |  " +
                             modeText);
        header.Style = Style.Parse("dim");
        AnsiConsole.Write(header);
    }

    /// <summary>
    /// Renders the current trick in a compass layout.
    /// </summary>
    public static void RenderTrick(TrickState? trick, GameMode? gameMode = null)
    {
        var topCard = GetCardAtPosition(trick, PlayerPosition.Top, gameMode);
        var leftCard = GetCardAtPosition(trick, PlayerPosition.Left, gameMode);
        var rightCard = GetCardAtPosition(trick, PlayerPosition.Right, gameMode);
        var bottomCard = GetCardAtPosition(trick, PlayerPosition.Bottom, gameMode);

        var leadSuitText = trick?.LeadSuit.HasValue == true
            ? $"Lead: {CardRenderer.GetSuitSymbol(trick.LeadSuit.Value)}"
            : "";

        // Build the trick display
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"                     [dim]TOP (Partner)[/]");
        AnsiConsole.MarkupLine($"                       {topCard}");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"       [dim]LEFT[/]              {'\u250C'}{new string('\u2500', 7)}{'\u2510'}           [dim]RIGHT[/]");
        AnsiConsole.MarkupLine($"       {leftCard,-20}{'\u2502'} {bottomCard,-5} {'\u2502'}           {rightCard}");
        AnsiConsole.MarkupLine($"                         {'\u2502'}  {leftCard,-4} {'\u2502'}");
        AnsiConsole.MarkupLine($"                         {'\u2502'}  {topCard,-4} {'\u2502'}");
        AnsiConsole.MarkupLine($"                         {'\u2502'}  {rightCard,-4}{'\u2502'}");
        AnsiConsole.MarkupLine($"                         {'\u2514'}{new string('\u2500', 7)}{'\u2518'}");
        AnsiConsole.MarkupLine($"                          {leadSuitText}");
    }

    /// <summary>
    /// Renders the trick as a simple horizontal display.
    /// </summary>
    public static void RenderTrickSimple(TrickState? trick, GameMode? gameMode = null)
    {
        if (trick == null || trick.PlayedCards.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No cards played yet[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("Player").Centered())
            .AddColumn(new TableColumn("Card").Centered());

        foreach (var played in trick.PlayedCards)
        {
            var posName = played.Player.ToString();
            if (played.Player == PlayerPosition.Bottom)
                posName = "[bold]You[/]";
            else if (played.Player == PlayerPosition.Top)
                posName = "[blue]Partner[/]";

            table.AddRow(posName, CardRenderer.Render(played.Card, gameMode));
        }

        AnsiConsole.Write(table);

        if (trick.LeadSuit.HasValue)
        {
            var leadSymbol = CardRenderer.GetSuitSymbol(trick.LeadSuit.Value);
            var isRed = trick.LeadSuit.Value is CardSuit.Hearts or CardSuit.Diamonds;
            var leadMarkup = isRed ? $"[red]{leadSymbol}[/]" : leadSymbol;
            AnsiConsole.MarkupLine($"Lead suit: {leadMarkup}");
        }
    }

    private static string GetCardAtPosition(TrickState? trick, PlayerPosition position, GameMode? gameMode)
    {
        if (trick == null) return "[dim]--[/]";

        var playedCard = trick.PlayedCards.FirstOrDefault(pc => pc.Player == position);
        if (playedCard.Card.Rank == 0) return "[dim]--[/]";

        return CardRenderer.Render(playedCard.Card, gameMode);
    }

    /// <summary>
    /// Renders the player's hand with valid plays highlighted.
    /// </summary>
    public static void RenderHand(
        IReadOnlyList<Card> hand,
        IReadOnlyList<Card>? validPlays = null,
        GameMode? gameMode = null)
    {
        AnsiConsole.Write(new Rule("[bold]YOUR HAND[/]").LeftJustified());

        var grouped = hand
            .GroupBy(c => c.Suit)
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            var suitSymbol = CardRenderer.GetSuitSymbol(group.Key);
            var isRed = group.Key is CardSuit.Hearts or CardSuit.Diamonds;
            var suitMarkup = isRed ? $"[red]{suitSymbol}[/]" : suitSymbol;

            var cardStrings = group
                .OrderByDescending(c => c.GetStrength(gameMode ?? GameMode.SansAs))
                .Select(c =>
                {
                    var rendered = CardRenderer.Render(c, gameMode);
                    var isValid = validPlays?.Contains(c) ?? true;
                    return isValid ? rendered : $"[dim strikethrough]{CardRenderer.RenderPlain(c)}[/]";
                });

            AnsiConsole.MarkupLine($"  {suitMarkup}: {string.Join("  ", cardStrings)}");
        }

        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Renders the negotiation state showing bid history.
    /// </summary>
    public static void RenderNegotiation(NegotiationState negotiation)
    {
        AnsiConsole.Write(new Rule("[bold]NEGOTIATION[/]").LeftJustified());

        if (negotiation.Actions.Count > 0)
        {
            var table = new Table()
                .Border(TableBorder.Simple)
                .AddColumn("Player")
                .AddColumn("Action");

            foreach (var action in negotiation.Actions)
            {
                var playerName = action.Player == PlayerPosition.Bottom ? "[bold]You[/]" : action.Player.ToString();
                var actionText = action switch
                {
                    AnnouncementAction a => $"Announces {CardRenderer.RenderGameMode(a.Mode)}",
                    AcceptAction => "Accepts",
                    DoubleAction d => $"Doubles {CardRenderer.RenderGameMode(d.TargetMode)}",
                    RedoubleAction r => $"Redoubles {CardRenderer.RenderGameMode(r.TargetMode)}",
                    _ => action.ToString()
                };
                table.AddRow(playerName, actionText);
            }

            AnsiConsole.Write(table);
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]No bids yet[/]");
        }

        if (negotiation.CurrentBid.HasValue)
        {
            AnsiConsole.MarkupLine($"\nCurrent bid: [bold]{CardRenderer.RenderGameMode(negotiation.CurrentBid.Value)}[/] by {negotiation.CurrentBidder}");
        }

        AnsiConsole.MarkupLine($"Turn: [bold]{(negotiation.CurrentPlayer == PlayerPosition.Bottom ? "You" : negotiation.CurrentPlayer.ToString())}[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Renders the deal result.
    /// </summary>
    public static void RenderDealResult(DealResult result, MatchState matchState)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold yellow]DEAL COMPLETE[/]"));
        AnsiConsole.WriteLine();

        var resultTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("", c => c.Width(20))
            .AddColumn("[blue]Team 1 (You + Partner)[/]", c => c.Centered())
            .AddColumn("[green]Team 2 (Left + Right)[/]", c => c.Centered());

        resultTable.AddRow("Card Points", result.Team1CardPoints.ToString(), result.Team2CardPoints.ToString());
        resultTable.AddRow("Match Points",
            result.Team1MatchPoints > 0 ? $"[bold]+{result.Team1MatchPoints}[/]" : "0",
            result.Team2MatchPoints > 0 ? $"[bold]+{result.Team2MatchPoints}[/]" : "0");
        resultTable.AddRow("[bold]Total Score[/]",
            $"[bold blue]{matchState.Team1MatchPoints}[/]",
            $"[bold green]{matchState.Team2MatchPoints}[/]");

        AnsiConsole.Write(resultTable);
        AnsiConsole.WriteLine();

        // Show game mode and announcer
        AnsiConsole.MarkupLine($"Mode: {CardRenderer.RenderGameMode(result.GameMode)}");
        AnsiConsole.MarkupLine($"Announcer: {(result.AnnouncerTeam == Team.Team1 ? "[blue]Team 1[/]" : "[green]Team 2[/]")}");

        if (result.Multiplier != MultiplierState.Normal)
        {
            AnsiConsole.MarkupLine($"Multiplier: [bold]{result.Multiplier}[/]");
        }

        if (result.WasSweep)
        {
            AnsiConsole.MarkupLine($"[bold yellow]SWEEP by {(result.SweepingTeam == Team.Team1 ? "Team 1" : "Team 2")}![/]");
        }

        if (result.AnnouncerWon)
        {
            var winnerTeam = result.AnnouncerTeam == Team.Team1 ? "[blue]Team 1[/]" : "[green]Team 2[/]";
            AnsiConsole.MarkupLine($"\n{winnerTeam} wins the deal!");
        }
        else
        {
            var winnerTeam = result.AnnouncerTeam == Team.Team1 ? "[green]Team 2[/]" : "[blue]Team 1[/]";
            AnsiConsole.MarkupLine($"\n{winnerTeam} wins the deal! (Announcer failed)");
        }

        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Renders the final match result.
    /// </summary>
    public static void RenderMatchResult(MatchState matchState)
    {
        AnsiConsole.Clear();

        var winnerText = matchState.Winner == Team.Team1
            ? "[bold blue]TEAM 1 WINS![/]"
            : "[bold green]TEAM 2 WINS![/]";

        AnsiConsole.Write(new FigletText("GAME OVER").Centered().Color(Color.Yellow));
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule(winnerText).RuleStyle("yellow"));
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Double)
            .AddColumn("[blue]Team 1 (You + Partner)[/]", c => c.Centered())
            .AddColumn("[green]Team 2 (Left + Right)[/]", c => c.Centered());

        table.AddRow(
            $"[bold]{matchState.Team1MatchPoints}[/]",
            $"[bold]{matchState.Team2MatchPoints}[/]");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        if (matchState.Winner == Team.Team1)
        {
            AnsiConsole.MarkupLine("[bold blue]Congratulations! You and your partner won![/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[green]The opponents won this time. Better luck next game![/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Total deals played: {matchState.CompletedDeals.Count}[/]");
    }

    /// <summary>
    /// Renders deal start information.
    /// </summary>
    public static void RenderDealStart(MatchState matchState)
    {
        AnsiConsole.Clear();
        RenderMatchHeader(matchState);
        AnsiConsole.WriteLine();

        var dealNumber = matchState.CompletedDeals.Count + 1;
        AnsiConsole.Write(new Rule($"[bold]Deal #{dealNumber}[/]"));
        AnsiConsole.MarkupLine($"\nDealer: {matchState.CurrentDealer}");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Renders the current game state during play.
    /// </summary>
    public static void RenderPlayState(
        MatchState matchState,
        IReadOnlyList<Card> playerHand,
        IReadOnlyList<Card>? validPlays = null)
    {
        AnsiConsole.Clear();

        var deal = matchState.CurrentDeal!;
        var hand = deal.Hand!;
        var trick = hand.CurrentTrick;

        RenderMatchHeader(matchState, deal.ResolvedMode);
        AnsiConsole.WriteLine();

        // Show trick info
        var trickNum = trick?.TrickNumber ?? (hand.CompletedTricks.Count + 1);
        AnsiConsole.MarkupLine($"[bold]Trick {trickNum} of 8[/]  |  " +
                              $"Points: [blue]{hand.Team1CardPoints}[/]-[green]{hand.Team2CardPoints}[/]");
        AnsiConsole.WriteLine();

        // Show current trick
        RenderTrickSimple(trick, deal.ResolvedMode);
        AnsiConsole.WriteLine();

        // Show hand
        RenderHand(playerHand, validPlays, deal.ResolvedMode);
    }

    /// <summary>
    /// Renders the negotiation phase state.
    /// </summary>
    public static void RenderNegotiationState(MatchState matchState, IReadOnlyList<Card> playerHand)
    {
        AnsiConsole.Clear();

        var deal = matchState.CurrentDeal!;

        RenderMatchHeader(matchState);
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine($"[bold]Negotiation Phase[/]  |  Dealer: {deal.Dealer}");
        AnsiConsole.WriteLine();

        // Show hand first
        RenderHand(playerHand);

        // Show negotiation history
        RenderNegotiation(deal.Negotiation!);
    }
}
