# Remote Bot Player API Specification

> **Version:** 1.0
> **Protocol:** HTTP/1.1 (HTTPS strongly recommended)
> **Content-Type:** `application/json`
> **Encoding:** UTF-8

## Overview

This document specifies the HTTP API that a remote bot server must implement to participate as a player in Giretra (Malagasy Belote). The game engine acts as the HTTP client, driving the full match lifecycle through request/response pairs.

### Design Principles

- **Session-based**: Each bot instance in a game is represented by a session. The server may handle many concurrent sessions across different games.
- **Engine-driven**: The game engine (client) controls all timing, turn order, and validation. The server simply responds to requests.
- **Rich context**: Decision endpoints include enough game state for stateless implementations. Observation events are also sent, enabling stateful bots that track cards, infer voids, etc.
- **Synchronous request/response**: Every call expects a JSON response. The engine blocks until a response is received (subject to client-side timeouts).

### Timing & Timeout Responsibilities

| Concern | Handled by |
|---------|------------|
| Turn timeout / clock | Game engine (client) |
| HTTP request timeout | Game engine (client) |
| Request retry / fallback | Game engine (client) |
| Concurrent session isolation | Bot server |

---

## Base URL

All endpoints are relative to the bot server's base URL, e.g. `https://bot.example.com/api`.

---

## Common Types

### Card

```json
{ "rank": "Jack", "suit": "Hearts" }
```

| Field | Type | Values |
|-------|------|--------|
| `rank` | string | `Seven`, `Eight`, `Nine`, `Ten`, `Jack`, `Queen`, `King`, `Ace` |
| `suit` | string | `Clubs`, `Diamonds`, `Hearts`, `Spades` |

### PlayerPosition

```
"Bottom" | "Left" | "Top" | "Right"
```

Clockwise order: Bottom → Left → Top → Right.
Teams: Bottom+Top = Team1, Left+Right = Team2.

### Team

```
"Team1" | "Team2"
```

### GameMode

```
"ColourClubs" | "ColourDiamonds" | "ColourHearts" | "ColourSpades" | "NoTrumps" | "AllTrumps"
```

Ordered from lowest to highest (ColourClubs < ColourDiamonds < ColourHearts < ColourSpades < NoTrumps < AllTrumps).

### MultiplierState

```
"Normal" | "Doubled" | "Redoubled"
```

### PlayedCard

```json
{ "player": "Bottom", "card": { "rank": "Ace", "suit": "Hearts" } }
```

### NegotiationAction

Discriminated by the `type` field:

```json
{ "type": "Announcement", "player": "Bottom", "mode": "AllTrumps" }
{ "type": "Accept", "player": "Left" }
{ "type": "Double", "player": "Top", "targetMode": "ColourHearts" }
{ "type": "Redouble", "player": "Bottom", "targetMode": "ColourHearts" }
```

| Type | Fields |
|------|--------|
| `Announcement` | `player`, `mode` (GameMode) |
| `Accept` | `player` |
| `Double` | `player`, `targetMode` (GameMode) |
| `Redouble` | `player`, `targetMode` (GameMode) |

### TrickState

```json
{
  "leader": "Bottom",
  "trickNumber": 3,
  "playedCards": [
    { "player": "Bottom", "card": { "rank": "Ace", "suit": "Hearts" } },
    { "player": "Left", "card": { "rank": "Seven", "suit": "Hearts" } }
  ],
  "isComplete": false
}
```

### NegotiationState

