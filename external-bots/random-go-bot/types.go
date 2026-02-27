// types.go — Type definitions for the Giretra bot API.
//
// These types mirror the JSON payloads exchanged between the game server and your bot.
// JSON is serialized with camelCase — Go struct tags handle the mapping.
//
// You should NOT need to edit this file. All game logic belongs in bot.go.

package main

// ─── Cards ──────────────────────────────────────────────────────────

// Rank represents card ranks from lowest to highest.
//
// Strength order depends on game mode:
//   - Trump / AllTrumps: J > 9 > A > 10 > K > Q > 8 > 7
//   - Non-trump / NoTrumps: A > 10 > K > Q > J > 9 > 8 > 7
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

// Suit represents card suits. Also determines Colour game modes (ColourClubs, ColourDiamonds, etc.).
type Suit string

const (
	SuitClubs    Suit = "clubs"
	SuitDiamonds Suit = "diamonds"
	SuitHearts   Suit = "hearts"
	SuitSpades   Suit = "spades"
)

// Card is a playing card with a rank and suit.
//
// Point values depend on game mode:
//
//	Card        Trump/AllTrumps    Non-trump/NoTrumps
//	Jack        20                 2
//	Nine        14                 0
//	Ace         11                 11
//	Ten         10                 10
//	King        4                  4
//	Queen       3                  3
//	Eight,Seven 0                  0
type Card struct {
	Rank Rank `json:"rank"`
	Suit Suit `json:"suit"`
}

// ─── Players & Teams ────────────────────────────────────────────────

// PlayerPosition represents seat positions around the table (clockwise: Bottom → Left → Top → Right).
// Your bot is always Bottom.
type PlayerPosition string

const (
	PlayerBottom PlayerPosition = "bottom"
	PlayerLeft   PlayerPosition = "left"
	PlayerTop    PlayerPosition = "top"
	PlayerRight  PlayerPosition = "right"
)

// Team identifies a team. Team1 = Bottom + Top (you and your partner). Team2 = Left + Right (opponents).
type Team string

const (
	Team1 Team = "team1"
	Team2 Team = "team2"
)

// PlayedCard is a card played by a specific player in a trick.
type PlayedCard struct {
	// Player is the player who played the card.
	Player PlayerPosition `json:"player"`
	// Card is the card that was played.
	Card Card `json:"card"`
}

// ─── Game Modes ─────────────────────────────────────────────────────

// GameMode represents game modes ordered from lowest to highest bid.
// During negotiation you can only announce a mode higher than the current bid.
type GameMode string

const (
	ColourClubs    GameMode = "colourClubs"
	ColourDiamonds GameMode = "colourDiamonds"
	ColourHearts   GameMode = "colourHearts"
	ColourSpades   GameMode = "colourSpades"
	NoTrumps       GameMode = "noTrumps"
	AllTrumps      GameMode = "allTrumps"
)

// Multiplier represents the scoring multiplier for a deal.
// Normal = x1, Doubled = x2, Redoubled = x4.
type Multiplier string

const (
	MultiplierNormal    Multiplier = "normal"
	MultiplierDoubled   Multiplier = "doubled"
	MultiplierRedoubled Multiplier = "redoubled"
)

// ─── Trick ──────────────────────────────────────────────────────────

// TrickState is the state of a single trick (4 cards, one per player).
type TrickState struct {
	// Leader is the player who led this trick (played first).
	Leader PlayerPosition `json:"leader"`
	// TrickNumber is the 1-based trick number within the deal (1–8).
	TrickNumber int `json:"trickNumber"`
	// PlayedCards are the cards played so far in this trick (0–4 cards, in play order).
	PlayedCards []PlayedCard `json:"playedCards"`
	// IsComplete is true when all 4 cards have been played.
	IsComplete bool `json:"isComplete"`
}

// ─── Hand State ─────────────────────────────────────────────────────

// HandState is the state of the current deal's play phase (after negotiation).
// Total card points per mode: AllTrumps = 258, Colour = 162, NoTrumps = 130.
type HandState struct {
	// GameMode is the game mode in effect for this deal.
	GameMode GameMode `json:"gameMode"`
	// Team1CardPoints is card points accumulated by Team1 (Bottom + Top) so far.
	Team1CardPoints int `json:"team1CardPoints"`
	// Team2CardPoints is card points accumulated by Team2 (Left + Right) so far.
	Team2CardPoints int `json:"team2CardPoints"`
	// Team1TricksWon is the number of tricks won by Team1 so far (0–8).
	Team1TricksWon int `json:"team1TricksWon"`
	// Team2TricksWon is the number of tricks won by Team2 so far (0–8).
	Team2TricksWon int `json:"team2TricksWon"`
	// CurrentTrick is the trick currently being played (nil between tricks).
	CurrentTrick *TrickState `json:"currentTrick,omitempty"`
	// CompletedTricks is all tricks completed so far in this deal.
	CompletedTricks []TrickState `json:"completedTricks"`
}

