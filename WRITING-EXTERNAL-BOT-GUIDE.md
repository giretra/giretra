# Writing an External Bot for Giretra

This guide walks you through building your own bot that plays Malagasy Belote in the Giretra game engine. Your bot runs as an HTTP server; the engine calls your endpoints when it needs a decision or wants to notify you of game events.

## Architecture

```
┌─────────────┐  HTTP requests   ┌────────────────┐
│  Giretra     │ ──────────────> │  Your Bot      │
│  Game Engine │ <────────────── │  (HTTP server)  │
│  (client)    │  JSON responses │                 │
└─────────────┘                  └────────────────┘
```

- **The engine is the HTTP client.** Your bot is the HTTP server.
- **The engine drives everything:** turn order, rule validation, timeouts.
- **Your bot just responds** to requests with a valid choice from the options provided.

## Quick Start

Your bot server must expose these endpoints under a base URL (e.g. `http://localhost:5050`):

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/health` | Health check (return `200 OK`) |
| `POST` | `/api/sessions` | Create a session for a new match |
| `DELETE` | `/api/sessions/{sessionId}` | Destroy session on match end |
| `POST` | `/api/sessions/{sessionId}/choose-cut` | Decide where to cut the deck |
| `POST` | `/api/sessions/{sessionId}/choose-negotiation-action` | Choose a bid/accept/double/redouble |
| `POST` | `/api/sessions/{sessionId}/choose-card` | Play a card |
| `POST` | `/api/sessions/{sessionId}/notify/deal-started` | Notification: new deal |
| `POST` | `/api/sessions/{sessionId}/notify/card-played` | Notification: a card was played |
| `POST` | `/api/sessions/{sessionId}/notify/trick-completed` | Notification: trick finished |
| `POST` | `/api/sessions/{sessionId}/notify/deal-ended` | Notification: deal scored |
| `POST` | `/api/sessions/{sessionId}/notify/match-ended` | Notification: match over |

## JSON Conventions

- **Content-Type:** `application/json` (UTF-8)
- **Property names:** `camelCase`
- **Enums:** serialized as strings (e.g. `"AllTrumps"`, `"Jack"`, `"Hearts"`)
- **Null fields:** omitted from the request body
- **Comparison is case-insensitive** for enum values in responses

## Enum Reference

### CardRank

`"Seven"` | `"Eight"` | `"Nine"` | `"Ten"` | `"Jack"` | `"Queen"` | `"King"` | `"Ace"`

### CardSuit

`"Clubs"` | `"Diamonds"` | `"Hearts"` | `"Spades"`

### GameMode (ordered lowest to highest)

`"ColourClubs"` < `"ColourDiamonds"` < `"ColourHearts"` < `"ColourSpades"` < `"NoTrumps"` < `"AllTrumps"`

### PlayerPosition (clockwise)

`"Bottom"` -> `"Left"` -> `"Top"` -> `"Right"`

Teams: `"Bottom"` + `"Top"` = Team1, `"Left"` + `"Right"` = Team2.

### Team

`"Team1"` | `"Team2"`

### MultiplierState

`"Normal"` (x1) | `"Doubled"` (x2) | `"Redoubled"` (x4)

## Session Lifecycle

A match consists of multiple deals. For each bot, the lifecycle is:

```
1. Engine calls POST /api/sessions          -> you return a sessionId
2. For each deal:
   a. Engine calls notify/deal-started
   b. Engine calls choose-cut               (only for the cutter)
   c. Engine calls choose-negotiation-action (multiple rounds until 3 accepts)
   d. Engine calls choose-card              (8 tricks x 4 players = up to 32 calls)
      - After each card: notify/card-played
      - After each trick: notify/trick-completed
   e. Engine calls notify/deal-ended
