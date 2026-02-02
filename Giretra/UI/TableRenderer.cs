using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Core.State;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Giretra.UI;

/// <summary>
/// Renders the compass-style game table with current trick.
/// </summary>
public static class TableRenderer
{
    /// <summary>
    /// Renders the full game table during play.
    /// </summary>
    public static void RenderTable(
        HandState handState,
        IReadOnlyDictionary<PlayerPosition, int> cardCounts,
        MatchState matchState)
    {
        var gameMode = handState.GameMode;
        var trick = handState.CurrentTrick;

        // Get played cards for each position
        var playedCards = new Dictionary<PlayerPosition, Card?>();
        foreach (var pos in Enum.GetValues<PlayerPosition>())
        {
            playedCards[pos] = trick?.PlayedCards.FirstOrDefault(pc => pc.Player == pos).Card;
            if (playedCards[pos]?.Equals(default(Card)) == true)
                playedCards[pos] = null;
        }

        AnsiConsole.WriteLine();

        // TOP player panel
        var topPanel = BuildPlayerPanel("TOP (Partner)", cardCounts[PlayerPosition.Top], playedCards[PlayerPosition.Top], gameMode, "blue");

        // Create top row table (centered TOP player)
        var topRow = new Table().Border(TableBorder.None).HideHeaders();
        topRow.AddColumn(new TableColumn("").Centered());
        topRow.AddRow(topPanel);
        AnsiConsole.Write(topRow);

        AnsiConsole.WriteLine();

        // Middle row: LEFT | TRICK BOX | RIGHT
        var middleTable = new Table().Border(TableBorder.None).HideHeaders();
        middleTable.AddColumn(new TableColumn("left").Width(25));
        middleTable.AddColumn(new TableColumn("center").Width(35));
        middleTable.AddColumn(new TableColumn("right").Width(25));

        var leftPanel = BuildPlayerPanel("LEFT", cardCounts[PlayerPosition.Left], playedCards[PlayerPosition.Left], gameMode, "green");
        var trickPanel = BuildTrickPanel(trick, gameMode);
        var rightPanel = BuildPlayerPanel("RIGHT", cardCounts[PlayerPosition.Right], playedCards[PlayerPosition.Right], gameMode, "green");

        middleTable.AddRow(leftPanel, trickPanel, rightPanel);
        AnsiConsole.Write(middleTable);

        AnsiConsole.WriteLine();

        // Trick points summary
        ScoreboardRenderer.RenderTrickPoints(handState);

        AnsiConsole.WriteLine();
    }

    private static Panel BuildPlayerPanel(string name, int cardCount, Card? playedCard, GameMode gameMode, string teamColor)
    {
        var hiddenCards = CardRenderer.RenderFaceDown(cardCount);
        var playedText = playedCard.HasValue
            ? CardRenderer.ToMarkup(playedCard.Value, gameMode)
            : "[dim]--[/]";

        var content = new Markup($"Cards: [[{hiddenCards}]]\nPlayed: {playedText}");

        return new Panel(content)
            .Header($"[{teamColor}]{name}[/]")
            .Border(BoxBorder.Rounded)
            .Padding(1, 0);
    }

    private static Panel BuildTrickPanel(TrickState? trick, GameMode gameMode)
    {
        var trickNum = trick?.TrickNumber ?? 1;
        var leadSuit = trick?.LeadSuit;

        var topCard = GetTrickCardMarkup(trick, PlayerPosition.Top, gameMode);
        var leftCard = GetTrickCardMarkup(trick, PlayerPosition.Left, gameMode);
        var rightCard = GetTrickCardMarkup(trick, PlayerPosition.Right, gameMode);
        var bottomCard = GetTrickCardMarkup(trick, PlayerPosition.Bottom, gameMode);

        var leadText = leadSuit.HasValue
            ? $"Lead: {CardRenderer.SuitToMarkup(leadSuit.Value)}"
            : "[dim]No lead yet[/]";

        var table = new Table().Border(TableBorder.None).HideHeaders();
        table.AddColumn(new TableColumn("pos").Width(8));
        table.AddColumn(new TableColumn("card").Width(8));

        table.AddRow("[blue]TOP[/]", topCard);
        table.AddRow("[green]LEFT[/]", leftCard);
        table.AddRow("[green]RIGHT[/]", rightCard);
        table.AddRow("[blue]YOU[/]", bottomCard);

        var content = new Rows(
            new Markup(leadText),
            new Text(""),
            table
        );

        return new Panel(content)
            .Header($"[yellow]Trick #{trickNum}[/]")
            .Border(BoxBorder.Rounded)
            .Padding(1, 0);
    }

    private static string GetTrickCardMarkup(TrickState? trick, PlayerPosition pos, GameMode gameMode)
    {
        if (trick == null) return "[dim]--[/]";
        var playedCard = trick.PlayedCards.FirstOrDefault(pc => pc.Player == pos);
        if (playedCard.Card.Equals(default(Card))) return "[dim]--[/]";
        return CardRenderer.ToMarkup(playedCard.Card, gameMode);
    }

    /// <summary>
    /// Renders a simple table showing who's turn it is during negotiation.
    /// </summary>
    public static void RenderNegotiationPositions(PlayerPosition currentPlayer, PlayerPosition dealer)
    {
        AnsiConsole.WriteLine();

        // TOP row
        var topTable = new Table().Border(TableBorder.None).HideHeaders();
        topTable.AddColumn(new TableColumn("").Centered());
        topTable.AddRow(BuildPositionPanel("TOP (Partner)", PlayerPosition.Top, currentPlayer, dealer, "blue"));
        AnsiConsole.Write(topTable);

        AnsiConsole.WriteLine();

        // Middle row: LEFT and RIGHT
        var middleTable = new Table().Border(TableBorder.None).HideHeaders();
        middleTable.AddColumn(new TableColumn("left").Width(40));
        middleTable.AddColumn(new TableColumn("spacer").Width(10));
        middleTable.AddColumn(new TableColumn("right").Width(40));

        var leftPanel = BuildPositionPanel("LEFT", PlayerPosition.Left, currentPlayer, dealer, "green");
        var rightPanel = BuildPositionPanel("RIGHT", PlayerPosition.Right, currentPlayer, dealer, "green");

        middleTable.AddRow(leftPanel, new Text(""), rightPanel);
        AnsiConsole.Write(middleTable);

        AnsiConsole.WriteLine();

        // Bottom row: YOU
        var bottomTable = new Table().Border(TableBorder.None).HideHeaders();
        bottomTable.AddColumn(new TableColumn("").Centered());
        bottomTable.AddRow(BuildPositionPanel("YOU (Bottom)", PlayerPosition.Bottom, currentPlayer, dealer, "blue"));
        AnsiConsole.Write(bottomTable);

        AnsiConsole.WriteLine();
    }

    private static Panel BuildPositionPanel(string name, PlayerPosition pos, PlayerPosition currentPlayer, PlayerPosition dealer, string teamColor)
    {
        var indicators = new List<string>();

        if (pos == currentPlayer)
            indicators.Add("[yellow bold]<< YOUR TURN >>[/]");
        if (pos == dealer)
            indicators.Add("[dim](Dealer)[/]");

        var content = indicators.Count > 0
            ? new Markup(string.Join(" ", indicators))
            : new Markup("[dim] [/]");

        return new Panel(content)
            .Header($"[{teamColor}]{name}[/]")
            .Border(BoxBorder.Rounded)
            .Padding(1, 0);
    }
}
