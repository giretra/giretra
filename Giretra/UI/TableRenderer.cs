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

        // Build the compact table layout (fits in 80 chars)
        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .Width(78);

        table.AddColumn(new TableColumn("left").Width(20));
        table.AddColumn(new TableColumn("center").Width(38));
        table.AddColumn(new TableColumn("right").Width(20));

        // Row 1: Empty | TOP | Empty
        var topContent = BuildCompactPlayerContent("TOP", cardCounts[PlayerPosition.Top], playedCards[PlayerPosition.Top], gameMode, "blue", isPartner: true);
        table.AddRow(
            new Text(""),
            new Panel(topContent).Header("[blue]TOP (Partner)[/]").Border(BoxBorder.Rounded).Expand(),
            new Text(""));

        // Row 2: LEFT | TRICK | RIGHT
        var leftContent = BuildCompactPlayerContent("LEFT", cardCounts[PlayerPosition.Left], playedCards[PlayerPosition.Left], gameMode, "green");
        var rightContent = BuildCompactPlayerContent("RIGHT", cardCounts[PlayerPosition.Right], playedCards[PlayerPosition.Right], gameMode, "green");
        var trickContent = BuildCompactTrickPanel(trick, gameMode);

        table.AddRow(
            new Panel(leftContent).Header("[green]LEFT[/]").Border(BoxBorder.Rounded).Expand(),
            trickContent,
            new Panel(rightContent).Header("[green]RIGHT[/]").Border(BoxBorder.Rounded).Expand());

        AnsiConsole.WriteLine();
        AnsiConsole.Write(table);

        // Trick points summary (compact)
        ScoreboardRenderer.RenderTrickPoints(handState);
        AnsiConsole.WriteLine();
    }

    private static IRenderable BuildCompactPlayerContent(string name, int cardCount, Card? playedCard, GameMode gameMode, string teamColor, bool isPartner = false)
    {
        var hiddenCards = CardRenderer.RenderFaceDown(cardCount);
        var playedText = playedCard.HasValue
            ? CardRenderer.ToMarkup(playedCard.Value, gameMode)
            : "[dim]--[/]";

        // Use [[ and ]] to escape brackets in Spectre.Console markup
        return new Markup($"[[{hiddenCards}]] {playedText}");
    }

    private static Panel BuildCompactTrickPanel(TrickState? trick, GameMode gameMode)
    {
        var trickNum = trick?.TrickNumber ?? 1;
        var leadSuit = trick?.LeadSuit;
        var leader = trick?.Leader;

        var topCard = GetTrickCardMarkup(trick, PlayerPosition.Top, gameMode);
        var leftCard = GetTrickCardMarkup(trick, PlayerPosition.Left, gameMode);
        var rightCard = GetTrickCardMarkup(trick, PlayerPosition.Right, gameMode);
        var bottomCard = GetTrickCardMarkup(trick, PlayerPosition.Bottom, gameMode);

        var leadText = leadSuit.HasValue
            ? $"Lead: {CardRenderer.SuitToMarkup(leadSuit.Value)}"
            : "[dim]Waiting...[/]";

        // Show leader with arrow marker
        string GetCardWithMarker(PlayerPosition pos, string card)
        {
            var marker = leader == pos ? "[yellow bold]>[/]" : " ";
            return $"{marker}{card}";
        }

        // Compass-style layout: cards positioned in front of each player
        var grid = new Grid();
        grid.AddColumn(new GridColumn().Width(10));  // left
        grid.AddColumn(new GridColumn().Width(10));  // center
        grid.AddColumn(new GridColumn().Width(10));  // right

        // Row 1: TOP card centered
        grid.AddRow(
            new Text(""),
            Align.Center(new Markup(GetCardWithMarker(PlayerPosition.Top, topCard))),
            new Text(""));

        // Row 2: LEFT card | empty | RIGHT card
        grid.AddRow(
            new Markup(GetCardWithMarker(PlayerPosition.Left, leftCard)),
            new Text(""),
            Align.Right(new Markup(GetCardWithMarker(PlayerPosition.Right, rightCard))));

        // Row 3: BOTTOM/YOU card centered
        grid.AddRow(
            new Text(""),
            Align.Center(new Markup(GetCardWithMarker(PlayerPosition.Bottom, bottomCard))),
            new Text(""));

        var content = new Rows(
            new Markup(leadText),
            grid
        );

        return new Panel(content)
            .Header($"[yellow]Trick #{trickNum}[/]")
            .Border(BoxBorder.Rounded)
            .Expand();
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
    /// Renders the table with a specific completed trick, highlighting the winner.
    /// </summary>
    public static void RenderTableWithTrick(
        TrickState trick,
        PlayerPosition winner,
        IReadOnlyDictionary<PlayerPosition, int> cardCounts,
        GameMode gameMode)
    {
        // Get played cards for each position from the completed trick
        var playedCards = new Dictionary<PlayerPosition, Card?>();
        foreach (var pos in Enum.GetValues<PlayerPosition>())
        {
            var played = trick.PlayedCards.FirstOrDefault(pc => pc.Player == pos);
            playedCards[pos] = played.Card.Equals(default(Card)) ? null : played.Card;
        }

        // Build the compact table layout (fits in 80 chars)
        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .Width(78);

        table.AddColumn(new TableColumn("left").Width(20));
        table.AddColumn(new TableColumn("center").Width(38));
        table.AddColumn(new TableColumn("right").Width(20));

        // Row 1: Empty | TOP | Empty
        var topContent = BuildCompactPlayerContent("TOP", cardCounts[PlayerPosition.Top], playedCards[PlayerPosition.Top], gameMode, "blue", isPartner: true);
        table.AddRow(
            new Text(""),
            new Panel(topContent).Header("[blue]TOP (Partner)[/]").Border(BoxBorder.Rounded).Expand(),
            new Text(""));

        // Row 2: LEFT | TRICK | RIGHT
        var leftContent = BuildCompactPlayerContent("LEFT", cardCounts[PlayerPosition.Left], playedCards[PlayerPosition.Left], gameMode, "green");
        var rightContent = BuildCompactPlayerContent("RIGHT", cardCounts[PlayerPosition.Right], playedCards[PlayerPosition.Right], gameMode, "green");
        var trickContent = BuildCompletedTrickPanel(trick, winner, gameMode);

        table.AddRow(
            new Panel(leftContent).Header("[green]LEFT[/]").Border(BoxBorder.Rounded).Expand(),
            trickContent,
            new Panel(rightContent).Header("[green]RIGHT[/]").Border(BoxBorder.Rounded).Expand());

        AnsiConsole.WriteLine();
        AnsiConsole.Write(table);
    }

    private static Panel BuildCompletedTrickPanel(TrickState trick, PlayerPosition winner, GameMode gameMode)
    {
        var trickNum = trick.TrickNumber;
        var leadSuit = trick.LeadSuit;

        var leadText = $"Lead: {CardRenderer.SuitToMarkup(leadSuit!.Value)}";

        // Show winner with star marker
        string GetCardMarkup(PlayerPosition pos)
        {
            var played = trick.PlayedCards.FirstOrDefault(pc => pc.Player == pos);
            if (played.Card.Equals(default(Card))) return "[dim]--[/]";
            var cardMarkup = CardRenderer.ToMarkup(played.Card, gameMode);
            var marker = winner == pos ? "[yellow bold]*[/]" : " ";
            // Highlight winner's card
            var card = winner == pos ? $"[bold underline]{cardMarkup}[/]" : cardMarkup;
            return $"{marker}{card}";
        }

        // Compass-style layout: cards positioned in front of each player
        var grid = new Grid();
        grid.AddColumn(new GridColumn().Width(10));  // left
        grid.AddColumn(new GridColumn().Width(10));  // center
        grid.AddColumn(new GridColumn().Width(10));  // right

        // Row 1: TOP card centered
        grid.AddRow(
            new Text(""),
            Align.Center(new Markup(GetCardMarkup(PlayerPosition.Top))),
            new Text(""));

        // Row 2: LEFT card | empty | RIGHT card
        grid.AddRow(
            new Markup(GetCardMarkup(PlayerPosition.Left)),
            new Text(""),
            Align.Right(new Markup(GetCardMarkup(PlayerPosition.Right))));

        // Row 3: BOTTOM/YOU card centered
        grid.AddRow(
            new Text(""),
            Align.Center(new Markup(GetCardMarkup(PlayerPosition.Bottom))),
            new Text(""));

        var content = new Rows(
            new Markup(leadText),
            grid
        );

        return new Panel(content)
            .Header($"[yellow bold]Trick #{trickNum} - Complete[/]")
            .Border(BoxBorder.Rounded)
            .Expand();
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
