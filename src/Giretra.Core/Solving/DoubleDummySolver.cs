using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Core.State;

namespace Giretra.Core.Solving;

/// <summary>
/// Perfect-information ("double-dummy") solver for the playing phase: given all four hands,
/// computes the exact result of every valid card assuming perfect play by everyone afterwards.
/// </summary>
/// <remarks>
/// <para>
/// The solver maximises a team's final card points, with a sweep ranked above (and being swept below)
/// every other result. Match points never decrease when that score increases, so the best card for
/// card points is also the best card for match points.
/// </para>
/// <para>
/// The search is an alpha-beta minimax over bitmask hands with a transposition table keyed on trick
/// boundaries. The table only depends on the game mode, so one instance can evaluate many positions
/// (e.g. the 32 plays of a deal) and reuses earlier work. Instances are not thread-safe.
/// </para>
/// </remarks>
public sealed class DoubleDummySolver
{
    /// <summary>
    /// The score offset applied when a team wins all 8 tricks (see <see cref="SolvedOutcome.GetScore"/>).
    /// </summary>
    public const int SweepBonus = 1000;

    private const int Infinity = 30000;
    private const int MaxTableEntries = 4_000_000;
    private const int Team1HasTrick = 1;
    private const int Team2HasTrick = 2;

    // Cards are indexed as suit * 8 + (strength - 1), so within a suit a higher bit is a stronger card.
    private readonly int _trumpSuit;
    private readonly bool _allTrumps;
    private readonly int[] _points = new int[32];
    private readonly Card[] _cards = new Card[32];
    private readonly int[][] _suitPoints = new int[4][];
    private readonly Dictionary<TableKey, TableEntry> _table = new();

    // Search state, mutated with do/undo while recursing.
    private readonly uint[] _hands = new uint[4];
    private uint _trickMask;
    private int _flags;

    /// <summary>
    /// Gets the game mode this solver plays.
    /// </summary>
    public GameMode GameMode { get; }

    /// <summary>
    /// Gets the number of positions visited by the last call (for diagnostics).
    /// </summary>
    public long NodesVisited { get; private set; }

    /// <summary>
    /// Creates a solver for the given game mode.
    /// </summary>
    public DoubleDummySolver(GameMode gameMode)
    {
        GameMode = gameMode;
        _trumpSuit = gameMode.GetTrumpSuit() is { } trump ? (int)trump : -1;
        _allTrumps = gameMode.GetCategory() == GameModeCategory.AllTrumps;

        foreach (var card in Deck.CreateStandard().Cards)
        {
            var index = IndexOf(card);
            _cards[index] = card;
            _points[index] = card.GetPointValue(gameMode);
        }

        for (var suit = 0; suit < 4; suit++)
        {
            _suitPoints[suit] = new int[256];
            for (var bits = 0; bits < 256; bits++)
            {
                for (var rank = 0; rank < 8; rank++)
                {
                    if ((bits & (1 << rank)) != 0)
                    {
                        _suitPoints[suit][bits] += _points[suit * 8 + rank];
                    }
                }
            }
        }
    }

    /// <summary>
    /// Evaluates every valid card for the player to move.
    /// </summary>
    /// <param name="hand">The hand in progress (tricks played so far, current trick, captured points).</param>
    /// <param name="hands">The cards currently held by each player.</param>
    public PositionEvaluation Evaluate(HandState hand, IReadOnlyDictionary<PlayerPosition, IReadOnlyList<Card>> hands)
    {
        var root = LoadPosition(hand, hands);
        var mover = (PlayerPosition)root.Player;
        var team = mover.GetTeam();

        var legal = LegalMoves(root.Player, root.Played, root.LeadSuit, root.WinCard, root.WinPlayer);
        var evaluations = new List<CardEvaluation>();

        while (legal != 0)
        {
            var card = LowestBit(legal);
            legal &= legal - 1;

            var value = PlayMove(root.Player, root.Played, root.LeadSuit, root.WinCard, root.WinPlayer,
                root.TrickPoints, card, -Infinity, Infinity);
            var outcome = ToOutcome(hand, value);
            evaluations.Add(new CardEvaluation(_cards[card], outcome, outcome.GetScore(team), 0, false));
        }

        evaluations.Sort((a, b) => b.Score.CompareTo(a.Score));

        var best = evaluations[0];
        for (var i = 0; i < evaluations.Count; i++)
        {
            var evaluation = evaluations[i];
            evaluations[i] = evaluation with
            {
                CardPointLoss = best.Outcome.GetCardPoints(team) - evaluation.Outcome.GetCardPoints(team),
                IsOptimal = evaluation.Score == best.Score
            };
        }

        return new PositionEvaluation(GameMode, mover, evaluations);
    }

