using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Core.State;
using Spectre.Console;

namespace Giretra.UI;

/// <summary>
/// Displays trick history during or after a hand.
/// </summary>
public static class TrickHistoryViewer
{
    /// <summary>
    /// Renders all completed tricks in a table format.
    /// </summary>
    public static void RenderTrickHistory(HandState handState)
    {
        var gameMode = handState.GameMode;
        var completedTricks = handState.CompletedTricks;

        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule($"[bold]TRICK HISTORY[/] - {CardRenderer.GameModeToMarkup(gameMode)}").RuleStyle("blue"));
        AnsiConsole.WriteLine();

        if (completedTricks.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No tricks completed yet.[/]");
            WaitForKey();
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("#")
            .AddColumn("Leader")
            .AddColumn("You (Bottom)")
            .AddColumn("Left")
            .AddColumn("Top")
            .AddColumn("Right")
            .AddColumn("Winner")
            .AddColumn("Pts");

        int team1TotalPoints = 0;
        int team2TotalPoints = 0;

        foreach (var trick in completedTricks)
        {
            var leader = trick.Leader;
            var winner = DetermineWinner(trick, gameMode);
            var points = CalculateTrickPoints(trick, gameMode);

            if (winner.GetTeam() == Team.Team1)
                team1TotalPoints += points;
            else
                team2TotalPoints += points;

            var leaderText = FormatPosition(leader);
            var winnerText = FormatPosition(winner, highlight: true);

            // Get each player's card
            var bottomCard = GetCardMarkup(trick, PlayerPosition.Bottom, gameMode, leader, winner);
            var leftCard = GetCardMarkup(trick, PlayerPosition.Left, gameMode, leader, winner);
            var topCard = GetCardMarkup(trick, PlayerPosition.Top, gameMode, leader, winner);
            var rightCard = GetCardMarkup(trick, PlayerPosition.Right, gameMode, leader, winner);

            table.AddRow(
                $"{trick.TrickNumber}",
                leaderText,
                bottomCard,
                leftCard,
                topCard,
                rightCard,
                winnerText,
                $"{points}");
        }

        // Summary row
        table.AddRow(
            "[bold]Total[/]",
            "",
            "",
            "",
            "",
            "",
            "",
            "");

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"Points: [blue]Team 1: {team1TotalPoints}[/] | [green]Team 2: {team2TotalPoints}[/]");
        AnsiConsole.MarkupLine($"Tricks Won: [blue]Team 1: {handState.Team1TricksWon}[/] | [green]Team 2: {handState.Team2TricksWon}[/]");

        WaitForKey();
    }

    private static string GetCardMarkup(TrickState trick, PlayerPosition pos, GameMode gameMode, PlayerPosition leader, PlayerPosition winner)
    {
        var playedCard = trick.PlayedCards.FirstOrDefault(pc => pc.Player == pos);
        if (playedCard.Card.Equals(default))
            return "--";

        var cardText = CardRenderer.ToMarkup(playedCard.Card, gameMode);

        // Highlight leader with *
        if (pos == leader)
            cardText = $"{cardText}*";

        // Highlight winner
        if (pos == winner)
            cardText = $"[bold underline]{CardRenderer.ToPlainText(playedCard.Card)}[/]";

        return cardText;
    }

    private static string FormatPosition(PlayerPosition pos, bool highlight = false)
    {
        var text = pos switch
        {
            PlayerPosition.Bottom => "You",
            PlayerPosition.Top => "Top",
            PlayerPosition.Left => "Left",
            PlayerPosition.Right => "Right",
            _ => pos.ToString()
        };

        var color = pos.GetTeam() == Team.Team1 ? "blue" : "green";

        if (highlight)
            return $"[bold {color}]{text}[/]";

        return $"[{color}]{text}[/]";
    }

    private static PlayerPosition DetermineWinner(TrickState trick, GameMode gameMode)
    {
        var leadSuit = trick.LeadSuit!.Value;
        var trumpSuit = gameMode.GetTrumpSuit();

        var bestCard = trick.PlayedCards.First();

        foreach (var playedCard in trick.PlayedCards.Skip(1))
        {
            if (BeatsCard(playedCard.Card, bestCard.Card, leadSuit, trumpSuit, gameMode))
            {
                bestCard = playedCard;
            }
        }

        return bestCard.Player;
    }

    private static bool BeatsCard(Core.Cards.Card challenger, Core.Cards.Card current, Core.Cards.CardSuit leadSuit, Core.Cards.CardSuit? trumpSuit, GameMode gameMode)
    {
        var challengerIsTrump = trumpSuit.HasValue && challenger.Suit == trumpSuit.Value;
        var currentIsTrump = trumpSuit.HasValue && current.Suit == trumpSuit.Value;

        // Trump beats non-trump
        if (challengerIsTrump && !currentIsTrump)
            return true;
        if (!challengerIsTrump && currentIsTrump)
            return false;

        // Both trump or neither trump - must follow lead suit to win
        if (!challengerIsTrump && challenger.Suit != leadSuit)
            return false;
        if (!currentIsTrump && current.Suit != leadSuit && challenger.Suit == leadSuit)
            return true;

        // Same suit - compare strength
        if (challenger.Suit == current.Suit)
        {
            return challenger.GetStrength(gameMode) > current.GetStrength(gameMode);
        }

        return false;
    }

    private static int CalculateTrickPoints(TrickState trick, GameMode gameMode)
    {
        return trick.PlayedCards.Sum(pc => pc.Card.GetPointValue(gameMode));
    }

    private static void WaitForKey()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key to return...[/]");
        Console.ReadKey(true);
    }
}