3. Engine calls notify/match-ended
4. Engine calls DELETE /api/sessions/{sessionId}
```

## Endpoint Details

### 1. Create Session

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

- `position` — the seat your bot occupies (`"Bottom"`, `"Left"`, `"Top"`, or `"Right"`)
- `matchId` — unique match identifier (use it to isolate state if handling multiple games)

**Response:** `201 Created`
```json
{
  "sessionId": "your-unique-session-id"
}
```

Generate any string you want as the session ID. The engine uses it for all subsequent calls.

### 2. Destroy Session

```
DELETE /api/sessions/{sessionId}
```

**Response:** `204 No Content`

Must be idempotent — return `204` even for unknown session IDs.

### 3. Choose Cut

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

**Response:** `200 OK`
```json
{
  "position": 16,
  "fromTop": true
}
```

- `position` — number of cards to cut, between **6 and 26** inclusive
- `fromTop` — `true` to cut from the top of the deck, `false` from the bottom

### 4. Choose Negotiation Action

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
  "negotiationState": {
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
  },
  "matchState": { ... },
  "validActions": [
    { "type": "Announcement", "mode": "ColourSpades" },
    { "type": "Announcement", "mode": "NoTrumps" },
    { "type": "Announcement", "mode": "AllTrumps" },
    { "type": "Accept" }
  ]
}
```

**Response:** `200 OK` — must be **one of the `validActions`**:
```json
{ "type": "Announcement", "mode": "AllTrumps" }
```

The four action types are:

| Type | Fields | Meaning |
|------|--------|---------|
| `Announcement` | `mode` | Bid a game mode (must be higher than current bid) |
| `Accept` | *(none)* | Accept the current bid |
| `Double` | `targetMode` | Double an opponent's bid |
| `Redouble` | `targetMode` | Counter a Double on your team's bid |

**Important:** The `player` field is present in `negotiationState.actions` (history) but **omitted** from `validActions` and your response — the engine fills it in.

### 5. Choose Card

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
  "handState": {
    "gameMode": "AllTrumps",
    "team1CardPoints": 45,
    "team2CardPoints": 30,
    "team1TricksWon": 3,
    "team2TricksWon": 2,
    "currentTrick": {
      "leader": "Left",
      "trickNumber": 6,
      "playedCards": [
        { "player": "Left", "card": { "rank": "Ace", "suit": "Spades" } }
      ],
      "isComplete": false
    },
    "completedTricks": [ ... ]
  },
  "matchState": { ... },
  "validPlays": [
    { "rank": "Nine", "suit": "Spades" }
  ]
}
```

**Response:** `200 OK` — must be **one of the `validPlays`**:
```json
{ "rank": "Nine", "suit": "Spades" }
```

### 6-10. Notification Endpoints

Notifications inform your bot of game events. Return `200 OK` (body is ignored). A stateless bot can return `200 OK` immediately without processing.

| Endpoint | Body fields |
|----------|-------------|
| `notify/deal-started` | `matchState` |
| `notify/card-played` | `player`, `card`, `handState`, `matchState` |
| `notify/trick-completed` | `completedTrick`, `winner`, `handState`, `matchState` |
| `notify/deal-ended` | `result` (DealResult), `handState`, `matchState` |
| `notify/match-ended` | `matchState` |

## Data Types on the Wire

### Card
```json
{ "rank": "Jack", "suit": "Hearts" }
```

### PlayedCard
```json
{ "player": "Bottom", "card": { "rank": "Ace", "suit": "Hearts" } }
```

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

### HandState
```json
{
  "gameMode": "AllTrumps",
  "team1CardPoints": 45,
  "team2CardPoints": 30,
  "team1TricksWon": 3,
  "team2TricksWon": 2,
  "currentTrick": { ... },
  "completedTricks": [ ... ]
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
  "actions": [ ... ],
  "doubledModes": {},
  "redoubledModes": [],
  "teamColourAnnouncements": { "Team1": "ColourHearts" }
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
  "completedDeals": [ ... ]
}
```

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
  "isInstantWin": false
}
```

## Complete Example: Minimal Python Bot

Below is a fully working bot using Flask. It plays randomly from the valid options — a good starting point.

