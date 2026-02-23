// BotTypes.cs — Type definitions for the Giretra bot API.
//
// JSON is serialized with camelCase — ASP.NET Core handles this automatically.
// C# properties use PascalCase (idiomatic C#).

namespace RandomDotnetBot;

// ─── Cards ──────────────────────────────────────────────────────────

public enum Rank { Seven, Eight, Nine, Ten, Jack, Queen, King, Ace }
public enum Suit { Clubs, Diamonds, Hearts, Spades }

public class Card
{
    public Rank Rank { get; set; }
    public Suit Suit { get; set; }
}

// ─── Players & Teams ────────────────────────────────────────────────

public enum PlayerPosition { Bottom, Left, Top, Right }
public enum Team { Team1, Team2 }

public class PlayedCard
{
    public PlayerPosition Player { get; set; }
    public Card Card { get; set; } = new();
}

// ─── Game Modes ─────────────────────────────────────────────────────

public enum GameMode
{
    ColourClubs, ColourDiamonds, ColourHearts, ColourSpades,
    NoTrumps, AllTrumps
}

public enum Multiplier { Normal, Doubled, Redoubled }

// ─── Trick ──────────────────────────────────────────────────────────

public class TrickState
{
    public PlayerPosition Leader { get; set; }
    public int TrickNumber { get; set; }
    public List<PlayedCard> PlayedCards { get; set; } = [];
    public bool IsComplete { get; set; }
}

// ─── Hand State ─────────────────────────────────────────────────────

public class HandState
{
    public GameMode GameMode { get; set; }
    public int Team1CardPoints { get; set; }
    public int Team2CardPoints { get; set; }
    public int Team1TricksWon { get; set; }
    public int Team2TricksWon { get; set; }
    public TrickState? CurrentTrick { get; set; }
    public List<TrickState> CompletedTricks { get; set; } = [];
}

// ─── Negotiation ────────────────────────────────────────────────────

public enum NegotiationActionType { Announcement, Accept, Double, Redouble }

public class NegotiationAction
{
    public NegotiationActionType Type { get; set; }
    public PlayerPosition? Player { get; set; }
    public GameMode? Mode { get; set; }
    public GameMode? TargetMode { get; set; }
}

/// <summary>Valid action choice (no player field — the server knows who you are).</summary>
public class NegotiationActionChoice
{
    public NegotiationActionType Type { get; set; }
    public GameMode? Mode { get; set; }
    public GameMode? TargetMode { get; set; }
}

public class NegotiationState
{
    public PlayerPosition Dealer { get; set; }
    public PlayerPosition CurrentPlayer { get; set; }
    public GameMode? CurrentBid { get; set; }
    public PlayerPosition? CurrentBidder { get; set; }
    public int ConsecutiveAccepts { get; set; }
    public bool HasDoubleOccurred { get; set; }
    public List<NegotiationAction> Actions { get; set; } = [];
    public Dictionary<GameMode, bool> DoubledModes { get; set; } = new();
    public List<GameMode> RedoubledModes { get; set; } = [];
    public Dictionary<Team, GameMode> TeamColourAnnouncements { get; set; } = new();
}

// ─── Scoring ────────────────────────────────────────────────────────

public class DealResult
{
    public GameMode GameMode { get; set; }
    public Multiplier Multiplier { get; set; }
    public Team AnnouncerTeam { get; set; }
    public int Team1CardPoints { get; set; }
    public int Team2CardPoints { get; set; }
    public int Team1MatchPoints { get; set; }
    public int Team2MatchPoints { get; set; }
    public bool WasSweep { get; set; }
    public Team? SweepingTeam { get; set; }
    public bool IsInstantWin { get; set; }
}

// ─── Match State ────────────────────────────────────────────────────

public class MatchState
{
    public int TargetScore { get; set; }
    public int Team1MatchPoints { get; set; }
    public int Team2MatchPoints { get; set; }
    public PlayerPosition CurrentDealer { get; set; }
    public bool IsComplete { get; set; }
    public Team? Winner { get; set; }
    public List<DealResult> CompletedDeals { get; set; } = [];
}

// ─── Session (internal — used by Server.cs for deserialization) ──────

public class SessionRequest
{
    public PlayerPosition Position { get; set; }
    public string MatchId { get; set; } = "";
}

// ─── Bot Contexts (passed to your methods) ──────────────────────────

public class ChooseCutContext
{
    public int DeckSize { get; set; }
    public MatchState MatchState { get; set; } = new();
}

public class CutResult
{
    public int Position { get; set; }
    public bool FromTop { get; set; }
}

public class ChooseNegotiationActionContext
{
    public List<Card> Hand { get; set; } = [];
    public NegotiationState NegotiationState { get; set; } = new();
    public MatchState MatchState { get; set; } = new();
    public List<NegotiationActionChoice> ValidActions { get; set; } = [];
}

public class ChooseCardContext
{
    public List<Card> Hand { get; set; } = [];
    public HandState HandState { get; set; } = new();
    public MatchState MatchState { get; set; } = new();
    public List<Card> ValidPlays { get; set; } = [];
}

public class DealStartedContext
{
    public MatchState MatchState { get; set; } = new();
}

public class CardPlayedContext
{
    public PlayerPosition Player { get; set; }
    public Card Card { get; set; } = new();
    public HandState HandState { get; set; } = new();
    public MatchState MatchState { get; set; } = new();
}

public class TrickCompletedContext
{
    public TrickState CompletedTrick { get; set; } = new();
    public PlayerPosition Winner { get; set; }
    public HandState HandState { get; set; } = new();
    public MatchState MatchState { get; set; } = new();
}

public class DealEndedContext
{
    public DealResult Result { get; set; } = new();
    public HandState HandState { get; set; } = new();
    public MatchState MatchState { get; set; } = new();
}

public class MatchEndedContext
{
    public MatchState MatchState { get; set; } = new();
}