    /// <summary>
    /// Evaluates every valid card for the player to move in a deal being played.
    /// </summary>
    public PositionEvaluation Evaluate(DealState deal)
    {
        if (deal.Hand is null)
        {
            throw new ArgumentException("The deal is not in the playing phase.", nameof(deal));
        }

        return Evaluate(deal.Hand, HandsOf(deal));
    }

    /// <summary>
    /// Computes only the result of perfect play from a position.
    /// Faster than <see cref="Evaluate(HandState, IReadOnlyDictionary{PlayerPosition, IReadOnlyList{Card}})"/>
    /// when the per-card values are not needed.
    /// </summary>
    public SolvedOutcome Solve(HandState hand, IReadOnlyDictionary<PlayerPosition, IReadOnlyList<Card>> hands)
    {
        var root = LoadPosition(hand, hands);
        var value = Search(root.Player, root.Played, root.LeadSuit, root.WinCard, root.WinPlayer,
            root.TrickPoints, -Infinity, Infinity);
        return ToOutcome(hand, value);
    }

    /// <summary>
    /// Discards the transposition table.
    /// </summary>
    public void Clear() => _table.Clear();

    internal static IReadOnlyDictionary<PlayerPosition, IReadOnlyList<Card>> HandsOf(DealState deal)
        => deal.Players.ToDictionary(p => p.Key, p => (IReadOnlyList<Card>)p.Value.Hand);

    #region Position setup

    private readonly struct RootNode
    {
        public readonly int Player, Played, LeadSuit, WinCard, WinPlayer, TrickPoints;

        public RootNode(int player, int played, int leadSuit, int winCard, int winPlayer, int trickPoints)
        {
            Player = player;
            Played = played;
            LeadSuit = leadSuit;
            WinCard = winCard;
            WinPlayer = winPlayer;
            TrickPoints = trickPoints;
        }
    }

    private RootNode LoadPosition(HandState hand, IReadOnlyDictionary<PlayerPosition, IReadOnlyList<Card>> hands)
    {
        if (hand.GameMode != GameMode)
        {
            throw new ArgumentException($"This solver plays {GameMode}, not {hand.GameMode}.", nameof(hand));
        }

        var trick = hand.CurrentTrick
                    ?? throw new ArgumentException("The hand is already complete.", nameof(hand));

        NodesVisited = 0;
        if (_table.Count > MaxTableEntries)
        {
            _table.Clear();
        }

        // Load the hands
        uint seen = 0;
        for (var player = 0; player < 4; player++)
        {
            _hands[player] = 0;
            if (!hands.TryGetValue((PlayerPosition)player, out var cards)) continue;

            foreach (var card in cards)
            {
                var bit = 1u << IndexOf(card);
                if ((seen & bit) != 0)
                {
                    throw new ArgumentException($"{card} appears more than once.", nameof(hands));
                }

                seen |= bit;
                _hands[player] |= bit;
            }
        }

        // Replay the trick in progress
        _trickMask = 0;
        int leadSuit = 0, winCard = 0, winPlayer = 0, trickPoints = 0;
        for (var i = 0; i < trick.PlayedCards.Count; i++)
        {
            var played = trick.PlayedCards[i];
            var card = IndexOf(played.Card);
            var bit = 1u << card;
            if ((seen & bit) != 0)
            {
                throw new ArgumentException($"{played.Card} is both in a hand and in the current trick.", nameof(hands));
            }

            seen |= bit;
            _trickMask |= bit;
            trickPoints += _points[card];

            if (i == 0)
            {
                leadSuit = card >> 3;
                winCard = card;
                winPlayer = (int)played.Player;
            }
            else if (Beats(card, winCard))
            {
                winCard = card;
                winPlayer = (int)played.Player;
            }
        }

        // Players who already played to the current trick hold one card fewer
        var mover = (int)trick.CurrentPlayer!.Value;
        var expected = PopCount(_hands[mover]);
        if (expected == 0)
        {
            throw new ArgumentException($"{trick.CurrentPlayer} has no cards to play.", nameof(hands));
        }

        for (var player = 0; player < 4; player++)
        {
            var hasPlayed = trick.PlayedCards.Any(pc => (int)pc.Player == player);
            if (PopCount(_hands[player]) != (hasPlayed ? expected - 1 : expected))
            {
                throw new ArgumentException("Hand sizes are inconsistent with the current trick.", nameof(hands));
            }
        }

        _flags = (hand.Team1TricksWon > 0 ? Team1HasTrick : 0) | (hand.Team2TricksWon > 0 ? Team2HasTrick : 0);

        return new RootNode(mover, trick.PlayedCards.Count, leadSuit, winCard, winPlayer, trickPoints);
    }

