// BotTypes.cs — Type definitions for the Giretra bot API.
//
// These types mirror the JSON payloads exchanged between the game server and your bot.
// JSON is serialized with camelCase property names — ASP.NET Core handles this automatically.
// C# properties use PascalCase (idiomatic C#).
//
// You should NOT need to edit this file. All game logic belongs in Bot.cs.

namespace RandomDotnetBot;

// ─── Cards ──────────────────────────────────────────────────────────

/// <summary>
/// Card ranks from lowest to highest.
/// <list type="table">
///   <listheader><term>Context</term><description>Order (high to low)</description></listheader>
///   <item><term>Trump / AllTrumps</term><description>J &gt; 9 &gt; A &gt; 10 &gt; K &gt; Q &gt; 8 &gt; 7</description></item>
///   <item><term>Non-trump / NoTrumps</term><description>A &gt; 10 &gt; K &gt; Q &gt; J &gt; 9 &gt; 8 &gt; 7</description></item>
/// </list>
/// </summary>
public enum Rank { Seven, Eight, Nine, Ten, Jack, Queen, King, Ace }

/// <summary>Card suits. Also determines Colour game modes (ColourClubs, ColourDiamonds, etc.).</summary>
public enum Suit { Clubs, Diamonds, Hearts, Spades }

/// <summary>
/// A playing card with a rank and suit.
/// <para>
/// Point values depend on game mode:
/// <list type="table">
///   <listheader><term>Card</term><description>Trump/AllTrumps | Non-trump/NoTrumps</description></listheader>
///   <item><term>Jack</term><description>20 | 2</description></item>
///   <item><term>Nine</term><description>14 | 0</description></item>
///   <item><term>Ace</term><description>11 | 11</description></item>
///   <item><term>Ten</term><description>10 | 10</description></item>
///   <item><term>King</term><description>4 | 4</description></item>
///   <item><term>Queen</term><description>3 | 3</description></item>
///   <item><term>Eight, Seven</term><description>0 | 0</description></item>
/// </list>
/// </para>
/// </summary>
public class Card
{
    public Rank Rank { get; set; }
    public Suit Suit { get; set; }

    public override string ToString() => $"{Rank} of {Suit}";
}

// ─── Players & Teams ────────────────────────────────────────────────

/// <summary>
/// Seat positions around the table (clockwise: Bottom → Left → Top → Right).
/// Your bot is always <see cref="Bottom"/>.
/// </summary>
public enum PlayerPosition { Bottom, Left, Top, Right }

/// <summary>
/// Team1 = Bottom + Top (you and your partner).
/// Team2 = Left + Right (opponents).
/// </summary>
public enum Team { Team1, Team2 }

/// <summary>A card played by a specific player in a trick.</summary>
public class PlayedCard
{
    /// <summary>The player who played the card.</summary>
    public PlayerPosition Player { get; set; }

    /// <summary>The card that was played.</summary>
    public Card Card { get; set; } = new();
}

// ─── Game Modes ─────────────────────────────────────────────────────

/// <summary>
/// Game modes ordered from lowest to highest bid.
/// During negotiation you can only announce a mode higher than the current bid.
/// </summary>
public enum GameMode
{
    ColourClubs, ColourDiamonds, ColourHearts, ColourSpades,
    NoTrumps, AllTrumps
}

/// <summary>
/// Scoring multiplier for a deal.
/// Normal = x1, Doubled = x2, Redoubled = x4.
/// </summary>
public enum Multiplier { Normal, Doubled, Redoubled }

// ─── Trick ──────────────────────────────────────────────────────────

/// <summary>State of a single trick (4 cards, one per player).</summary>
public class TrickState
{
    /// <summary>The player who led this trick (played first).</summary>
    public PlayerPosition Leader { get; set; }

    /// <summary>1-based trick number within the deal (1–8).</summary>
    public int TrickNumber { get; set; }

    /// <summary>Cards played so far in this trick (0–4 cards, in play order).</summary>
    public List<PlayedCard> PlayedCards { get; set; } = [];

    /// <summary>True when all 4 cards have been played.</summary>
    public bool IsComplete { get; set; }
}

// ─── Hand State ─────────────────────────────────────────────────────

/// <summary>
/// State of the current deal's play phase (after negotiation).
/// <para>Total card points per mode: AllTrumps = 258, Colour = 162, NoTrumps = 130.</para>
/// </summary>
public class HandState
{
    /// <summary>The game mode in effect for this deal.</summary>
    public GameMode GameMode { get; set; }

    /// <summary>Card points accumulated by Team1 (Bottom + Top) so far.</summary>
    public int Team1CardPoints { get; set; }

    /// <summary>Card points accumulated by Team2 (Left + Right) so far.</summary>
    public int Team2CardPoints { get; set; }

