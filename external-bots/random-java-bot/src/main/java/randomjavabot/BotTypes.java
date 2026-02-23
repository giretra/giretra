// BotTypes.java — Type definitions for the Giretra bot API.
//
// JSON is serialized with camelCase — Jackson handles this automatically.
// Java records provide immutable data types with minimal boilerplate.

package randomjavabot;

import java.util.List;
import java.util.Map;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import com.fasterxml.jackson.annotation.JsonInclude;

// ─── Cards ──────────────────────────────────────────────────────────

enum Rank { Seven, Eight, Nine, Ten, Jack, Queen, King, Ace }
enum Suit { Clubs, Diamonds, Hearts, Spades }

@JsonIgnoreProperties(ignoreUnknown = true)
record Card(Rank rank, Suit suit) {}

// ─── Players & Teams ────────────────────────────────────────────────

enum PlayerPosition { Bottom, Left, Top, Right }
enum Team { Team1, Team2 }

@JsonIgnoreProperties(ignoreUnknown = true)
record PlayedCard(PlayerPosition player, Card card) {}

// ─── Game Modes ─────────────────────────────────────────────────────

enum GameMode {
    ColourClubs, ColourDiamonds, ColourHearts, ColourSpades,
    NoTrumps, AllTrumps
}

enum Multiplier { Normal, Doubled, Redoubled }

// ─── Trick ──────────────────────────────────────────────────────────

@JsonIgnoreProperties(ignoreUnknown = true)
record TrickState(
    PlayerPosition leader,
    int trickNumber,
    List<PlayedCard> playedCards,
    boolean isComplete
) {}

// ─── Hand State ─────────────────────────────────────────────────────

@JsonIgnoreProperties(ignoreUnknown = true)
record HandState(
    GameMode gameMode,
    int team1CardPoints,
    int team2CardPoints,
    int team1TricksWon,
    int team2TricksWon,
    TrickState currentTrick,
    List<TrickState> completedTricks
) {}

// ─── Negotiation ────────────────────────────────────────────────────

enum NegotiationActionType { Announcement, Accept, Double, Redouble }

@JsonIgnoreProperties(ignoreUnknown = true)
@JsonInclude(JsonInclude.Include.NON_NULL)
record NegotiationAction(
    NegotiationActionType type,
    PlayerPosition player,
    GameMode mode,
    GameMode targetMode
) {}

/** Valid action choice (no player field — the server knows who you are). */
@JsonIgnoreProperties(ignoreUnknown = true)
@JsonInclude(JsonInclude.Include.NON_NULL)
record NegotiationActionChoice(
    NegotiationActionType type,
    GameMode mode,
    GameMode targetMode
) {}

@JsonIgnoreProperties(ignoreUnknown = true)
record NegotiationState(
    PlayerPosition dealer,
    PlayerPosition currentPlayer,
    GameMode currentBid,
    PlayerPosition currentBidder,
    int consecutiveAccepts,
    boolean hasDoubleOccurred,
    List<NegotiationAction> actions,
    Map<String, Boolean> doubledModes,
    List<String> redoubledModes,
    Map<String, String> teamColourAnnouncements
) {}

// ─── Scoring ────────────────────────────────────────────────────────

@JsonIgnoreProperties(ignoreUnknown = true)
record DealResult(
    GameMode gameMode,
    Multiplier multiplier,
    Team announcerTeam,
    int team1CardPoints,
    int team2CardPoints,
    int team1MatchPoints,
    int team2MatchPoints,
    boolean wasSweep,
    Team sweepingTeam,
    boolean isInstantWin
) {}

// ─── Match State ────────────────────────────────────────────────────

@JsonIgnoreProperties(ignoreUnknown = true)
record MatchState(
    int targetScore,
    int team1MatchPoints,
    int team2MatchPoints,
    PlayerPosition currentDealer,
    boolean isComplete,
    Team winner,
    List<DealResult> completedDeals
) {}

// ─── Session (internal — used by Server.java for deserialization) ───

@JsonIgnoreProperties(ignoreUnknown = true)
record SessionRequest(PlayerPosition position, String matchId) {}

// ─── Bot Contexts (passed to your methods) ──────────────────────────

@JsonIgnoreProperties(ignoreUnknown = true)
record ChooseCutContext(int deckSize, MatchState matchState) {}

@JsonInclude(JsonInclude.Include.NON_NULL)
record CutResult(int position, boolean fromTop) {}

@JsonIgnoreProperties(ignoreUnknown = true)
record ChooseNegotiationActionContext(
    List<Card> hand,
    NegotiationState negotiationState,
    MatchState matchState,
    List<NegotiationActionChoice> validActions
) {}

@JsonIgnoreProperties(ignoreUnknown = true)
record ChooseCardContext(
    List<Card> hand,
    HandState handState,
    MatchState matchState,
    List<Card> validPlays
) {}

@JsonIgnoreProperties(ignoreUnknown = true)
record DealStartedContext(MatchState matchState) {}

@JsonIgnoreProperties(ignoreUnknown = true)
record CardPlayedContext(
    PlayerPosition player,
    Card card,
    HandState handState,
    MatchState matchState
) {}

@JsonIgnoreProperties(ignoreUnknown = true)
record TrickCompletedContext(
    TrickState completedTrick,
    PlayerPosition winner,
    HandState handState,
    MatchState matchState
) {}

@JsonIgnoreProperties(ignoreUnknown = true)
record DealEndedContext(
    DealResult result,
    HandState handState,
    MatchState matchState
) {}

@JsonIgnoreProperties(ignoreUnknown = true)
record MatchEndedContext(MatchState matchState) {}
