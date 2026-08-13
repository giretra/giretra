using Giretra.Core.Cards;

namespace Giretra.Core.Players.Agents;

/// <summary>
/// Maps a cut of a known deck onto the cards each seat will be dealt, so an agent that tracks
/// the deck order (see <see cref="DeckTracker"/>) can pick its cut deliberately.
/// </summary>
public static class CutPlanner
{
    /// <summary>Number of cards in the deck.</summary>
    public const int DeckSize = 32;

    /// <summary>Smallest legal cut.</summary>
    public const int MinCutPosition = 6;

    /// <summary>Largest legal cut.</summary>
    public const int MaxCutPosition = 26;

    /// <summary>Cards held during negotiation (3 + 2 from the first two dealing rounds).</summary>
    public const int NegotiationHandSize = 5;

    /// <summary>Cards held once the final round has been dealt.</summary>
    public const int FullHandSize = 8;

    /// <summary>
    /// Gets every cut that produces a distinct deck, as a number of cards taken from the top.
    /// <para>
    /// Cutting moves the bottom portion on top, which is a rotation by
    /// <c>fromTop ? position : 32 - position</c>. Both forms cover 6..26, so cutting
    /// <c>p</c> cards from the top yields exactly the deck that cutting <c>32 - p</c> cards from
    /// the bottom yields: enumerating 6..26 from the top covers every reachable deck, and cutting
    /// from the bottom never adds an option.
    /// </para>
    /// </summary>
    public static IReadOnlyList<int> CandidateCutPositions { get; } =
        [.. Enumerable.Range(MinCutPosition, MaxCutPosition - MinCutPosition + 1)];

    /// <summary>
    /// Projects the eight cards a seat will hold after the deck is cut at the given position.
    /// </summary>
    /// <param name="deck">The full 32-card deck, in the order it will be cut.</param>
    /// <param name="dealer">The dealer of the deal, which fixes the dealing order.</param>
    /// <param name="player">The seat to project.</param>
    /// <param name="cutPosition">Cards taken from the top, between 6 and 26.</param>
    /// <returns>
    /// The cards in dealing order: the first three from the opening round, then two, then the
    /// three dealt after negotiation. Take the first <see cref="NegotiationHandSize"/> for the
    /// hand the seat negotiates with.
    /// </returns>
    public static IReadOnlyList<Card> ProjectHand(
        Deck deck,
        PlayerPosition dealer,
        PlayerPosition player,
        int cutPosition)
    {
        ArgumentNullException.ThrowIfNull(deck);

        if (deck.Count != DeckSize)
        {
            throw new ArgumentException($"A cut can only be projected on a full {DeckSize}-card deck.", nameof(deck));
        }

        if (cutPosition < MinCutPosition || cutPosition > MaxCutPosition)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cutPosition),
                cutPosition,
                $"Cut position must be between {MinCutPosition} and {MaxCutPosition}.");
        }

        // The cut rotates the deck, so slot s of the cut deck holds deck[(cutPosition + s) % 32].
        // Cards are dealt in play order (starting to the dealer's left) three each, then two each,
        // then three each after negotiation, which fixes the slots every seat receives.
        var seat = SeatIndex(dealer, player);
        var cards = new List<Card>(FullHandSize);

        AddSlots(cards, deck, cutPosition, firstSlot: 3 * seat, count: 3);
        AddSlots(cards, deck, cutPosition, firstSlot: 12 + 2 * seat, count: 2);
        AddSlots(cards, deck, cutPosition, firstSlot: 20 + 3 * seat, count: 3);

        return cards;
    }

    /// <summary>
    /// Gets a seat's index in the dealing order: 0 is dealt to first (the dealer's left),
    /// 3 is the dealer.
    /// </summary>
    public static int SeatIndex(PlayerPosition dealer, PlayerPosition player)
        => ((int)player - (int)dealer + 3) % 4;

    private static void AddSlots(List<Card> cards, Deck deck, int cutPosition, int firstSlot, int count)
    {
        for (var i = 0; i < count; i++)
        {
            cards.Add(deck[(cutPosition + firstSlot + i) % DeckSize]);
        }
    }
}