    /// <summary>Number of tricks won by Team1 so far (0–8).</summary>
    public int Team1TricksWon { get; set; }

    /// <summary>Number of tricks won by Team2 so far (0–8).</summary>
    public int Team2TricksWon { get; set; }

    /// <summary>The trick currently being played (null between tricks).</summary>
    public TrickState? CurrentTrick { get; set; }

    /// <summary>All tricks completed so far in this deal.</summary>
    public List<TrickState> CompletedTricks { get; set; } = [];
}

// ─── Negotiation ────────────────────────────────────────────────────

/// <summary>The type of action taken during negotiation.</summary>
public enum NegotiationActionType { Announcement, Accept, Double, Redouble }

/// <summary>
/// A negotiation action from the history (includes the player who took it).
/// </summary>
public class NegotiationAction
{
    /// <summary>What type of action was taken.</summary>
    public NegotiationActionType Type { get; set; }

    /// <summary>The player who took this action (present in history, absent in choices).</summary>
    public PlayerPosition? Player { get; set; }

    /// <summary>The game mode being announced (only for <see cref="NegotiationActionType.Announcement"/>).</summary>
    public GameMode? Mode { get; set; }

    /// <summary>The mode being doubled/redoubled (only for <see cref="NegotiationActionType.Double"/> and <see cref="NegotiationActionType.Redouble"/>).</summary>
    public GameMode? TargetMode { get; set; }
}

/// <summary>
/// A valid action you can choose during negotiation.
/// Same shape as <see cref="NegotiationAction"/> but without the Player field
/// (the server knows who you are).
/// </summary>
public class NegotiationActionChoice
{
    public NegotiationActionType Type { get; set; }
    public GameMode? Mode { get; set; }
    public GameMode? TargetMode { get; set; }
}

/// <summary>
/// Full state of the negotiation (bidding) phase.
/// Negotiation ends after 3 consecutive Accepts.
/// </summary>
public class NegotiationState
{
    /// <summary>The dealer for this deal.</summary>
    public PlayerPosition Dealer { get; set; }

    /// <summary>Whose turn it is to act.</summary>
    public PlayerPosition CurrentPlayer { get; set; }

    /// <summary>The highest bid so far, or null if no one has announced yet.</summary>
    public GameMode? CurrentBid { get; set; }

    /// <summary>The player who made the current highest bid.</summary>
    public PlayerPosition? CurrentBidder { get; set; }

    /// <summary>Number of consecutive accepts (negotiation ends at 3).</summary>
    public int ConsecutiveAccepts { get; set; }

    /// <summary>Whether any double has occurred in this negotiation.</summary>
    public bool HasDoubleOccurred { get; set; }

    /// <summary>Full history of all actions taken in this negotiation.</summary>
    public List<NegotiationAction> Actions { get; set; } = [];

    /// <summary>Which game modes have been doubled (mode → true/false).</summary>
    public Dictionary<GameMode, bool> DoubledModes { get; set; } = new();

    /// <summary>Game modes that have been redoubled.</summary>
    public List<GameMode> RedoubledModes { get; set; } = [];

    /// <summary>Each team's Colour announcement this deal (max one Colour per team).</summary>
    public Dictionary<Team, GameMode> TeamColourAnnouncements { get; set; } = new();
}

// ─── Scoring ────────────────────────────────────────────────────────

/// <summary>
/// Result of a completed deal, including card points and match points awarded.
/// <para>
/// Scoring thresholds: AllTrumps 129+/258, Colour 82+/162, NoTrumps 65+/130.
/// Match points: AllTrumps = 26 (split), NoTrumps = 52 (winner-takes-all), Colour = 16 (winner-takes-all).
/// Last trick bonus: +10 card points.
/// </para>
/// </summary>
public class DealResult
{
    /// <summary>The game mode that was played.</summary>
    public GameMode GameMode { get; set; }

    /// <summary>The multiplier (Normal/Doubled/Redoubled).</summary>
    public Multiplier Multiplier { get; set; }

    /// <summary>The team that made the winning bid.</summary>
    public Team AnnouncerTeam { get; set; }

    /// <summary>Total card points earned by Team1.</summary>
    public int Team1CardPoints { get; set; }

    /// <summary>Total card points earned by Team2.</summary>
    public int Team2CardPoints { get; set; }

    /// <summary>Match points awarded to Team1.</summary>
    public int Team1MatchPoints { get; set; }

    /// <summary>Match points awarded to Team2.</summary>
    public int Team2MatchPoints { get; set; }

    /// <summary>Whether one team won all 8 tricks.</summary>
    public bool WasSweep { get; set; }

    /// <summary>Which team swept (null if no sweep).</summary>
    public Team? SweepingTeam { get; set; }

