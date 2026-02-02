using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Spectre.Console;

namespace Giretra.UI;

/// <summary>
/// Renders cards with colored suit symbols using Spectre.Console markup.
/// </summary>
public static class CardRenderer
{
    /// <summary>
    /// Gets the Unicode symbol for a suit.
    /// </summary>
    public static string GetSuitSymbol(CardSuit suit) => suit switch
    {
        CardSuit.Clubs => "\u2663",
        CardSuit.Diamonds => "\u2666",
        CardSuit.Hearts => "\u2665",
        CardSuit.Spades => "\u2660",
        _ => "?"
    };

    /// <summary>
    /// Gets the short name for a rank.
    /// </summary>
    public static string GetRankName(CardRank rank) => rank switch
    {
        CardRank.Ace => "A",
        CardRank.King => "K",
        CardRank.Queen => "Q",
        CardRank.Jack => "J",
        _ => ((int)rank).ToString()
    };

    /// <summary>
    /// Renders a card with colored markup.
    /// Hearts and Diamonds are red, Spades and Clubs are default color.
    /// </summary>
    public static string Render(Card card, GameMode? gameMode = null)
    {
        var rankStr = GetRankName(card.Rank);
        var suitSymbol = GetSuitSymbol(card.Suit);
        var isRed = card.Suit is CardSuit.Hearts or CardSuit.Diamonds;

        var isTrump = gameMode.HasValue &&
                      (gameMode.Value.GetCategory() == GameModeCategory.ToutAs ||
                       gameMode.Value.GetTrumpSuit() == card.Suit);

        var text = $"{rankStr}{suitSymbol}";

        if (isTrump)
        {
            return isRed
                ? $"[bold underline red]{text}[/]"
                : $"[bold underline]{text}[/]";
        }

        return isRed
            ? $"[red]{text}[/]"
            : text;
    }

    /// <summary>
    /// Renders a card as plain text without markup (for prompts).
    /// </summary>
    public static string RenderPlain(Card card)
    {
        return $"{GetRankName(card.Rank)}{GetSuitSymbol(card.Suit)}";
    }

    /// <summary>
    /// Renders a list of cards grouped by suit.
    /// </summary>
    public static string RenderHand(IReadOnlyList<Card> hand, GameMode? gameMode = null)
    {
        var grouped = hand
            .GroupBy(c => c.Suit)
            .OrderBy(g => g.Key);

        var parts = new List<string>();
        foreach (var group in grouped)
        {
            var suitSymbol = GetSuitSymbol(group.Key);
            var isRed = group.Key is CardSuit.Hearts or CardSuit.Diamonds;
            var suitMarkup = isRed ? $"[red]{suitSymbol}[/]" : suitSymbol;

            var cardStrings = group
                .OrderByDescending(c => c.GetStrength(gameMode ?? GameMode.SansAs))
                .Select(c => Render(c, gameMode));

            parts.Add($"{suitMarkup}: {string.Join(" ", cardStrings)}");
        }

        return string.Join("  |  ", parts);
    }

    /// <summary>
    /// Renders a game mode with suit symbol if applicable.
    /// </summary>
    public static string RenderGameMode(GameMode mode)
    {
        var trumpSuit = mode.GetTrumpSuit();
        if (trumpSuit.HasValue)
        {
            var symbol = GetSuitSymbol(trumpSuit.Value);
            var isRed = trumpSuit.Value is CardSuit.Hearts or CardSuit.Diamonds;
            var suitMarkup = isRed ? $"[red]{symbol}[/]" : symbol;
            return $"{mode} {suitMarkup}";
        }

        return mode.ToString();
    }

    /// <summary>
    /// Renders a game mode as plain text.
    /// </summary>
    public static string RenderGameModePlain(GameMode mode)
    {
        var trumpSuit = mode.GetTrumpSuit();
        if (trumpSuit.HasValue)
        {
            return $"{mode} {GetSuitSymbol(trumpSuit.Value)}";
        }

        return mode.ToString();
    }
}