    private static SolvedOutcome ToOutcome(HandState hand, int value)
    {
        Team? sweepingTeam = null;
        if (value >= SweepBonus)
        {
            sweepingTeam = Team.Team1;
            value -= SweepBonus;
        }
        else if (value < 0)
        {
            sweepingTeam = Team.Team2;
            value += SweepBonus;
        }

        var total = hand.GameMode.GetTotalPoints();
        var team1 = hand.Team1CardPoints + value;
        return new SolvedOutcome(team1, total - team1, sweepingTeam);
    }

    private int IndexOf(Card card)
        => (int)card.Suit * 8 + card.GetStrength(GameMode) - 1;

    #endregion

    #region Search

    /// <summary>
    /// Returns the card points Team1 still captures from this position (current trick included),
    /// plus or minus <see cref="SweepBonus"/> if the hand ends in a sweep. Team1 maximises, Team2 minimises.
    /// </summary>
    private int Search(int player, int played, int leadSuit, int winCard, int winPlayer, int trickPoints,
        int alpha, int beta)
    {
        NodesVisited++;

        var atTrickStart = played == 0;
        var key = default(TableKey);
        var tableMove = -1;

        if (atTrickStart)
        {
            var all = _hands[0] | _hands[1] | _hands[2] | _hands[3];
            if (all == 0)
            {
                return _flags == Team1HasTrick ? SweepBonus : _flags == Team2HasTrick ? -SweepBonus : 0;
            }

            // Whatever happens, Team1 gets between nothing and everything that is left
            var max = PointsOf(all) + 10 + ((_flags & Team2HasTrick) == 0 ? SweepBonus : 0);
            var min = (_flags & Team1HasTrick) == 0 ? -SweepBonus : 0;
            if (max <= alpha) return max;
            if (min >= beta) return min;

            key = new TableKey(_hands, player, _flags);
            if (_table.TryGetValue(key, out var entry))
            {
                if (entry.Lower >= beta) return entry.Lower;
                if (entry.Upper <= alpha) return entry.Upper;
                if (entry.Lower == entry.Upper) return entry.Lower;
                if (entry.Lower > alpha) alpha = entry.Lower;
                if (entry.Upper < beta) beta = entry.Upper;
                tableMove = entry.Move;
            }
        }

        var legal = LegalMoves(player, played, leadSuit, winCard, winPlayer);

        Span<int> moves = stackalloc int[8];
        var count = OrderMoves(legal, player, played, winCard, winPlayer, tableMove, moves);

        var maximizing = (player & 1) == 0;
        var alpha0 = alpha;
        var beta0 = beta;
        var best = maximizing ? -Infinity : Infinity;
        var bestMove = moves[0];

        for (var i = 0; i < count; i++)
        {
            var value = PlayMove(player, played, leadSuit, winCard, winPlayer, trickPoints, moves[i], alpha, beta);

            if (maximizing)
            {
                if (value > best)
                {
                    best = value;
                    bestMove = moves[i];
                }

                if (best > alpha) alpha = best;
            }
            else
            {
                if (value < best)
                {
                    best = value;
                    bestMove = moves[i];
                }

                if (best < beta) beta = best;
            }

            if (alpha >= beta) break;
        }

        if (atTrickStart)
        {
            if (!_table.TryGetValue(key, out var entry))
            {
                entry = new TableEntry(-Infinity, Infinity, bestMove);
            }

            var lower = best > alpha0 ? Math.Max(entry.Lower, best) : entry.Lower;
            var upper = best < beta0 ? Math.Min(entry.Upper, best) : entry.Upper;
            _table[key] = new TableEntry(lower, upper, bestMove);
        }

        return best;
    }

