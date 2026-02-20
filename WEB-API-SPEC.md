# Giretra Web API Specification

## Overview

The Giretra Web API provides a REST + WebSocket interface for playing Malagasy Belote card games. REST endpoints handle room management and game actions, while SignalR provides real-time event notifications.

**Base URL:** `http://localhost:5067` (development)

## Authentication

The API uses simple token-based authentication. When you create or join a room, you receive a `clientId` token. Include this in all subsequent requests to identify yourself.

---

## Data Types

### Enums

#### PlayerPosition
```
Bottom = 0   # Team 1
Left = 1     # Team 2
Top = 2      # Team 1
Right = 3    # Team 2
```

#### Team
```
Team1 = 0    # Bottom + Top
Team2 = 1    # Left + Right
```

#### CardSuit
```
Clubs = 0
Diamonds = 1
Hearts = 2
Spades = 3
```

#### CardRank
```
Seven = 7
Eight = 8
Nine = 9
Ten = 10
Jack = 11
Queen = 12
King = 13
Ace = 14
```

#### GameMode
```
ColourClubs = 0
ColourDiamonds = 1
ColourHearts = 2
ColourSpades = 3
NoTrumps = 4
AllTrumps = 5
```

#### MultiplierState
```
Normal = 1      # ×1
Doubled = 2     # ×2
Redoubled = 4   # ×4
```

#### RoomStatus
```
Waiting = 0
Playing = 1
Completed = 2
```

#### DealPhase
```
AwaitingCut = 0
InitialDistribution = 1
Negotiation = 2
FinalDistribution = 3
Playing = 4
Completed = 5
```

#### PendingActionType
```
Cut = 0
Negotiate = 1
PlayCard = 2
```

### Common Objects

#### Card
```json
{
  "rank": "Ace",
  "suit": "Spades",
  "display": "A♠"
}
```

#### PlayedCard
```json
{
  "player": "Bottom",
  "card": { "rank": "Ace", "suit": "Spades", "display": "A♠" }
}
```

#### Trick
```json
{
  "leader": "Bottom",
  "trickNumber": 1,
  "playedCards": [ /* PlayedCard[] */ ],
  "isComplete": false,
  "winner": null
}
```

#### NegotiationAction (in responses)
```json
{
  "actionType": "Announce",  // "Announce" | "Accept" | "Double" | "Redouble"
  "player": "Bottom",
  "mode": "ColourHearts"     // null for Accept
}
```

#### ValidAction
```json
{
  "actionType": "Announce",
  "mode": "ColourHearts"     // null for Accept
}
```

---

## REST API

### Rooms

#### List Rooms
```
GET /api/rooms
```

**Response:**
```json
{
  "rooms": [
    {
      "roomId": "room_abc123",
      "name": "My Room",
      "status": "Waiting",
      "playerCount": 2,
      "watcherCount": 1,
      "playerSlots": [
        { "position": "Bottom", "isOccupied": true, "playerName": "Alice", "isAi": false },
        { "position": "Left", "isOccupied": true, "playerName": "Bob", "isAi": false },
        { "position": "Top", "isOccupied": false, "playerName": null, "isAi": false },
        { "position": "Right", "isOccupied": false, "playerName": null, "isAi": false }
      ],
      "gameId": null,
      "createdAt": "2024-01-15T10:30:00Z"
    }
  ],
  "totalCount": 1
}
```

#### Get Room
```
GET /api/rooms/{roomId}
```

**Response:** Same as room object above.

#### Create Room
```
POST /api/rooms
Content-Type: application/json

{
  "name": "My Room",
  "creatorName": "Alice"
}
```

**Response:**
```json
{
  "clientId": "client_xyz789",
  "position": "Bottom",
  "room": { /* Room object */ }
}
```

#### Delete Room
```
DELETE /api/rooms/{roomId}?clientId={clientId}
```

Only the creator can delete a room, and only before the game starts.

**Response:** `204 No Content`

#### Join Room (as Player)
```
POST /api/rooms/{roomId}/join
Content-Type: application/json

{
  "displayName": "Bob",
  "preferredPosition": "Left"  // optional
}
```

**Response:**
```json
{
  "clientId": "client_abc456",
  "position": "Left",
  "room": { /* Room object */ }
}
```

**Errors:**
- `400` - Room full or game already started

#### Join Room (as Watcher)
```
POST /api/rooms/{roomId}/watch
Content-Type: application/json

{
  "displayName": "Spectator1"
}
```

**Response:**
```json
{
  "clientId": "client_def789",
  "position": null,
  "room": { /* Room object */ }
}
```

#### Leave Room
```
POST /api/rooms/{roomId}/leave
Content-Type: application/json

{
  "clientId": "client_abc456"
}
```

**Response:** `204 No Content`

#### Start Game
```
POST /api/rooms/{roomId}/start
Content-Type: application/json

{
  "clientId": "client_xyz789"
}
```

Only the room creator can start the game. Empty player slots are filled with AI.