```python
import uuid
from flask import Flask, request, jsonify
import random

app = Flask(__name__)
sessions = {}

@app.route("/health", methods=["GET"])
def health():
    return "", 200

@app.route("/api/sessions", methods=["POST"])
def create_session():
    data = request.json
    session_id = str(uuid.uuid4())
    sessions[session_id] = {
        "position": data["position"],
        "match_id": data["matchId"],
    }
    return jsonify({"sessionId": session_id}), 201

@app.route("/api/sessions/<session_id>", methods=["DELETE"])
def destroy_session(session_id):
    sessions.pop(session_id, None)
    return "", 204

@app.route("/api/sessions/<session_id>/choose-cut", methods=["POST"])
def choose_cut(session_id):
    position = random.randint(6, 26)
    from_top = random.choice([True, False])
    return jsonify({"position": position, "fromTop": from_top})

@app.route("/api/sessions/<session_id>/choose-negotiation-action", methods=["POST"])
def choose_negotiation_action(session_id):
    data = request.json
    valid_actions = data["validActions"]
    chosen = random.choice(valid_actions)
    return jsonify(chosen)

@app.route("/api/sessions/<session_id>/choose-card", methods=["POST"])
def choose_card(session_id):
    data = request.json
    valid_plays = data["validPlays"]
    chosen = random.choice(valid_plays)
    return jsonify(chosen)

# Notifications — just acknowledge
@app.route("/api/sessions/<session_id>/notify/<path:event>", methods=["POST"])
def notify(session_id, event):
    return "", 200

if __name__ == "__main__":
    app.run(port=5050)
```

Run it:
```bash
pip install flask
python bot.py
```

## Complete Example: Minimal JavaScript Bot

```javascript
const express = require("express");
const { v4: uuidv4 } = require("uuid");

const app = express();
app.use(express.json());

const sessions = new Map();

app.get("/health", (req, res) => res.sendStatus(200));

app.post("/api/sessions", (req, res) => {
  const sessionId = uuidv4();
  sessions.set(sessionId, {
    position: req.body.position,
    matchId: req.body.matchId,
  });
  res.status(201).json({ sessionId });
});

app.delete("/api/sessions/:sessionId", (req, res) => {
  sessions.delete(req.params.sessionId);
  res.sendStatus(204);
});

app.post("/api/sessions/:sessionId/choose-cut", (req, res) => {
  const position = Math.floor(Math.random() * 21) + 6; // 6-26
  res.json({ position, fromTop: Math.random() > 0.5 });
});

app.post("/api/sessions/:sessionId/choose-negotiation-action", (req, res) => {
  const actions = req.body.validActions;
  res.json(actions[Math.floor(Math.random() * actions.length)]);
});

app.post("/api/sessions/:sessionId/choose-card", (req, res) => {
  const plays = req.body.validPlays;
  res.json(plays[Math.floor(Math.random() * plays.length)]);
});

// Catch-all for notifications
app.post("/api/sessions/:sessionId/notify/:event", (req, res) => {
  res.sendStatus(200);
});

app.listen(5050, () => console.log("Bot running on port 5050"));
```

## Game Rules Cheat Sheet

Understanding these rules helps you make better decisions. The engine enforces all rules — the `validActions`/`validPlays` arrays already filter out illegal moves.

### Negotiation

- Players bid **game modes** in ascending order: ColourClubs < ColourDiamonds < ColourHearts < ColourSpades < NoTrumps < AllTrumps.
- You can only announce a mode **higher** than the current bid.
- Each team can only announce **one Colour** mode per deal.
- Accepting NoTrumps or ColourClubs by an opponent auto-triggers a Double.
- Redouble is only available for AllTrumps, Spades, Hearts, and Diamonds (not NoTrumps or ColourClubs).
- Negotiation ends after **3 consecutive Accepts**.

### Playing

- **Follow suit** if you can (all modes).
- **AllTrumps:** must play a higher card of the same suit if able.
- **Colour mode:** must trump if you can't follow suit (exception: your teammate is winning with a non-trump card). Must overtrump if trump was already played and you can beat it.
- **NoTrumps:** only must follow suit — no obligation to beat.

### Card Rankings

**Trump / AllTrumps:** J > 9 > A > 10 > K > Q > 8 > 7
**Non-trump / NoTrumps:** A > 10 > K > Q > J > 9 > 8 > 7

### Card Point Values

| Card | Trump/AllTrumps | Non-trump/NoTrumps |
|------|:-:|:-:|
| Jack | 20 | 2 |
| Nine | 14 | 0 |
| Ace | 11 | 11 |
| Ten | 10 | 10 |
| King | 4 | 4 |
| Queen | 3 | 3 |
| 8, 7 | 0 | 0 |