    private int PlayMove(int player, int played, int leadSuit, int winCard, int winPlayer, int trickPoints,
        int card, int alpha, int beta)
    {
        var bit = 1u << card;
        var points = trickPoints + _points[card];

        if (played == 0)
        {
            leadSuit = card >> 3;
            winCard = card;
            winPlayer = player;
        }
        else if (Beats(card, winCard))
        {
            winCard = card;
            winPlayer = player;
        }

        _hands[player] &= ~bit;
        int result;

        if (played < 3)
        {
            _trickMask |= bit;
            result = Search((player + 1) & 3, played + 1, leadSuit, winCard, winPlayer, points, alpha, beta);
            _trickMask &= ~bit;
        }
        else
        {
            var savedMask = _trickMask;
            var savedFlags = _flags;
            _trickMask = 0;

            // The fourth player to the last trick empties the last hand
            if (_hands[player] == 0) points += 10;

            int gain;
            if ((winPlayer & 1) == 0)
            {
                gain = points;
                _flags |= Team1HasTrick;
            }
            else
            {
                gain = 0;
                _flags |= Team2HasTrick;
            }

            result = gain + Search(winPlayer, 0, 0, 0, 0, 0, alpha - gain, beta - gain);

            _flags = savedFlags;
            _trickMask = savedMask;
        }

        _hands[player] |= bit;
        return result;
    }

    /// <summary>
    /// Mirrors <see cref="Play.PlayValidator"/> on bitmasks.
    /// </summary>
    private uint LegalMoves(int player, int played, int leadSuit, int winCard, int winPlayer)
    {
        var hand = _hands[player];
        if (played == 0) return hand;

        var follow = hand & SuitMask(leadSuit);
        if (follow != 0)
        {
            // Must play higher when following in AllTrumps or when trump is led;
            // in both cases the winning card is necessarily of the lead suit
            if (_allTrumps || leadSuit == _trumpSuit)
            {
                var higher = follow & Above(winCard);
                if (higher != 0) return higher;
            }

            return follow;
        }

        if (_trumpSuit < 0) return hand;

        var trumps = hand & SuitMask(_trumpSuit);
        if (trumps == 0) return hand;

        // A trump in the trick is always the winning card: must overtrump if possible, else undertrump
        if (winCard >> 3 == _trumpSuit)
        {
            var higher = trumps & Above(winCard);
            return higher != 0 ? higher : trumps;
        }

        // Teammate winning with a non-trump: free to discard
        if (((winPlayer ^ player) & 1) == 0) return hand;

        return trumps;
    }

