using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Players;

namespace Giretra.Web.Players;

public enum RecordedActionType
{
    Cut,
    Announce,
    Accept,
    Double,
    Redouble,
    ReRedouble,
    PlayCard
}

public sealed record RecordedAction
{
    public required int ActionOrder { get; init; }
    public required RecordedActionType ActionType { get; init; }
    public required PlayerPosition PlayerPosition { get; init; }
    public CardRank? CardRank { get; init; }
    public CardSuit? CardSuit { get; init; }
    public GameMode? GameMode { get; init; }
    public int? CutPosition { get; init; }
    public bool? CutFromTop { get; init; }
    public int? TrickNumber { get; init; }
}

public sealed record RecordedDeal
{
    public required int DealNumber { get; init; }
    public required PlayerPosition DealerPosition { get; init; }
    public required IReadOnlyList<RecordedAction> Actions { get; init; }
    public required IReadOnlyDictionary<PlayerPosition, IReadOnlyList<Card>> InitialHands { get; init; }
    public required IReadOnlyDictionary<PlayerPosition, IReadOnlyList<Card>> FullHands { get; init; }
}

public sealed class ActionRecorder
{
    private readonly Lock _lock = new();
    private readonly List<RecordedDeal> _completedDeals = [];
    private int _currentDealNumber;
    private PlayerPosition _currentDealerPosition;
    private List<RecordedAction> _currentActions = [];
    private int _actionOrder;
    private Dictionary<PlayerPosition, IReadOnlyList<Card>> _currentInitialHands = [];
    private Dictionary<PlayerPosition, IReadOnlyList<Card>> _currentFullHands = [];

    public void StartDeal(int dealNumber, PlayerPosition dealerPosition)
    {
        lock (_lock)
        {
            // Finalize any previous deal
            if (_currentActions.Count > 0)
            {
                _completedDeals.Add(new RecordedDeal
                {
                    DealNumber = _currentDealNumber,
                    DealerPosition = _currentDealerPosition,
                    Actions = _currentActions.ToList(),
                    InitialHands = _currentInitialHands,
                    FullHands = _currentFullHands
                });
            }

            _currentDealNumber = dealNumber;
            _currentDealerPosition = dealerPosition;
            _currentActions = [];
            _actionOrder = 0;
            _currentInitialHands = [];
            _currentFullHands = [];
        }
    }

    public void RecordInitialHand(PlayerPosition player, IReadOnlyList<Card> hand)
    {
        lock (_lock)
        {
            _currentInitialHands[player] = hand.ToList();
        }
    }

    public void RecordFullHand(PlayerPosition player, IReadOnlyList<Card> hand)
    {
        lock (_lock)
        {
            _currentFullHands[player] = hand.ToList();
        }
    }

    public void RecordCut(PlayerPosition player, int position, bool fromTop)
    {
        lock (_lock)
        {
            _currentActions.Add(new RecordedAction
            {
                ActionOrder = _actionOrder++,
                ActionType = RecordedActionType.Cut,
                PlayerPosition = player,
                CutPosition = position,
                CutFromTop = fromTop
            });
        }
    }

    public void RecordNegotiation(PlayerPosition player, RecordedActionType actionType, GameMode? gameMode = null)
    {
        lock (_lock)
        {
            _currentActions.Add(new RecordedAction
            {
                ActionOrder = _actionOrder++,
                ActionType = actionType,
                PlayerPosition = player,
                GameMode = gameMode
            });
        }
    }

    public void RecordCardPlay(PlayerPosition player, Card card, int trickNumber)
    {
        lock (_lock)
        {
            _currentActions.Add(new RecordedAction
            {
                ActionOrder = _actionOrder++,
                ActionType = RecordedActionType.PlayCard,
                PlayerPosition = player,
                CardRank = card.Rank,
                CardSuit = card.Suit,
                TrickNumber = trickNumber
            });
        }
    }

    public List<RecordedDeal> GetDeals()
    {
        lock (_lock)
        {
            // Include the current in-progress deal if it has actions
            var result = new List<RecordedDeal>(_completedDeals);
            if (_currentActions.Count > 0)
            {
                result.Add(new RecordedDeal
                {
                    DealNumber = _currentDealNumber,
                    DealerPosition = _currentDealerPosition,
                    Actions = _currentActions.ToList(),
                    InitialHands = _currentInitialHands,
                    FullHands = _currentFullHands
                });
            }
            return result;
        }
    }
}
