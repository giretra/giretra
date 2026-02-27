// BotTypes.java — Type definitions for the Giretra bot API.
//
// These types mirror the JSON payloads exchanged between the game server and your bot.
// JSON is serialized with camelCase — Jackson handles this automatically.
// Java records provide immutable data types with minimal boilerplate.
//
// You should NOT need to edit this file. All game logic belongs in Bot.java.

package randomjavabot;

import java.util.List;
import java.util.Map;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import com.fasterxml.jackson.annotation.JsonInclude;

// ─── Cards ──────────────────────────────────────────────────────────

/**
 * Card ranks from lowest to highest.
 *
 * <p>Strength order depends on game mode:
 * <ul>
 *   <li>Trump / AllTrumps: J &gt; 9 &gt; A &gt; 10 &gt; K &gt; Q &gt; 8 &gt; 7</li>
 *   <li>Non-trump / NoTrumps: A &gt; 10 &gt; K &gt; Q &gt; J &gt; 9 &gt; 8 &gt; 7</li>
 * </ul>
 */
enum Rank { Seven, Eight, Nine, Ten, Jack, Queen, King, Ace }

/** Card suits. Also determines Colour game modes (ColourClubs, ColourDiamonds, etc.). */
enum Suit { Clubs, Diamonds, Hearts, Spades }

/**
 * A playing card with a rank and suit.
 *
 * <p>Point values depend on game mode:
 * <table>
 *   <tr><th>Card</th><th>Trump/AllTrumps</th><th>Non-trump/NoTrumps</th></tr>
 *   <tr><td>Jack</td><td>20</td><td>2</td></tr>
 *   <tr><td>Nine</td><td>14</td><td>0</td></tr>
 *   <tr><td>Ace</td><td>11</td><td>11</td></tr>
 *   <tr><td>Ten</td><td>10</td><td>10</td></tr>
 *   <tr><td>King</td><td>4</td><td>4</td></tr>
 *   <tr><td>Queen</td><td>3</td><td>3</td></tr>
 *   <tr><td>Eight, Seven</td><td>0</td><td>0</td></tr>
 * </table>
 */
@JsonIgnoreProperties(ignoreUnknown = true)
record Card(Rank rank, Suit suit) {}

// ─── Players & Teams ────────────────────────────────────────────────

/**
 * Seat positions around the table (clockwise: Bottom → Left → Top → Right).
 * Your bot is always {@code Bottom}.
 */
enum PlayerPosition { Bottom, Left, Top, Right }

/**
 * Team1 = Bottom + Top (you and your partner).
 * Team2 = Left + Right (opponents).
 */
enum Team { Team1, Team2 }

/** A card played by a specific player in a trick. */
@JsonIgnoreProperties(ignoreUnknown = true)
record PlayedCard(
    /** The player who played the card. */
    PlayerPosition player,
    /** The card that was played. */
    Card card
) {}

// ─── Game Modes ─────────────────────────────────────────────────────

/**
 * Game modes ordered from lowest to highest bid.
 * During negotiation you can only announce a mode higher than the current bid.
 */
enum GameMode {
    ColourClubs, ColourDiamonds, ColourHearts, ColourSpades,
    NoTrumps, AllTrumps
}

/**
 * Scoring multiplier for a deal.
 * Normal = x1, Doubled = x2, Redoubled = x4.
 */
enum Multiplier { Normal, Doubled, Redoubled }

// ─── Trick ──────────────────────────────────────────────────────────

/** State of a single trick (4 cards, one per player). */
@JsonIgnoreProperties(ignoreUnknown = true)
record TrickState(
    /** The player who led this trick (played first). */
    PlayerPosition leader,
    /** 1-based trick number within the deal (1–8). */
    int trickNumber,
    /** Cards played so far in this trick (0–4 cards, in play order). */
    List<PlayedCard> playedCards,
    /** True when all 4 cards have been played. */
    boolean isComplete
) {}

// ─── Hand State ─────────────────────────────────────────────────────