    /// <summary>
    /// Fills <paramref name="moves"/> with the cards worth searching, most promising first.
    /// Cards that are interchangeable with a lower one (adjacent among the cards still in play,
    /// same hand, same point value) are dropped.
    /// </summary>
    private int OrderMoves(uint legal, int player, int played, int winCard, int winPlayer, int tableMove, Span<int> moves)
    {
        Span<int> scores = stackalloc int[8];
        var others = (_hands[0] | _hands[1] | _hands[2] | _hands[3]) & ~_hands[player];
        var live = others | _hands[player] | _trickMask;
        var partnerWinning = played > 0 && ((winPlayer ^ player) & 1) == 0;

        var count = 0;
        var previous = -1;

        while (legal != 0)
        {
            var card = LowestBit(legal);
            legal &= legal - 1;

            if (previous >= 0
                && previous >> 3 == card >> 3
                && _points[previous] == _points[card]
                && (live & Between(previous, card)) == 0
                && card != tableMove)
            {
                continue;
            }

            previous = card;

            int score;
            if (card == tableMove)
            {
                score = 10000;
            }
            else if (played == 0)
            {
                var isMaster = (others & SuitMask(card >> 3) & Above(card)) == 0;
                score = isMaster ? 100 + _points[card] : -_points[card];
            }
            else if (Beats(card, winCard))
            {
                score = played == 3 ? 100 + _points[card] : 50 + (card & 7);
            }
            else if (partnerWinning)
            {
                score = (played == 3 ? 100 : 20) + _points[card];
            }
            else
            {
                score = -_points[card];
            }

            // Insertion sort, best first
            var slot = count++;
            while (slot > 0 && scores[slot - 1] < score)
            {
                scores[slot] = scores[slot - 1];
                moves[slot] = moves[slot - 1];
                slot--;
            }

            scores[slot] = score;
            moves[slot] = card;
        }

        return count;
    }

    private bool Beats(int card, int winCard)
    {
        var suit = card >> 3;
        var winSuit = winCard >> 3;
        return suit == winSuit ? card > winCard : suit == _trumpSuit;
    }

    private int PointsOf(uint mask)
        => _suitPoints[0][mask & 0xFF]
           + _suitPoints[1][(mask >> 8) & 0xFF]
           + _suitPoints[2][(mask >> 16) & 0xFF]
           + _suitPoints[3][mask >> 24];

    #endregion

    #region Bit helpers

    private static uint SuitMask(int suit) => 0xFFu << (suit * 8);

    /// <summary>Cards of the same suit that are stronger than <paramref name="card"/>.</summary>
    private static uint Above(int card) => SuitMask(card >> 3) & ~((2u << card) - 1);

    /// <summary>Card indexes strictly between two cards (low &lt; high).</summary>
    private static uint Between(int low, int high) => ((1u << high) - 1) & ~((2u << low) - 1);

    private static int LowestBit(uint mask)
    {
#if NETSTANDARD2_1
        return PopCount((mask & (~mask + 1)) - 1);
#else
        return System.Numerics.BitOperations.TrailingZeroCount(mask);
#endif
    }

    private static int PopCount(uint mask)
    {
#if NETSTANDARD2_1
        mask -= (mask >> 1) & 0x55555555u;
        mask = (mask & 0x33333333u) + ((mask >> 2) & 0x33333333u);
        return (int)((((mask + (mask >> 4)) & 0x0F0F0F0Fu) * 0x01010101u) >> 24);
#else
        return System.Numerics.BitOperations.PopCount(mask);
#endif
    }

    #endregion

    #region Transposition table

    private readonly struct TableKey : IEquatable<TableKey>
    {
        private readonly ulong _a;
        private readonly ulong _b;
        private readonly int _meta;

        public TableKey(uint[] hands, int leader, int flags)
        {
            _a = hands[0] | ((ulong)hands[1] << 32);
            _b = hands[2] | ((ulong)hands[3] << 32);
            _meta = leader | (flags << 2);
        }

        public bool Equals(TableKey other) => _a == other._a && _b == other._b && _meta == other._meta;

        public override bool Equals(object? obj) => obj is TableKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = _a * 0x9E3779B97F4A7C15UL;
                hash ^= (_b + (ulong)_meta) * 0xC2B2AE3D27D4EB4FUL;
                hash ^= hash >> 29;
                return (int)(hash ^ (hash >> 32));
            }
        }
    }

    private readonly struct TableEntry
    {
        public readonly short Lower;
        public readonly short Upper;
        public readonly sbyte Move;

        public TableEntry(int lower, int upper, int move)
        {
            Lower = (short)lower;
            Upper = (short)upper;
            Move = (sbyte)move;
        }
    }

    #endregion
}
