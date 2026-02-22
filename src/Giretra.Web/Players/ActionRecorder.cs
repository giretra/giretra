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
}

public sealed class ActionRecorder
{
    private readonly Lock _lock = new();
    private readonly List<RecordedDeal> _completedDeals = [];
    private int _currentDealNumber;
    private PlayerPosition _currentDealerPosition;
    private List<RecordedAction> _currentActions = [];
    private int _actionOrder;

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
                    Actions = _currentActions.ToList()
                });
            }

            _currentDealNumber = dealNumber;
            _currentDealerPosition = dealerPosition;
            _currentActions = [];
            _actionOrder = 0;
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
                    Actions = _currentActions.ToList()
                });
            }
            return result;
        }
    }
}