/**
 * State of the current deal's play phase (after negotiation).
 *
 * <p>Total card points per mode: AllTrumps = 258, Colour = 162, NoTrumps = 130.
 */
@JsonIgnoreProperties(ignoreUnknown = true)
record HandState(
    /** The game mode in effect for this deal. */
    GameMode gameMode,
    /** Card points accumulated by Team1 (Bottom + Top) so far. */
    int team1CardPoints,
    /** Card points accumulated by Team2 (Left + Right) so far. */
    int team2CardPoints,
    /** Number of tricks won by Team1 so far (0–8). */
    int team1TricksWon,
    /** Number of tricks won by Team2 so far (0–8). */
    int team2TricksWon,
    /** The trick currently being played (null between tricks). */
    TrickState currentTrick,
    /** All tricks completed so far in this deal. */
    List<TrickState> completedTricks
) {}

// ─── Negotiation ────────────────────────────────────────────────────

/** The type of action taken during negotiation. */
enum NegotiationActionType { Announcement, Accept, Double, Redouble }

/**
 * A negotiation action from the history (includes the player who took it).
 */
@JsonIgnoreProperties(ignoreUnknown = true)
@JsonInclude(JsonInclude.Include.NON_NULL)
record NegotiationAction(
    /** What type of action was taken. */
    NegotiationActionType type,
    /** The player who took this action (present in history, absent in choices). */
    PlayerPosition player,
    /** The game mode being announced (only for Announcement). */
    GameMode mode,
    /** The mode being doubled/redoubled (only for Double and Redouble). */
    GameMode targetMode
) {}

/**
 * A valid action you can choose during negotiation.
 * Same shape as {@link NegotiationAction} but without the player field
 * (the server knows who you are).
 */
@JsonIgnoreProperties(ignoreUnknown = true)
@JsonInclude(JsonInclude.Include.NON_NULL)
record NegotiationActionChoice(
    NegotiationActionType type,
    GameMode mode,
    GameMode targetMode
) {}

/**
 * Full state of the negotiation (bidding) phase.
 * Negotiation ends after 3 consecutive Accepts.
 */
@JsonIgnoreProperties(ignoreUnknown = true)
record NegotiationState(
    /** The dealer for this deal. */
    PlayerPosition dealer,
    /** Whose turn it is to act. */
    PlayerPosition currentPlayer,
    /** The highest bid so far, or null if no one has announced yet. */
    GameMode currentBid,
    /** The player who made the current highest bid. */
    PlayerPosition currentBidder,
    /** Number of consecutive accepts (negotiation ends at 3). */
    int consecutiveAccepts,
    /** Whether any double has occurred in this negotiation. */
    boolean hasDoubleOccurred,
    /** Full history of all actions taken in this negotiation. */
    List<NegotiationAction> actions,
    /** Which game modes have been doubled (mode → true/false). */
    Map<String, Boolean> doubledModes,
    /** Game modes that have been redoubled. */
    List<String> redoubledModes,
    /** Each team's Colour announcement this deal (max one Colour per team). */
    Map<String, String> teamColourAnnouncements
) {}

// ─── Scoring ────────────────────────────────────────────────────────

/**
 * Result of a completed deal, including card points and match points awarded.
 *
 * <p>Scoring thresholds: AllTrumps 129+/258, Colour 82+/162, NoTrumps 65+/130.
 * Match points: AllTrumps = 26 (split), NoTrumps = 52 (winner-takes-all), Colour = 16 (winner-takes-all).
 * Last trick bonus: +10 card points.
 */
@JsonIgnoreProperties(ignoreUnknown = true)
record DealResult(
    /** The game mode that was played. */
    GameMode gameMode,
    /** The multiplier (Normal/Doubled/Redoubled). */
    Multiplier multiplier,
    /** The team that made the winning bid. */
    Team announcerTeam,
    /** Total card points earned by Team1. */
    int team1CardPoints,
    /** Total card points earned by Team2. */
    int team2CardPoints,
    /** Match points awarded to Team1. */
    int team1MatchPoints,
    /** Match points awarded to Team2. */
    int team2MatchPoints,
    /** Whether one team won all 8 tricks. */
    boolean wasSweep,
    /** Which team swept (null if no sweep). */
    Team sweepingTeam,
    /** Whether this deal results in an instant match win (Colour sweep). */
    boolean isInstantWin
) {}

