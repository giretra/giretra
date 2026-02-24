// types.go — Type definitions for the Giretra bot API.
//
// JSON is serialized with camelCase — Go struct tags handle the mapping.
// See bot.go for the game logic.

package main

// ─── Cards ──────────────────────────────────────────────────────────

type Rank string

const (
	RankSeven Rank = "seven"
	RankEight Rank = "eight"
	RankNine  Rank = "nine"
	RankTen   Rank = "ten"
	RankJack  Rank = "jack"
	RankQueen Rank = "queen"
	RankKing  Rank = "king"
	RankAce   Rank = "ace"
)

type Suit string

const (
	SuitClubs    Suit = "clubs"
	SuitDiamonds Suit = "diamonds"
	SuitHearts   Suit = "hearts"
	SuitSpades   Suit = "spades"
)

type Card struct {
	Rank Rank `json:"rank"`
	Suit Suit `json:"suit"`
}

// ─── Players & Teams ────────────────────────────────────────────────

type PlayerPosition string

const (
	PlayerBottom PlayerPosition = "bottom"
	PlayerLeft   PlayerPosition = "left"
	PlayerTop    PlayerPosition = "top"
	PlayerRight  PlayerPosition = "right"
)

type Team string

const (
	Team1 Team = "team1"
	Team2 Team = "team2"
)

type PlayedCard struct {
	Player PlayerPosition `json:"player"`
	Card   Card           `json:"card"`
}

// ─── Game Modes ─────────────────────────────────────────────────────

type GameMode string

const (
	ColourClubs    GameMode = "colourClubs"
	ColourDiamonds GameMode = "colourDiamonds"
	ColourHearts   GameMode = "colourHearts"
	ColourSpades   GameMode = "colourSpades"
	NoTrumps       GameMode = "noTrumps"
	AllTrumps      GameMode = "allTrumps"
)

type Multiplier string

const (
	MultiplierNormal    Multiplier = "normal"
	MultiplierDoubled   Multiplier = "doubled"
	MultiplierRedoubled Multiplier = "redoubled"
)

// ─── Trick ──────────────────────────────────────────────────────────

type TrickState struct {
	Leader      PlayerPosition `json:"leader"`
	TrickNumber int            `json:"trickNumber"`
	PlayedCards []PlayedCard   `json:"playedCards"`
	IsComplete  bool           `json:"isComplete"`
}

// ─── Hand State ─────────────────────────────────────────────────────

type HandState struct {
	GameMode        GameMode     `json:"gameMode"`
	Team1CardPoints int          `json:"team1CardPoints"`
	Team2CardPoints int          `json:"team2CardPoints"`
	Team1TricksWon  int          `json:"team1TricksWon"`
	Team2TricksWon  int          `json:"team2TricksWon"`
	CurrentTrick    *TrickState  `json:"currentTrick,omitempty"`
	CompletedTricks []TrickState `json:"completedTricks"`
}

// ─── Negotiation ────────────────────────────────────────────────────

type NegotiationActionType string

const (
	ActionAnnouncement NegotiationActionType = "announcement"
	ActionAccept       NegotiationActionType = "accept"
	ActionDouble       NegotiationActionType = "double"
	ActionRedouble     NegotiationActionType = "redouble"
)

type NegotiationAction struct {
	Type       NegotiationActionType `json:"type"`
	Player     *PlayerPosition       `json:"player,omitempty"`
	Mode       *GameMode             `json:"mode,omitempty"`
	TargetMode *GameMode             `json:"targetMode,omitempty"`
}

// NegotiationActionChoice is a valid action choice (no player field — the server knows who you are).
type NegotiationActionChoice struct {
	Type       NegotiationActionType `json:"type"`
	Mode       *GameMode             `json:"mode,omitempty"`
	TargetMode *GameMode             `json:"targetMode,omitempty"`
}

type NegotiationState struct {
	Dealer                  PlayerPosition      `json:"dealer"`
	CurrentPlayer           PlayerPosition      `json:"currentPlayer"`
	CurrentBid              *GameMode            `json:"currentBid,omitempty"`
	CurrentBidder           *PlayerPosition      `json:"currentBidder,omitempty"`
	ConsecutiveAccepts      int                  `json:"consecutiveAccepts"`
	HasDoubleOccurred       bool                 `json:"hasDoubleOccurred"`
	Actions                 []NegotiationAction  `json:"actions"`
	DoubledModes            map[GameMode]int     `json:"doubledModes"`
	RedoubledModes          []GameMode           `json:"redoubledModes"`
	TeamColourAnnouncements map[Team]GameMode    `json:"teamColourAnnouncements"`
}

// ─── Scoring ────────────────────────────────────────────────────────

type DealResult struct {
	GameMode         GameMode   `json:"gameMode"`
	Multiplier       Multiplier `json:"multiplier"`
	AnnouncerTeam    Team       `json:"announcerTeam"`
	Team1CardPoints  int        `json:"team1CardPoints"`
	Team2CardPoints  int        `json:"team2CardPoints"`
	Team1MatchPoints int        `json:"team1MatchPoints"`
	Team2MatchPoints int        `json:"team2MatchPoints"`
	WasSweep         bool       `json:"wasSweep"`
	SweepingTeam     *Team      `json:"sweepingTeam,omitempty"`
	IsInstantWin     bool       `json:"isInstantWin"`
}

// ─── Match State ────────────────────────────────────────────────────

type MatchState struct {
	TargetScore      int            `json:"targetScore"`
	Team1MatchPoints int            `json:"team1MatchPoints"`
	Team2MatchPoints int            `json:"team2MatchPoints"`
	CurrentDealer    PlayerPosition `json:"currentDealer"`
	IsComplete       bool           `json:"isComplete"`
	Winner           *Team          `json:"winner,omitempty"`
	CompletedDeals   []DealResult   `json:"completedDeals"`
}

// ─── Session (internal — used by server.go for deserialization) ─────

type SessionRequest struct {
	Position PlayerPosition `json:"position"`
	MatchID  string         `json:"matchId"`
}

// ─── Bot Contexts (passed to your methods) ──────────────────────────

type ChooseCutContext struct {
	DeckSize   int        `json:"deckSize"`
	MatchState MatchState `json:"matchState"`
}

type CutResult struct {
	Position int  `json:"position"`
	FromTop  bool `json:"fromTop"`
}

type ChooseNegotiationActionContext struct {
	Hand             []Card                  `json:"hand"`
	NegotiationState NegotiationState        `json:"negotiationState"`
	MatchState       MatchState              `json:"matchState"`
	ValidActions     []NegotiationActionChoice `json:"validActions"`
}

type ChooseCardContext struct {
	Hand       []Card     `json:"hand"`
	HandState  HandState  `json:"handState"`
	MatchState MatchState `json:"matchState"`
	ValidPlays []Card     `json:"validPlays"`
}

type DealStartedContext struct {
	MatchState MatchState `json:"matchState"`
}

type CardPlayedContext struct {
	Player     PlayerPosition `json:"player"`
	Card       Card           `json:"card"`
	HandState  HandState      `json:"handState"`
	MatchState MatchState     `json:"matchState"`
}

type TrickCompletedContext struct {
	CompletedTrick TrickState     `json:"completedTrick"`
	Winner         PlayerPosition `json:"winner"`
	HandState      HandState      `json:"handState"`
	MatchState     MatchState     `json:"matchState"`
}

type DealEndedContext struct {
	Result     DealResult `json:"result"`
	HandState  HandState  `json:"handState"`
	MatchState MatchState `json:"matchState"`
}

type MatchEndedContext struct {
	MatchState MatchState `json:"matchState"`
}
