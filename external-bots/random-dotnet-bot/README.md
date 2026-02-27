# Random .NET Bot — Giretra Bot Template

A starter template for building a Giretra bot in C# / .NET. This bot plays randomly — use it as a foundation and replace the logic with your own strategy.

## Quick Start

```bash
# Build
dotnet build -c Release

# Run (default port 5062)
dotnet run -c Release

# Run on a custom port
PORT=8080 dotnet run -c Release
```

## Project Structure

| File | Purpose |
|------|---------|
| **`Bot.cs`** | Your game logic. **This is the only file you need to edit.** |
| `BotTypes.cs` | Type definitions for the API (cards, game state, contexts). Read-only reference. |
| `Server.cs` | HTTP server boilerplate. Routes requests to your `Bot` methods. Do not edit. |
| `bot.meta.json` | Bot metadata for the game server (name, build/launch commands). |

## How It Works

Your bot runs as a standalone HTTP server. The game server communicates with it via REST calls:

```
Game Server ──HTTP──► Your Bot (http://localhost:5062)
```

### Lifecycle

1. **Session created** — `POST /api/sessions` — the server tells you your seat position and match ID. One `Bot` instance is created per match.
2. **Decisions** — the server calls your bot when it's your turn:
   - `ChooseCut` — cut the deck before dealing
   - `ChooseNegotiationAction` — bid during negotiation
   - `ChooseCard` — play a card during a trick
3. **Notifications** — the server informs your bot of game events (if enabled in `bot.meta.json`):
   - `OnDealStarted`, `OnCardPlayed`, `OnTrickCompleted`, `OnDealEnded`, `OnMatchEnded`
4. **Session destroyed** — `DELETE /api/sessions/{id}` — match is over, cleanup.

### Enabling Notifications

By default, notifications are disabled for performance. To receive them, list the events you want in `bot.meta.json`:

```json
{
  "notifications": ["deal-started", "card-played", "trick-completed", "deal-ended", "match-ended"]
}
```

Only enable what you actually use — each notification is an HTTP round-trip.

## Writing Your Bot

Edit `Bot.cs`. You have three decision methods and five notification hooks:

### Decision Methods (must return a value)

```csharp
// Cut the deck (position 6–26, from top or bottom)
public CutResult ChooseCut(ChooseCutContext ctx)

// Pick a negotiation action from ctx.ValidActions
public NegotiationActionChoice ChooseNegotiationAction(ChooseNegotiationActionContext ctx)

// Pick a card to play from ctx.ValidPlays
public Card ChooseCard(ChooseCardContext ctx)
```

### Notification Hooks (optional, fire-and-forget)

```csharp
public void OnDealStarted(DealStartedContext ctx)     // New deal begins
public void OnCardPlayed(CardPlayedContext ctx)        // Any player played a card
public void OnTrickCompleted(TrickCompletedContext ctx) // Trick finished, with winner
public void OnDealEnded(DealEndedContext ctx)          // Deal scored
public void OnMatchEnded(MatchEndedContext ctx)        // Match over
```

### Important Notes

- **You are always Bottom.** Your teammate is Top. Opponents are Left and Right.
- **Team1 = Bottom + Top** (you), **Team2 = Left + Right** (opponents).
- **ValidActions / ValidPlays** — always pick from these lists. The server enforces game rules and will reject invalid choices.
- **One Bot instance per match** — you can store state across deals in instance fields.
- **30s timeout** on decisions, 5s on notifications.

## Game Rules Quick Reference

### Deal Flow

1. **Cut** — player to dealer's right cuts the deck (position 6–26)
2. **Deal** — 5 cards each (3+2), then negotiation, then 3 more cards (8 total)
3. **Negotiation** — players bid game modes; ends after 3 consecutive accepts
4. **Play** — 8 tricks, each led by the previous trick's winner

### Game Modes (lowest → highest)

| Mode | Trump | Total Points | Threshold | Match Points |
|------|-------|-------------|-----------|-------------|
| ColourClubs | Clubs | 162 | 82+ | 16 (winner-takes-all) |
| ColourDiamonds | Diamonds | 162 | 82+ | 16 (winner-takes-all) |
| ColourHearts | Hearts | 162 | 82+ | 16 (winner-takes-all) |
| ColourSpades | Spades | 162 | 82+ | 16 (winner-takes-all) |
| NoTrumps | None | 130 | 65+ | 52 (winner-takes-all) |
| AllTrumps | All suits | 258 | 129+ | 26 (split, round to nearest) |

### Card Points

| Card | Trump / AllTrumps | Non-trump / NoTrumps |
|------|------------------|---------------------|
| Jack | 20 | 2 |
| Nine | 14 | 0 |
| Ace | 11 | 11 |
| Ten | 10 | 10 |
| King | 4 | 4 |
| Queen | 3 | 3 |
| 8, 7 | 0 | 0 |

### Card Strength (high → low)

- **Trump / AllTrumps:** J > 9 > A > 10 > K > Q > 8 > 7
- **Non-trump / NoTrumps:** A > 10 > K > Q > J > 9 > 8 > 7

### Scoring Extras

- **Last trick:** +10 card points
- **Sweep (all 8 tricks):** AllTrumps = +35 match points, NoTrumps = +90, Colour = instant match win
- **Double:** match points x2 | **Redouble:** match points x4
- **First to 150 match points wins the match**

## `bot.meta.json` Reference

```json
{
  "name": "my-bot",
  "displayName": "My Bot",
  "pun": "A witty one-liner shown in the UI",
  "notifications": [],
  "init": {
    "command": "dotnet",
    "arguments": "build -c Release"
  },
  "launch": {
    "fileName": "dotnet",
    "arguments": "run --no-build -c Release",
    "startupTimeout": 15,
    "healthEndpoint": "health"
  }
}
```

| Field | Description |
|-------|-------------|
| `name` | Unique identifier (kebab-case) |
| `displayName` | Human-readable name shown in the UI |
| `pun` | Fun tagline displayed alongside your bot |
| `notifications` | Array of event names to receive (empty = no notifications) |
| `init.command/arguments` | Build command run once before launching |
| `launch.fileName/arguments` | Command to start the bot server |
| `launch.startupTimeout` | Seconds to wait for the health endpoint before giving up |
| `launch.healthEndpoint` | Path the server pings to confirm your bot is ready |
