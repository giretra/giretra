using Giretra.Core.GameModes;

namespace Giretra.Core.Cards;

/// <summary>
/// Represents an immutable playing card with rank and suit.
/// </summary>
public readonly record struct Card(CardRank Rank, CardSuit Suit) : IComparable<Card>
{
    /// <summary>
    /// Gets the point value of this card in the given game mode.
    /// </summary>
    public int GetPointValue(GameMode gameMode)
    {
        var useTrumpValues = gameMode.GetCategory() == GameModeCategory.AllTrumps ||
                             (gameMode.GetTrumpSuit() == Suit);

        return useTrumpValues ? GetTrumpPointValue() : GetNonTrumpPointValue();
    }

    /// <summary>
    /// Gets the point value using trump/AllTrumps scoring.
    /// J=20, 9=14, A=11, 10=10, K=4, Q=3, 8=0, 7=0
    /// </summary>
    private int GetTrumpPointValue()
        => Rank switch
        {
            CardRank.Jack => 20,
            CardRank.Nine => 14,
            CardRank.Ace => 11,
            CardRank.Ten => 10,
            CardRank.King => 4,
            CardRank.Queen => 3,
            CardRank.Eight => 0,
            CardRank.Seven => 0,
            _ => 0
        };

    /// <summary>
    /// Gets the point value using non-trump/NoTrumps scoring.
    /// A=11, 10=10, K=4, Q=3, J=2, 9=0, 8=0, 7=0
    /// </summary>
    private int GetNonTrumpPointValue()
        => Rank switch
        {
            CardRank.Ace => 11,
            CardRank.Ten => 10,
            CardRank.King => 4,
            CardRank.Queen => 3,
            CardRank.Jack => 2,
            CardRank.Nine => 0,
            CardRank.Eight => 0,
            CardRank.Seven => 0,
            _ => 0
        };

    /// <summary>
    /// Gets the strength of this card for comparison purposes.
    /// Higher values are stronger cards.
    /// </summary>
    public int GetStrength(GameMode gameMode)
    {
        var useTrumpRanking = gameMode.GetCategory() == GameModeCategory.AllTrumps ||
                              (gameMode.GetTrumpSuit() == Suit);

        return useTrumpRanking ? GetTrumpStrength() : GetNonTrumpStrength();
    }

    /// <summary>
    /// Gets strength using trump/AllTrumps ranking: J > 9 > A > 10 > K > Q > 8 > 7
    /// </summary>
    private int GetTrumpStrength()
        => Rank switch
        {
            CardRank.Jack => 8,
            CardRank.Nine => 7,
            CardRank.Ace => 6,
            CardRank.Ten => 5,
            CardRank.King => 4,
            CardRank.Queen => 3,
            CardRank.Eight => 2,
            CardRank.Seven => 1,
            _ => 0
        };

    /// <summary>
    /// Gets strength using non-trump/NoTrumps ranking: A > 10 > K > Q > J > 9 > 8 > 7
    /// </summary>
    private int GetNonTrumpStrength()
        => Rank switch
        {
            CardRank.Ace => 8,
            CardRank.Ten => 7,
            CardRank.King => 6,
            CardRank.Queen => 5,
            CardRank.Jack => 4,
            CardRank.Nine => 3,
            CardRank.Eight => 2,
            CardRank.Seven => 1,
            _ => 0
        };

    /// <summary>
    /// Default comparison by suit then rank (for display/sorting purposes).
    /// </summary>
    public int CompareTo(Card other)
    {
        var suitComparison = Suit.CompareTo(other.Suit);
        return suitComparison != 0 ? suitComparison : Rank.CompareTo(other.Rank);
    }

    public override string ToString()
    {
        var rankStr = Rank switch
        {
            CardRank.Ace => "A",
            CardRank.King => "K",
            CardRank.Queen => "Q",
            CardRank.Jack => "J",
            _ => ((int)Rank).ToString()
        };

        var suitStr = Suit switch
        {
            CardSuit.Clubs => "♣",
            CardSuit.Diamonds => "♦",
            CardSuit.Hearts => "♥",
            CardSuit.Spades => "♠",
            _ => "?"
        };

        return $"{rankStr}{suitStr}";
    }
}
