namespace Giretra.Model.Enums;

// Numeric values must stay aligned with Giretra.Web's RecordedActionType,
// which MatchPersistenceService casts from by int when persisting actions.
public enum ActionType
{
    Cut = 0,
    Announce = 1,
    Accept = 2,
    Double = 3,
    Redouble = 4,
    ReRedouble = 5,
    PlayCard = 6
}