**Response:**
```json
{
  "gameId": "game_123abc",
  "roomId": "room_abc123"
}
```

---

### Games

#### Get Game State
```
GET /api/games/{gameId}
```

Returns full public game state (does not include player hands).

**Response:**
```json
{
  "gameId": "game_123abc",
  "roomId": "room_abc123",
  "targetScore": 150,
  "team1MatchPoints": 32,
  "team2MatchPoints": 16,
  "dealer": "Bottom",
  "phase": "Playing",
  "completedDealsCount": 2,
  "gameMode": "ColourHearts",
  "multiplier": "Normal",
  "currentTrick": {
    "leader": "Left",
    "trickNumber": 3,
    "playedCards": [
      { "player": "Left", "card": { "rank": "King", "suit": "Hearts", "display": "K♥" } },
      { "player": "Top", "card": { "rank": "Seven", "suit": "Hearts", "display": "7♥" } }
    ],
    "isComplete": false,
    "winner": null
  },
  "completedTricks": [ /* Trick[] */ ],
  "team1CardPoints": 45,
  "team2CardPoints": 23,
  "negotiationHistory": [ /* NegotiationAction[] */ ],
  "currentBid": null,
  "isComplete": false,
  "winner": null,
  "pendingActionType": "PlayCard",
  "pendingActionPlayer": "Right"
}
```

#### Get Player State
```
GET /api/games/{gameId}/player/{clientId}
```

Returns player-specific view including their hand and valid actions.

**Response:**
```json
{
  "position": "Bottom",
  "hand": [
    { "rank": "Ace", "suit": "Spades", "display": "A♠" },
    { "rank": "Jack", "suit": "Hearts", "display": "J♥" }
    // ... more cards
  ],
  "isYourTurn": true,
  "pendingActionType": "PlayCard",
  "validCards": [
    { "rank": "Jack", "suit": "Hearts", "display": "J♥" }
  ],
  "validActions": null,  // populated during negotiation
  "gameState": { /* GameState object */ }
}
```

When `pendingActionType` is `"Negotiate"`:
```json
{
  "isYourTurn": true,
  "pendingActionType": "Negotiate",
  "validCards": null,
  "validActions": [
    { "actionType": "Accept", "mode": null },
    { "actionType": "Announce", "mode": "ColourHearts" },
    { "actionType": "Announce", "mode": "ColourSpades" },
    { "actionType": "Announce", "mode": "NoTrumps" },
    { "actionType": "Announce", "mode": "AllTrumps" }
  ]
}
```

#### Get Watcher State
```
GET /api/games/{gameId}/watch
```

Returns game state without player hands (only card counts).

**Response:**
```json
{
  "gameState": { /* GameState object */ },
  "playerCardCounts": {
    "Bottom": 5,
    "Left": 5,
    "Top": 5,
    "Right": 5
  }
}
```

#### Submit Cut Decision
```
POST /api/games/{gameId}/cut
Content-Type: application/json

{
  "clientId": "client_xyz789",
  "position": 16,
  "fromTop": true
}
```

- `position`: Number of cards to cut (6-26 inclusive)
- `fromTop`: Cut from top (true) or bottom (false) of deck

**Response:** `200 OK`

**Errors:**
- `400` - Invalid position, not your turn, or wrong phase

#### Submit Negotiation Action
```
POST /api/games/{gameId}/negotiate
Content-Type: application/json

{
  "clientId": "client_xyz789",
  "actionType": "Announce",
  "mode": "ColourHearts"
}
```

Action types:
- `"Accept"` - Accept current bid (no mode needed)
- `"Announce"` - Announce a game mode (requires mode)
- `"Double"` - Double current bid (requires mode)
- `"Redouble"` - Redouble (requires mode)

**Response:** `200 OK`

**Errors:**
- `400` - Invalid action, not your turn, or action not in valid list

#### Submit Card Play
```
POST /api/games/{gameId}/play
Content-Type: application/json

{
  "clientId": "client_xyz789",
  "rank": "Ace",
  "suit": "Spades"
}
```

**Response:** `200 OK`

**Errors:**
- `400` - Invalid card, not your turn, or card not in valid list

---

## SignalR WebSocket API

**Hub URL:** `/hubs/game`

### Client Methods (call these)

#### JoinRoom
```javascript
connection.invoke("JoinRoom", roomId, clientId);
```

Subscribe to room/game events. Call this after connecting.

#### LeaveRoom
```javascript
connection.invoke("LeaveRoom", roomId, clientId);
```

Unsubscribe from room events.

### Server Events (listen to these)

#### PlayerJoined
```json
{
  "roomId": "room_abc123",
  "playerName": "Bob",
  "position": "Left"
}
```

#### PlayerLeft
```json
{
  "roomId": "room_abc123",
  "playerName": "Bob",
  "position": "Left"
}
```

#### GameStarted
```json
{
  "roomId": "room_abc123",
  "gameId": "game_123abc"
}
```