### Scoring

| Mode | Total points | Threshold to win | Base match points |
|------|:--:|:--:|:--:|
| AllTrumps | 258 | 129+ | 26 (split proportionally) |
| NoTrumps | 130 | 65+ | 52 (winner-takes-all) |
| Colour | 162 | 82+ | 16 (winner-takes-all) |
| ColourClubs | 162 | 82+ | 32 (winner-takes-all) |

- **Last trick bonus:** +10 card points
- **Sweep (all 8 tricks):** AllTrumps +35 match points, NoTrumps +90, Colour = instant match win
- First team to **150 match points** wins the match

## Timeouts

The engine enforces timeouts (not communicated to the bot):

- **Decision endpoints** (choose-cut, choose-negotiation-action, choose-card): default **30 seconds**
- **Notification endpoints** (notify/*): default **5 seconds**

If your bot exceeds these, the engine may abort the match.

## Stateless vs Stateful Bots

### Stateless

The simplest approach. Every decision endpoint receives the full game state — your hand, the current trick, match scores, negotiation history. You can make a decision purely from the request data.

- Ignore all `/notify/*` endpoints (just return `200 OK`).
- No session state needed beyond storing the position.

### Stateful

For smarter play, track information across calls:

- **Card tracking:** on each `notify/card-played`, record which cards have been played. This lets you know which cards remain in opponents' hands.
- **Void inference:** if a player doesn't follow suit, they are void in that suit for the rest of the deal.
- **Partner signals:** observe what your teammate leads and plays to infer their hand strength.
- **Reset on `notify/deal-started`:** clear per-deal state at the start of each deal.

## Registering Your Bot

To use your bot with the Giretra engine, you instantiate a `RemotePlayerAgentFactory` pointing to your server:

```csharp
var factory = new RemotePlayerAgentFactory(
    baseUrl: "http://localhost:5050",
    agentName: "my-cool-bot",
    displayName: "My Cool Bot",
    pun: "I was born to play cards"
);
```

### Auto-launch Mode

If your bot is a standalone executable, the engine can launch it automatically and poll the health endpoint until it's ready:

```csharp
var factory = new RemotePlayerAgentFactory(
    baseUrl: "http://localhost:5050",
    agentName: "my-cool-bot",
    processConfig: new BotProcessConfig
    {
        FileName = "python",
        Arguments = "bot.py",
        WorkingDirectory = "/path/to/bot",
        StartupTimeout = TimeSpan.FromSeconds(30),
        HealthEndpoint = "health"
    }
);

// This launches the process and waits for health check
await factory.InitializeAsync();
```

## Error Handling

| Scenario | Your responsibility |
|----------|-------------------|
| Invalid response (card not in `validPlays`, action not in `validActions`) | Engine rejects it as an error |
| Unknown session in `DELETE` | Return `204` (be idempotent) |
| Unknown session in other endpoints | Return `404` |
| Internal error | Return `500` — engine may retry |
| Overloaded | Return `503` — engine may retry with backoff |

## Checklist

Before testing your bot against the engine:

- [ ] `/health` returns `200 OK`
- [ ] `POST /api/sessions` returns `201` with `{"sessionId": "..."}`
- [ ] `DELETE /api/sessions/{id}` returns `204` (even for unknown IDs)
- [ ] `choose-cut` returns `{"position": N, "fromTop": bool}` where 6 <= N <= 26
- [ ] `choose-negotiation-action` returns one of the provided `validActions`
- [ ] `choose-card` returns one of the provided `validPlays`
- [ ] All `/notify/*` endpoints return `200 OK`
- [ ] Your server handles concurrent sessions (multiple games at once)
- [ ] Decision responses complete within 30 seconds
- [ ] Notification responses complete within 5 seconds

## Further Reference

- Full API specification: [`docs/remote-bot-api.md`](docs/remote-bot-api.md)
- Source code: `Giretra.Core/Players/Agents/Remote/` (RemoteBotClient, RemotePlayerAgent, BotProcessConfig)
- Built-in bot examples: `Giretra.Core/Players/Agents/` (RandomPlayerAgent, CalculatingPlayerAgent, DeterministicPlayerAgent)
