namespace Giretra.Model.Enums;

// Numeric values must stay aligned with Giretra.Core's MultiplierState,
// which MatchPersistenceService casts from by int when persisting deals.
public enum MultiplierState
{
    Normal = 1,
    Doubled = 2,
    Redoubled = 4,
    ReRedoubled = 8
}