    /// <summary>Whether this deal results in an instant match win (Colour sweep).</summary>
    public bool IsInstantWin { get; set; }
}

// ─── Match State ────────────────────────────────────────────────────

/// <summary>
/// Overall match state. First team to reach <see cref="TargetScore"/> (150) match points wins.
/// </summary>
public class MatchState
{
    /// <summary>Match points needed to win (default 150).</summary>
    public int TargetScore { get; set; }

    /// <summary>Team1's total match points across all deals.</summary>
    public int Team1MatchPoints { get; set; }

    /// <summary>Team2's total match points across all deals.</summary>
    public int Team2MatchPoints { get; set; }

    /// <summary>The current dealer position.</summary>
    public PlayerPosition CurrentDealer { get; set; }

    /// <summary>Whether the match is over.</summary>
    public bool IsComplete { get; set; }

    /// <summary>The winning team (null if match is still in progress).</summary>
    public Team? Winner { get; set; }

    /// <summary>Results of all completed deals in this match.</summary>
    public List<DealResult> CompletedDeals { get; set; } = [];
}

// ─── Session (internal — used by Server.cs for deserialization) ──────

public class SessionRequest
{
    public PlayerPosition Position { get; set; }
    public string MatchId { get; set; } = "";
}

// ─── Bot Contexts (passed to your methods) ──────────────────────────

/// <summary>Context for <see cref="Bot.ChooseCut"/>.</summary>
public class ChooseCutContext
{
    /// <summary>Total number of cards in the deck (always 32).</summary>
    public int DeckSize { get; set; }

    /// <summary>Current match state.</summary>
    public MatchState MatchState { get; set; } = new();
}

/// <summary>The cut decision: where to cut and from which end.</summary>
public class CutResult
{
    /// <summary>Cut position (must be between 6 and 26 inclusive).</summary>
    public int Position { get; set; }

    /// <summary>True to cut from the top of the deck, false from the bottom.</summary>
    public bool FromTop { get; set; }
}

/// <summary>Context for <see cref="Bot.ChooseNegotiationAction"/>.</summary>
public class ChooseNegotiationActionContext
{
    /// <summary>Your current hand (5 cards during negotiation).</summary>
    public List<Card> Hand { get; set; } = [];

    /// <summary>Full negotiation state including bid history.</summary>
    public NegotiationState NegotiationState { get; set; } = new();

    /// <summary>Current match state.</summary>
    public MatchState MatchState { get; set; } = new();

    /// <summary>List of valid actions you can choose from. Pick exactly one.</summary>
    public List<NegotiationActionChoice> ValidActions { get; set; } = [];
}

/// <summary>Context for <see cref="Bot.ChooseCard"/>.</summary>
public class ChooseCardContext
{
    /// <summary>Your current hand.</summary>
    public List<Card> Hand { get; set; } = [];

    /// <summary>Current play state (tricks, points, etc.).</summary>
    public HandState HandState { get; set; } = new();

    /// <summary>Current match state.</summary>
    public MatchState MatchState { get; set; } = new();

    /// <summary>List of cards you are allowed to play. Pick exactly one.</summary>
    public List<Card> ValidPlays { get; set; } = [];
}

/// <summary>Notification: a new deal is starting.</summary>
public class DealStartedContext
{
    public MatchState MatchState { get; set; } = new();
}

/// <summary>Notification: a player (any, including you) played a card.</summary>
public class CardPlayedContext
{
    /// <summary>The player who played the card.</summary>
    public PlayerPosition Player { get; set; }

    /// <summary>The card that was played.</summary>
    public Card Card { get; set; } = new();

    /// <summary>Updated hand state after the card was played.</summary>
    public HandState HandState { get; set; } = new();

    public MatchState MatchState { get; set; } = new();
}

/// <summary>Notification: a trick was completed.</summary>
public class TrickCompletedContext
{
    /// <summary>The completed trick with all 4 cards.</summary>
    public TrickState CompletedTrick { get; set; } = new();

    /// <summary>The player who won the trick.</summary>
    public PlayerPosition Winner { get; set; }

    /// <summary>Updated hand state after the trick.</summary>
    public HandState HandState { get; set; } = new();

    public MatchState MatchState { get; set; } = new();
}

/// <summary>Notification: a deal ended with scoring results.</summary>
public class DealEndedContext
{
    /// <summary>Scoring result for the completed deal.</summary>
    public DealResult Result { get; set; } = new();

    /// <summary>Final hand state at end of deal.</summary>
    public HandState HandState { get; set; } = new();

    public MatchState MatchState { get; set; } = new();
}

/// <summary>Notification: the match is over.</summary>
public class MatchEndedContext
{
    /// <summary>Final match state (check <see cref="MatchState.Winner"/>).</summary>
    public MatchState MatchState { get; set; } = new();
}