// ─── Match State ────────────────────────────────────────────────────

/**
 * Overall match state. First team to reach {@code targetScore} (150) match points wins.
 */
@JsonIgnoreProperties(ignoreUnknown = true)
record MatchState(
    /** Match points needed to win (default 150). */
    int targetScore,
    /** Team1's total match points across all deals. */
    int team1MatchPoints,
    /** Team2's total match points across all deals. */
    int team2MatchPoints,
    /** The current dealer position. */
    PlayerPosition currentDealer,
    /** Whether the match is over. */
    boolean isComplete,
    /** The winning team (null if match is still in progress). */
    Team winner,
    /** Results of all completed deals in this match. */
    List<DealResult> completedDeals
) {}

// ─── Session (internal — used by Server.java for deserialization) ───

@JsonIgnoreProperties(ignoreUnknown = true)
record SessionRequest(PlayerPosition position, String matchId) {}

// ─── Bot Contexts (passed to your methods) ──────────────────────────

/** Context for {@link Bot#chooseCut}. */
@JsonIgnoreProperties(ignoreUnknown = true)
record ChooseCutContext(
    /** Total number of cards in the deck (always 32). */
    int deckSize,
    /** Current match state. */
    MatchState matchState
) {}

/** The cut decision: where to cut and from which end. */
@JsonInclude(JsonInclude.Include.NON_NULL)
record CutResult(
    /** Cut position (must be between 6 and 26 inclusive). */
    int position,
    /** True to cut from the top of the deck, false from the bottom. */
    boolean fromTop
) {}

/** Context for {@link Bot#chooseNegotiationAction}. */
@JsonIgnoreProperties(ignoreUnknown = true)
record ChooseNegotiationActionContext(
    /** Your current hand (5 cards during negotiation). */
    List<Card> hand,
    /** Full negotiation state including bid history. */
    NegotiationState negotiationState,
    /** Current match state. */
    MatchState matchState,
    /** List of valid actions you can choose from. Pick exactly one. */
    List<NegotiationActionChoice> validActions
) {}

/** Context for {@link Bot#chooseCard}. */
@JsonIgnoreProperties(ignoreUnknown = true)
record ChooseCardContext(
    /** Your current hand. */
    List<Card> hand,
    /** Current play state (tricks, points, etc.). */
    HandState handState,
    /** Current match state. */
    MatchState matchState,
    /** List of cards you are allowed to play. Pick exactly one. */
    List<Card> validPlays
) {}

/** Notification: a new deal is starting. */
@JsonIgnoreProperties(ignoreUnknown = true)
record DealStartedContext(MatchState matchState) {}

/** Notification: a player (any, including you) played a card. */
@JsonIgnoreProperties(ignoreUnknown = true)
record CardPlayedContext(
    /** The player who played the card. */
    PlayerPosition player,
    /** The card that was played. */
    Card card,
    /** Updated hand state after the card was played. */
    HandState handState,
    MatchState matchState
) {}

/** Notification: a trick was completed. */
@JsonIgnoreProperties(ignoreUnknown = true)
record TrickCompletedContext(
    /** The completed trick with all 4 cards. */
    TrickState completedTrick,
    /** The player who won the trick. */
    PlayerPosition winner,
    /** Updated hand state after the trick. */
    HandState handState,
    MatchState matchState
) {}

/** Notification: a deal ended with scoring results. */
@JsonIgnoreProperties(ignoreUnknown = true)
record DealEndedContext(
    /** Scoring result for the completed deal. */
    DealResult result,
    /** Final hand state at end of deal. */
    HandState handState,
    MatchState matchState
) {}

/** Notification: the match is over. */
@JsonIgnoreProperties(ignoreUnknown = true)
record MatchEndedContext(
    /** Final match state (check {@link MatchState#winner}). */
    MatchState matchState
) {}