```json
{
  "dealer": "Left",
  "currentPlayer": "Top",
  "currentBid": "ColourHearts",
  "currentBidder": "Bottom",
  "consecutiveAccepts": 1,
  "hasDoubleOccurred": false,
  "actions": [
    { "type": "Announcement", "player": "Bottom", "mode": "ColourHearts" },
    { "type": "Accept", "player": "Left" }
  ],
  "doubledModes": {},
  "redoubledModes": [],
  "teamColourAnnouncements": { "Team1": "ColourHearts" }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `dealer` | PlayerPosition | Dealer for this deal |
| `currentPlayer` | PlayerPosition | Whose turn it is |
| `currentBid` | GameMode? | Highest bid so far (null if none) |
| `currentBidder` | PlayerPosition? | Who made the highest bid (null if none) |
| `consecutiveAccepts` | int | Number of consecutive Accept actions (3 = negotiation ends) |
| `hasDoubleOccurred` | bool | Whether any Double/Redouble has occurred |
| `actions` | NegotiationAction[] | Full action history in order |
| `doubledModes` | object | Map of GameMode → action index when doubled |
| `redoubledModes` | string[] | List of GameMode values that have been redoubled |
| `teamColourAnnouncements` | object | Map of Team → GameMode for Colour announcements |

### HandState

```json
{
  "gameMode": "AllTrumps",
  "team1CardPoints": 45,
  "team2CardPoints": 30,
  "team1TricksWon": 3,
  "team2TricksWon": 2,
  "currentTrick": {
    "leader": "Bottom",
    "trickNumber": 6,
    "playedCards": [
      { "player": "Bottom", "card": { "rank": "Ace", "suit": "Hearts" } }
    ],
    "isComplete": false
  },
  "completedTricks": [
    {
      "leader": "Left",
      "trickNumber": 1,
      "playedCards": [ ... ],
      "isComplete": true
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `gameMode` | GameMode | The resolved game mode for this deal |
| `team1CardPoints` | int | Card points earned by Team1 so far |
| `team2CardPoints` | int | Card points earned by Team2 so far |
| `team1TricksWon` | int | Tricks won by Team1 |
| `team2TricksWon` | int | Tricks won by Team2 |
| `currentTrick` | TrickState? | Current trick in progress (null if hand is complete) |
| `completedTricks` | TrickState[] | All completed tricks in order |

### DealResult

```json
{
  "gameMode": "AllTrumps",
  "multiplier": "Doubled",
  "announcerTeam": "Team1",
  "team1CardPoints": 180,
  "team2CardPoints": 78,
  "team1MatchPoints": 52,
  "team2MatchPoints": 0,
  "wasSweep": false,
  "sweepingTeam": null,
  "isInstantWin": false
}
```

### MatchState

```json
{
  "targetScore": 150,
  "team1MatchPoints": 42,
  "team2MatchPoints": 38,
  "currentDealer": "Left",
  "isComplete": false,
  "winner": null,
  "completedDeals": [
    {
      "gameMode": "AllTrumps",
      "multiplier": "Normal",
      "announcerTeam": "Team1",
      "team1CardPoints": 180,
      "team2CardPoints": 78,
      "team1MatchPoints": 26,
      "team2MatchPoints": 0,
      "wasSweep": false,
      "sweepingTeam": null,
      "isInstantWin": false
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `targetScore` | int | Points needed to win (starts at 150, increases on tie) |
| `team1MatchPoints` | int | Team1 cumulative match points |
| `team2MatchPoints` | int | Team2 cumulative match points |
| `currentDealer` | PlayerPosition | Current dealer position |
| `isComplete` | bool | Whether the match has ended |
| `winner` | Team? | Winning team (null if not complete) |
| `completedDeals` | DealResult[] | Results of all completed deals |

---

## Endpoints

### 1. Create Session

Creates a new bot session for a game. Called once per bot per match.

```
POST /api/sessions
```

**Request:**

```json
{
  "position": "Bottom",
  "matchId": "550e8400-e29b-41d4-a716-446655440000"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `position` | PlayerPosition | yes | The table position assigned to this bot |
| `matchId` | string | yes | Unique identifier for the match (for server-side correlation) |

**Response:** `201 Created`

```json
{
  "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `sessionId` | string | Unique session identifier (used in all subsequent requests) |

---

### 2. Destroy Session

Destroys a bot session when the match ends. Called once per bot per match.

```
DELETE /api/sessions/{sessionId}
```

**Response:** `204 No Content`

The server should release all state associated with this session. Destroying an already-destroyed or unknown session should return `204` (idempotent).

---

### 3. Choose Cut

Called when the bot must cut the deck (player to dealer's right).

```
POST /api/sessions/{sessionId}/choose-cut
```

**Request:**

```json
{
  "deckSize": 32,
  "matchState": { ... }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `deckSize` | int | Number of cards in the deck (always 32) |
| `matchState` | MatchState | Current match state |

**Response:** `200 OK`

```json
{
  "position": 16,
  "fromTop": true
}
```

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| `position` | int | 6–26 inclusive | Number of cards to cut |
| `fromTop` | bool | | `true` = cut from top, `false` = cut from bottom |

---

### 4. Choose Negotiation Action

Called when the bot must make a negotiation decision (announce, accept, double, or redouble).

```
POST /api/sessions/{sessionId}/choose-negotiation-action
```

**Request:**

```json
{
  "hand": [
    { "rank": "Jack", "suit": "Hearts" },
    { "rank": "Ace", "suit": "Spades" },
    { "rank": "Nine", "suit": "Hearts" },
    { "rank": "Ten", "suit": "Clubs" },
    { "rank": "Seven", "suit": "Diamonds" }
  ],
  "negotiationState": { ... },
  "matchState": { ... },
  "validActions": [
    { "type": "Announcement", "mode": "AllTrumps" },
    { "type": "Announcement", "mode": "NoTrumps" },
    { "type": "Accept" },
    { "type": "Double", "targetMode": "ColourHearts" }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `hand` | Card[] | The bot's current hand (5 cards during negotiation) |
| `negotiationState` | NegotiationState | Full negotiation state |
| `matchState` | MatchState | Current match state |
| `validActions` | NegotiationAction[] | The legal actions the bot may choose from (without `player` field — it is always the bot's position) |

**Response:** `200 OK`

The response must be one of the `validActions`. The `player` field is omitted (the engine fills it in).

```json
{ "type": "Announcement", "mode": "AllTrumps" }
```

or

```json
{ "type": "Accept" }
```

or

```json
{ "type": "Double", "targetMode": "ColourHearts" }
```

or

```json
{ "type": "Redouble", "targetMode": "ColourHearts" }
```

---

### 5. Choose Card

Called when the bot must play a card.

```
POST /api/sessions/{sessionId}/choose-card
```

**Request:**

```json
{
  "hand": [
    { "rank": "Jack", "suit": "Hearts" },
    { "rank": "Nine", "suit": "Spades" },
    { "rank": "Ace", "suit": "Clubs" }
  ],
  "handState": { ... },
  "matchState": { ... },
  "validPlays": [
    { "rank": "Jack", "suit": "Hearts" },
    { "rank": "Nine", "suit": "Spades" }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `hand` | Card[] | The bot's current hand |
| `handState` | HandState | Current hand state (tricks, points, current trick) |
| `matchState` | MatchState | Current match state |
| `validPlays` | Card[] | The legal cards the bot may play |

**Response:** `200 OK`

The response must be one of the `validPlays`.

```json
{ "rank": "Jack", "suit": "Hearts" }
```

---

## Observation Events

These endpoints notify the bot of game events. They allow stateful bots to track played cards, infer voids, observe partner signals, etc. The engine waits for a `200 OK` acknowledgment before proceeding (to guarantee event ordering).

A stateless bot may simply return `200 OK` with an empty body for all observation events.

---

### 6. Deal Started

Called at the beginning of each deal, before the cut. The bot should reset any per-deal internal state.

```
POST /api/sessions/{sessionId}/notify/deal-started
```

**Request:**

```json
{
  "matchState": { ... }
}
```

**Response:** `200 OK` (body ignored)

---

### 7. Card Played

Called after every card is played by any player (including the bot itself).

```
POST /api/sessions/{sessionId}/notify/card-played
```

**Request:**

```json
{
  "player": "Left",
  "card": { "rank": "Ace", "suit": "Hearts" },
  "handState": { ... },
  "matchState": { ... }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `player` | PlayerPosition | Who played the card |
| `card` | Card | The card that was played |
| `handState` | HandState | Hand state after the card was played |
| `matchState` | MatchState | Current match state |

**Response:** `200 OK` (body ignored)

---

### 8. Trick Completed

Called after each trick completes (all 4 cards played).

```
POST /api/sessions/{sessionId}/notify/trick-completed
```

**Request:**

```json
{
  "completedTrick": {
    "leader": "Bottom",
    "trickNumber": 3,
    "playedCards": [
      { "player": "Bottom", "card": { "rank": "Jack", "suit": "Hearts" } },
      { "player": "Left", "card": { "rank": "Nine", "suit": "Hearts" } },
      { "player": "Top", "card": { "rank": "Seven", "suit": "Hearts" } },
      { "player": "Right", "card": { "rank": "Ace", "suit": "Hearts" } }
    ],
    "isComplete": true
  },
  "winner": "Bottom",
  "handState": { ... },
  "matchState": { ... }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `completedTrick` | TrickState | The completed trick with all 4 cards |
| `winner` | PlayerPosition | Who won the trick |
| `handState` | HandState | Hand state after trick completion |
| `matchState` | MatchState | Current match state |

**Response:** `200 OK` (body ignored)

---

### 9. Deal Ended

Called when a deal completes (all 8 tricks played and scored).

```
POST /api/sessions/{sessionId}/notify/deal-ended
```

**Request:**

```json
{
  "result": { ... },
  "handState": { ... },
  "matchState": { ... }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `result` | DealResult | Scoring result of the completed deal |
| `handState` | HandState | Final hand state with all 8 completed tricks |
| `matchState` | MatchState | Match state after applying deal result |

**Response:** `200 OK` (body ignored)

---

### 10. Match Ended

Called when the match ends (a team reached the target score).

```
POST /api/sessions/{sessionId}/notify/match-ended
```

**Request:**

```json
{
  "matchState": { ... }
}
```

**Response:** `200 OK` (body ignored)

After this event, the engine will call `DELETE /api/sessions/{sessionId}` to clean up.

---

## Error Handling

### Server Errors

| Status | Meaning | Engine Behavior |
|--------|---------|-----------------|
| `200`/`201`/`204` | Success | Continue |
| `400 Bad Request` | Malformed request | Engine bug — log and abort |
| `404 Not Found` | Unknown session | Session expired or invalid — abort |
| `422 Unprocessable Entity` | Invalid response (e.g. illegal card) | Log and retry or abort |
| `500 Internal Server Error` | Server failure | Retry up to configured limit, then abort |
| `503 Service Unavailable` | Server overloaded | Retry with backoff, then abort |

### Invalid Responses

If the bot returns a response that doesn't match one of the `validActions` or `validPlays`, the engine treats this as an error. The engine is responsible for all game rule validation.

### Timeouts

The engine enforces timeouts on all HTTP calls. Timeout values are configured on the engine side and are not communicated to the bot. If a request times out, the engine may:
- Retry the request
- Forfeit the bot's turn (implementation-defined)
- Abort the match

---

## Lifecycle Summary

```
┌──────────────────────────────────────────────────────────┐
│ Match Start                                              │
│   POST /api/sessions                → sessionId          │
│                                                          │
│   ┌────────────────────────────────────────────────────┐ │
│   │ Deal (repeated until match ends)                   │ │
│   │                                                    │ │
│   │   POST .../notify/deal-started                     │ │
│   │   POST .../choose-cut              (cutter only)   │ │
│   │                                                    │ │
│   │   ┌──────────────────────────────────────────────┐ │ │
│   │   │ Negotiation (repeated until 3 accepts)       │ │ │
│   │   │   POST .../choose-negotiation-action         │ │ │
│   │   └──────────────────────────────────────────────┘ │ │
│   │                                                    │ │
│   │   ┌──────────────────────────────────────────────┐ │ │
│   │   │ Trick (repeated 8 times)                     │ │ │
│   │   │   POST .../choose-card         (per player)  │ │ │
│   │   │   POST .../notify/card-played  (all 4)       │ │ │
│   │   │   POST .../notify/trick-completed            │ │ │
│   │   └──────────────────────────────────────────────┘ │ │
│   │                                                    │ │
│   │   POST .../notify/deal-ended                       │ │
│   └────────────────────────────────────────────────────┘ │
│                                                          │
│   POST .../notify/match-ended                            │
│   DELETE /api/sessions/{sessionId}                        │
└──────────────────────────────────────────────────────────┘
```

---

## Implementation Notes

### For Bot Server Implementors

1. **Stateless bots**: If your bot is stateless (evaluates only the current request), you may ignore all `/notify/*` events — just return `200 OK` immediately. The decision endpoints include all necessary context.

2. **Stateful bots**: Use `notify/deal-started` to reset per-deal state. Use `notify/card-played` to track played cards and infer opponent voids. Use `notify/trick-completed` to reset per-trick state.

3. **Concurrency**: Your server will receive requests for multiple sessions (different games) concurrently. Ensure session state is properly isolated.

4. **Validation**: The `validActions` and `validPlays` arrays are authoritative. Your response **must** be one of the provided options. The engine validates all responses and will reject illegal moves.

5. **Session cleanup**: Always handle `DELETE` gracefully. The engine calls it at match end, but may also call it if a match is aborted. Implement `DELETE` as idempotent.

### For Game Engine Implementors

1. **Timeouts**: Configure separate timeouts for decision endpoints (longer, e.g. 30s) and observation endpoints (shorter, e.g. 5s).

2. **Retry policy**: Consider retrying on 5xx errors with exponential backoff, but never retry decision endpoints that returned a valid (but wrong) response.

3. **Session lifecycle**: Always create a session before any other call. Always destroy the session when the match ends or is aborted.

4. **The `player` field**: In `validActions`, the `player` field is omitted since it's implied. In `negotiationState.actions` (history), the `player` field is present for all actions.