#### YourTurn
Sent only to the specific player who must act.
```json
{
  "gameId": "game_123abc",
  "position": "Bottom",
  "actionType": "PlayCard"
}
```

#### PlayerTurn
Broadcast to all clients when it becomes a player's turn.
```json
{
  "gameId": "game_123abc",
  "position": "Bottom",
  "actionType": "PlayCard"
}
```

#### DealStarted
```json
{
  "gameId": "game_123abc",
  "dealer": "Bottom",
  "dealNumber": 1
}
```

#### CardPlayed
```json
{
  "gameId": "game_123abc",
  "player": "Left",
  "card": { "rank": "King", "suit": "Hearts", "display": "K♥" }
}
```

#### TrickCompleted
```json
{
  "gameId": "game_123abc",
  "trick": { /* Trick object */ },
  "winner": "Bottom",
  "team1CardPoints": 45,
  "team2CardPoints": 23
}
```

#### DealEnded
```json
{
  "gameId": "game_123abc",
  "gameMode": "ColourHearts",
  "team1CardPoints": 98,
  "team2CardPoints": 64,
  "team1MatchPointsEarned": 16,
  "team2MatchPointsEarned": 0,
  "team1TotalMatchPoints": 48,
  "team2TotalMatchPoints": 16,
  "wasSweep": false,
  "sweepingTeam": null
}
```

#### MatchEnded
```json
{
  "gameId": "game_123abc",
  "winner": "Team1",
  "team1MatchPoints": 156,
  "team2MatchPoints": 78,
  "totalDeals": 12
}
```

---

## Game Flow

### 1. Room Setup
1. Create room → receive `clientId`
2. Other players join → receive their `clientId`
3. Connect to SignalR hub and call `JoinRoom`
4. Creator starts game → empty slots filled with AI

### 2. Deal Flow (repeats until match ends)
```
DealStarted event
    ↓
AwaitingCut phase
    → Cutter (dealer's right) receives YourTurn(Cut)
    → Submit cut via POST /cut
    ↓
Negotiation phase
    → Players receive YourTurn(Negotiate) in turn
    → Submit via POST /negotiate
    → Ends after 3 consecutive Accepts
    ↓
Playing phase (8 tricks)
    → Current player receives YourTurn(PlayCard)
    → Submit via POST /play
    → CardPlayed event broadcast
    → TrickCompleted event after 4 cards
    ↓
DealEnded event
```

### 3. Polling vs Events

The API supports both polling and event-driven approaches:

**Event-driven (recommended):**
1. Connect to SignalR
2. Listen for `YourTurn` events
3. Fetch `GET /player/{clientId}` to get valid actions
4. Submit action via POST

**Polling:**
1. Periodically call `GET /player/{clientId}`
2. Check `isYourTurn` flag
3. Submit action when it's your turn

### 4. Timeouts

Players have **2 minutes** to submit each action. If timeout occurs:
- **Cut:** Server uses position 16 from top
- **Negotiate:** Server submits Accept (or first valid action)
- **Play:** Server plays first valid card

---

## Error Handling

All errors return standard HTTP status codes:

| Status | Meaning |
|--------|---------|
| 200 | Success |
| 204 | Success (no content) |
| 400 | Bad request (invalid input, wrong turn, invalid action) |
| 404 | Resource not found |

Error responses include a message:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Invalid cut submission. Either it's not your turn or the game doesn't exist."
}
```

---

## Example: Minimal Client Flow

```python
# 1. Create room
response = POST("/api/rooms", {"name": "Test", "creatorName": "Alice"})
client_id = response["clientId"]
room_id = response["room"]["roomId"]

# 2. Connect SignalR (pseudo-code)
hub = connect_signalr("/hubs/game")
hub.invoke("JoinRoom", room_id, client_id)

# 3. Start game (AI fills empty slots)
response = POST(f"/api/rooms/{room_id}/start", {"clientId": client_id})
game_id = response["gameId"]

# 4. Game loop
hub.on("YourTurn", lambda event:
    if event["actionType"] == "Cut":
        POST(f"/api/games/{game_id}/cut", {
            "clientId": client_id,
            "position": 16,
            "fromTop": True
        })
    elif event["actionType"] == "Negotiate":
        state = GET(f"/api/games/{game_id}/player/{client_id}")
        action = state["validActions"][0]  # pick first valid
        POST(f"/api/games/{game_id}/negotiate", {
            "clientId": client_id,
            "actionType": action["actionType"],
            "mode": action.get("mode")
        })
    elif event["actionType"] == "PlayCard":
        state = GET(f"/api/games/{game_id}/player/{client_id}")
        card = state["validCards"][0]  # play first valid
        POST(f"/api/games/{game_id}/play", {
            "clientId": client_id,
            "rank": card["rank"],
            "suit": card["suit"]
        })
)

hub.on("MatchEnded", lambda event:
    print(f"Winner: {event['winner']}")
)
```
