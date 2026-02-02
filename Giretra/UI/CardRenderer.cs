using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Spectre.Console;

namespace Giretra.UI;

/// <summary>
/// Renders cards with colors and symbols using Spectre.Console markup.
/// </summary>
public static class CardRenderer
{
    private static readonly Dictionary<CardSuit, string> SuitSymbols = new()
    {
        [CardSuit.Clubs] = "♣",
        [CardSuit.Diamonds] = "♦",
        [CardSuit.Hearts] = "♥",
        [CardSuit.Spades] = "♠"
    };

    private static readonly Dictionary<CardRank, string> RankNames = new()
    {
        [CardRank.Ace] = "A",
        [CardRank.King] = "K",
        [CardRank.Queen] = "Q",
        [CardRank.Jack] = "J",
        [CardRank.Ten] = "10",
        [CardRank.Nine] = "9",
        [CardRank.Eight] = "8",
        [CardRank.Seven] = "7"
    };

    /// <summary>
    /// Gets the suit symbol for a card suit.
    /// </summary>
    public static string GetSuitSymbol(CardSuit suit) => SuitSymbols[suit];

    /// <summary>
    /// Gets the rank display name for a card rank.
    /// </summary>
    public static string GetRankName(CardRank rank) => RankNames[rank];

    /// <summary>
    /// Renders a card as plain text (no markup).
    /// </summary>
    public static string ToPlainText(Card card) => $"{GetRankName(card.Rank)}{GetSuitSymbol(card.Suit)}";

    /// <summary>
    /// Gets the base color for a suit (red for hearts/diamonds, default for clubs/spades).
    /// </summary>
    public static string GetSuitColor(CardSuit suit) => suit switch
    {
        CardSuit.Hearts => "red",
        CardSuit.Diamonds => "red",
        _ => "default"
    };

    /// <summary>
    /// Renders a card with Spectre.Console markup, applying appropriate colors.
    /// </summary>
    public static string ToMarkup(Card card, GameMode? gameMode = null, bool isTrump = false)
    {
        var text = ToPlainText(card);
        var color = GetSuitColor(card.Suit);

        // Check if this card is trump
        if (gameMode.HasValue)
        {
            var trumpSuit = gameMode.Value.GetTrumpSuit();
            isTrump = trumpSuit.HasValue && card.Suit == trumpSuit.Value;
        }

        if (isTrump)
        {
            return $"[bold yellow]{text}[/]";
        }

        if (color == "default")
        {
            return text;
        }

        return $"[{color}]{text}[/]";
    }

    /// <summary>
    /// Renders a card as dimmed (for invalid plays).
    /// </summary>
    public static string ToDimmedMarkup(Card card) => $"[dim]{ToPlainText(card)}[/]";

    /// <summary>
    /// Renders a list of cards with markup, separated by spaces.
    /// </summary>
    public static string ToMarkup(IEnumerable<Card> cards, GameMode? gameMode = null)
    {
        return string.Join("  ", cards.Select(c => ToMarkup(c, gameMode)));
    }

    /// <summary>
    /// Renders a game mode with its symbol.
    /// </summary>
    public static string GameModeToMarkup(GameMode mode) => mode switch
    {
        GameMode.ColourClubs => $"Clubs {GetSuitSymbol(CardSuit.Clubs)}",
        GameMode.ColourDiamonds => $"[red]Diamonds {GetSuitSymbol(CardSuit.Diamonds)}[/]",
        GameMode.ColourHearts => $"[red]Hearts {GetSuitSymbol(CardSuit.Hearts)}[/]",
        GameMode.ColourSpades => $"Spades {GetSuitSymbol(CardSuit.Spades)}",
        GameMode.SansAs => "SansAs",
        GameMode.ToutAs => "[bold]ToutAs[/]",
        _ => mode.ToString()
    };

    /// <summary>
    /// Renders a game mode as plain text.
    /// </summary>
    public static string GameModeToPlainText(GameMode mode) => mode switch
    {
        GameMode.ColourClubs => $"Clubs {GetSuitSymbol(CardSuit.Clubs)}",
        GameMode.ColourDiamonds => $"Diamonds {GetSuitSymbol(CardSuit.Diamonds)}",
        GameMode.ColourHearts => $"Hearts {GetSuitSymbol(CardSuit.Hearts)}",
        GameMode.ColourSpades => $"Spades {GetSuitSymbol(CardSuit.Spades)}",
        GameMode.SansAs => "SansAs",
        GameMode.ToutAs => "ToutAs",
        _ => mode.ToString()
    };

    /// <summary>
    /// Renders a suit with its symbol and color.
    /// </summary>
    public static string SuitToMarkup(CardSuit suit)
    {
        var symbol = GetSuitSymbol(suit);
        var color = GetSuitColor(suit);
        return color == "default" ? symbol : $"[{color}]{symbol}[/]";
    }

    /// <summary>
    /// Renders face-down cards for opponents.
    /// </summary>
    public static string RenderFaceDown(int count)
    {
        return string.Join("", Enumerable.Repeat("█", count));
    }
}
