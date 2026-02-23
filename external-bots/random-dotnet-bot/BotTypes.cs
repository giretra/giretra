// BotTypes.cs — Type definitions for the Giretra bot API.
//
// JSON is serialized with camelCase — ASP.NET Core handles this automatically.
// C# properties use PascalCase (idiomatic C#).

namespace RandomDotnetBot;

// ─── Cards ──────────────────────────────────────────────────────────

public class Card
{
    public string Rank { get; set; } = "";
    public string Suit { get; set; } = "";
}

// ─── Players ────────────────────────────────────────────────────────

// PlayerPosition values: "Bottom", "Left", "Top", "Right"
// Team values: "Team1", "Team2"

public class PlayedCard
{
    public string Player { get; set; } = "";
    public Card Card { get; set; } = new();
}

// ─── Game Modes ─────────────────────────────────────────────────────

// GameMode values: "ColourClubs", "ColourDiamonds", "ColourHearts",
//                  "ColourSpades", "NoTrumps", "AllTrumps"
// MultiplierState values: "None", "Doubled", "Redoubled"

// ─── Trick ──────────────────────────────────────────────────────────

public class TrickState
{
    public string Leader { get; set; } = "";
    public int TrickNumber { get; set; }
    public List<PlayedCard> PlayedCards { get; set; } = [];
    public bool IsComplete { get; set; }
}

// ─── Hand State ─────────────────────────────────────────────────────

public class HandState
{
    public string GameMode { get; set; } = "";
    public int Team1CardPoints { get; set; }
    public int Team2CardPoints { get; set; }
    public int Team1TricksWon { get; set; }
    public int Team2TricksWon { get; set; }
    public TrickState? CurrentTrick { get; set; }
    public List<TrickState> CompletedTricks { get; set; } = [];
}

// ─── Negotiation ────────────────────────────────────────────────────

public class NegotiationAction
{
    public string Type { get; set; } = "";
    public string? Player { get; set; }
    public string? Mode { get; set; }
    public string? TargetMode { get; set; }
}

/// <summary>Valid action choice (no player field — the server knows who you are).</summary>
public class NegotiationActionChoice
{
    public string Type { get; set; } = "";
    public string? Mode { get; set; }
    public string? TargetMode { get; set; }
}

public class NegotiationState
{
    public string Dealer { get; set; } = "";
    public string CurrentPlayer { get; set; } = "";
    public string? CurrentBid { get; set; }
    public string? CurrentBidder { get; set; }
    public int ConsecutiveAccepts { get; set; }
    public bool HasDoubleOccurred { get; set; }
    public List<NegotiationAction> Actions { get; set; } = [];
    public Dictionary<string, bool> DoubledModes { get; set; } = new();
    public List<string> RedoubledModes { get; set; } = [];
    public Dictionary<string, string> TeamColourAnnouncements { get; set; } = new();
}

// ─── Scoring ────────────────────────────────────────────────────────

public class DealResult
{
    public string GameMode { get; set; } = "";
    public string Multiplier { get; set; } = "";
    public string AnnouncerTeam { get; set; } = "";
    public int Team1CardPoints { get; set; }
    public int Team2CardPoints { get; set; }
    public int Team1MatchPoints { get; set; }
    public int Team2MatchPoints { get; set; }
    public bool WasSweep { get; set; }
    public string? SweepingTeam { get; set; }
    public bool IsInstantWin { get; set; }
}

// ─── Match State ────────────────────────────────────────────────────

public class MatchState
{
    public int TargetScore { get; set; }
    public int Team1MatchPoints { get; set; }
    public int Team2MatchPoints { get; set; }
    public string CurrentDealer { get; set; } = "";
    public bool IsComplete { get; set; }
    public string? Winner { get; set; }
    public List<DealResult> CompletedDeals { get; set; } = [];
}

// ─── Session ────────────────────────────────────────────────────────

public class Session
{
    public string Position { get; set; } = "";
    public string MatchId { get; set; } = "";
}

// ─── Bot Contexts (passed to your functions) ────────────────────────

public class ChooseCutContext
{
    public int DeckSize { get; set; }
    public MatchState MatchState { get; set; } = new();
    public Session? Session { get; set; }
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
    public Session? Session { get; set; }
}

public class ChooseCardContext
{
    public List<Card> Hand { get; set; } = [];
    public HandState HandState { get; set; } = new();
    public MatchState MatchState { get; set; } = new();
    public List<Card> ValidPlays { get; set; } = [];
    public Session? Session { get; set; }
}

public class DealStartedContext
{
    public MatchState MatchState { get; set; } = new();
    public Session? Session { get; set; }
}

public class CardPlayedContext
{
    public string Player { get; set; } = "";
    public Card Card { get; set; } = new();
    public HandState HandState { get; set; } = new();
    public MatchState MatchState { get; set; } = new();
    public Session? Session { get; set; }
}

public class TrickCompletedContext
{
    public TrickState CompletedTrick { get; set; } = new();
    public string Winner { get; set; } = "";
    public HandState HandState { get; set; } = new();
    public MatchState MatchState { get; set; } = new();
    public Session? Session { get; set; }
}

public class DealEndedContext
{
    public DealResult Result { get; set; } = new();
    public HandState HandState { get; set; } = new();
    public MatchState MatchState { get; set; } = new();
    public Session? Session { get; set; }
}

public class MatchEndedContext
{
    public MatchState MatchState { get; set; } = new();
    public Session? Session { get; set; }
}