// ─── Negotiation ────────────────────────────────────────────────────

// NegotiationActionType is the type of action taken during negotiation.
type NegotiationActionType string

const (
	ActionAnnouncement NegotiationActionType = "announcement"
	ActionAccept       NegotiationActionType = "accept"
	ActionDouble       NegotiationActionType = "double"
	ActionRedouble     NegotiationActionType = "redouble"
)

// NegotiationAction is a negotiation action from the history (includes the player who took it).
type NegotiationAction struct {
	// Type is what type of action was taken.
	Type NegotiationActionType `json:"type"`
	// Player is who took this action (present in history, absent in choices).
	Player *PlayerPosition `json:"player,omitempty"`
	// Mode is the game mode being announced (only for Announcement).
	Mode *GameMode `json:"mode,omitempty"`
	// TargetMode is the mode being doubled/redoubled (only for Double and Redouble).
	TargetMode *GameMode `json:"targetMode,omitempty"`
}

// NegotiationActionChoice is a valid action you can choose during negotiation.
// Same shape as NegotiationAction but without the Player field (the server knows who you are).
type NegotiationActionChoice struct {
	Type       NegotiationActionType `json:"type"`
	Mode       *GameMode             `json:"mode,omitempty"`
	TargetMode *GameMode             `json:"targetMode,omitempty"`
}

// NegotiationState is the full state of the negotiation (bidding) phase.
// Negotiation ends after 3 consecutive Accepts.
type NegotiationState struct {
	// Dealer is the dealer for this deal.
	Dealer PlayerPosition `json:"dealer"`
	// CurrentPlayer is whose turn it is to act.
	CurrentPlayer PlayerPosition `json:"currentPlayer"`
	// CurrentBid is the highest bid so far, or nil if no one has announced yet.
	CurrentBid *GameMode `json:"currentBid,omitempty"`
	// CurrentBidder is the player who made the current highest bid.
	CurrentBidder *PlayerPosition `json:"currentBidder,omitempty"`
	// ConsecutiveAccepts is the number of consecutive accepts (negotiation ends at 3).
	ConsecutiveAccepts int `json:"consecutiveAccepts"`
	// HasDoubleOccurred is whether any double has occurred in this negotiation.
	HasDoubleOccurred bool `json:"hasDoubleOccurred"`
	// Actions is the full history of all actions taken in this negotiation.
	Actions []NegotiationAction `json:"actions"`
	// DoubledModes tracks which game modes have been doubled (mode → true/false).
	DoubledModes map[GameMode]int `json:"doubledModes"`
	// RedoubledModes lists game modes that have been redoubled.
	RedoubledModes []GameMode `json:"redoubledModes"`
	// TeamColourAnnouncements tracks each team's Colour announcement this deal (max one Colour per team).
	TeamColourAnnouncements map[Team]GameMode `json:"teamColourAnnouncements"`
}

// ─── Scoring ────────────────────────────────────────────────────────

// DealResult is the result of a completed deal, including card points and match points awarded.
//
// Scoring thresholds: AllTrumps 129+/258, Colour 82+/162, NoTrumps 65+/130.
// Match points: AllTrumps = 26 (split), NoTrumps = 52 (winner-takes-all), Colour = 16 (winner-takes-all).
// Last trick bonus: +10 card points.
type DealResult struct {
	// GameMode is the game mode that was played.
	GameMode GameMode `json:"gameMode"`
	// Multiplier is the multiplier (Normal/Doubled/Redoubled).
	Multiplier Multiplier `json:"multiplier"`
	// AnnouncerTeam is the team that made the winning bid.
	AnnouncerTeam Team `json:"announcerTeam"`
	// Team1CardPoints is total card points earned by Team1.
	Team1CardPoints int `json:"team1CardPoints"`
	// Team2CardPoints is total card points earned by Team2.
	Team2CardPoints int `json:"team2CardPoints"`
	// Team1MatchPoints is match points awarded to Team1.
	Team1MatchPoints int `json:"team1MatchPoints"`
	// Team2MatchPoints is match points awarded to Team2.
	Team2MatchPoints int `json:"team2MatchPoints"`
	// WasSweep is whether one team won all 8 tricks.
	WasSweep bool `json:"wasSweep"`
	// SweepingTeam is which team swept (nil if no sweep).
	SweepingTeam *Team `json:"sweepingTeam,omitempty"`
	// IsInstantWin is whether this deal results in an instant match win (Colour sweep).
	IsInstantWin bool `json:"isInstantWin"`
}

// ─── Match State ────────────────────────────────────────────────────

// MatchState is the overall match state. First team to reach TargetScore (150) match points wins.
type MatchState struct {
	// TargetScore is match points needed to win (default 150).
	TargetScore int `json:"targetScore"`
	// Team1MatchPoints is Team1's total match points across all deals.
	Team1MatchPoints int `json:"team1MatchPoints"`
	// Team2MatchPoints is Team2's total match points across all deals.
	Team2MatchPoints int `json:"team2MatchPoints"`
	// CurrentDealer is the current dealer position.
	CurrentDealer PlayerPosition `json:"currentDealer"`
	// IsComplete is whether the match is over.
	IsComplete bool `json:"isComplete"`
	// Winner is the winning team (nil if match is still in progress).
	Winner *Team `json:"winner,omitempty"`
	// CompletedDeals is results of all completed deals in this match.
	CompletedDeals []DealResult `json:"completedDeals"`
}

// ─── Session (internal — used by server.go for deserialization) ─────

type SessionRequest struct {
	Position PlayerPosition `json:"position"`
	MatchID  string         `json:"matchId"`
}

// ─── Bot Contexts (passed to your methods) ──────────────────────────

// ChooseCutContext is the context for Bot.ChooseCut.
type ChooseCutContext struct {
	// DeckSize is the total number of cards in the deck (always 32).
	DeckSize int `json:"deckSize"`
	// MatchState is the current match state.
	MatchState MatchState `json:"matchState"`
}

// CutResult is the cut decision: where to cut and from which end.
type CutResult struct {
	// Position is the cut position (must be between 6 and 26 inclusive).
	Position int `json:"position"`
	// FromTop is true to cut from the top of the deck, false from the bottom.
	FromTop bool `json:"fromTop"`
}

// ChooseNegotiationActionContext is the context for Bot.ChooseNegotiationAction.
type ChooseNegotiationActionContext struct {
	// Hand is your current hand (5 cards during negotiation).
	Hand []Card `json:"hand"`
	// NegotiationState is the full negotiation state including bid history.
	NegotiationState NegotiationState `json:"negotiationState"`
	// MatchState is the current match state.
	MatchState MatchState `json:"matchState"`
	// ValidActions is the list of valid actions you can choose from. Pick exactly one.
	ValidActions []NegotiationActionChoice `json:"validActions"`
}

// ChooseCardContext is the context for Bot.ChooseCard.
type ChooseCardContext struct {
	// Hand is your current hand.
	Hand []Card `json:"hand"`
	// HandState is the current play state (tricks, points, etc.).
	HandState HandState `json:"handState"`
	// MatchState is the current match state.
	MatchState MatchState `json:"matchState"`
	// ValidPlays is the list of cards you are allowed to play. Pick exactly one.
	ValidPlays []Card `json:"validPlays"`
}

// DealStartedContext is a notification that a new deal is starting.
type DealStartedContext struct {
	MatchState MatchState `json:"matchState"`
}

// CardPlayedContext is a notification that a player (any, including you) played a card.
type CardPlayedContext struct {
	// Player is the player who played the card.
	Player PlayerPosition `json:"player"`
	// Card is the card that was played.
	Card Card `json:"card"`
	// HandState is the updated hand state after the card was played.
	HandState HandState `json:"handState"`
	MatchState MatchState `json:"matchState"`
}

// TrickCompletedContext is a notification that a trick was completed.
type TrickCompletedContext struct {
	// CompletedTrick is the completed trick with all 4 cards.
	CompletedTrick TrickState `json:"completedTrick"`
	// Winner is the player who won the trick.
	Winner PlayerPosition `json:"winner"`
	// HandState is the updated hand state after the trick.
	HandState HandState `json:"handState"`
	MatchState MatchState `json:"matchState"`
}

// DealEndedContext is a notification that a deal ended with scoring results.
type DealEndedContext struct {
	// Result is the scoring result for the completed deal.
	Result DealResult `json:"result"`
	// HandState is the final hand state at end of deal.
	HandState HandState `json:"handState"`
	MatchState MatchState `json:"matchState"`
}

// MatchEndedContext is a notification that the match is over.
type MatchEndedContext struct {
	// MatchState is the final match state (check MatchState.Winner).
	MatchState MatchState `json:"matchState"`
}
