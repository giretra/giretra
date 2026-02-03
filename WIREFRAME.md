# Giretra — UI Implementation Specification

## Document purpose

This document describes the screens, layout, component hierarchy, interaction states, and data flow for the Giretra web client — a Malagasy Belote card game. It targets a senior frontend developer and is language/framework agnostic. It does not prescribe technology; it prescribes what to build and why.

The backend API (REST + SignalR) is already specified separately. This document references API endpoints only where the data flow matters for UI behavior.

---

## Architecture overview

The entire application consists of **two screens only**:

1. **Home** — lobby, room list, room creation and joining.
2. **Table** — the game surface. This is a single persistent view that handles every phase of the game: waiting for players, cutting, negotiation, playing tricks, deal summaries, and match end. Phase transitions never cause a route change; only the interactive zone within the table adapts.

This two-screen constraint is deliberate. Belote is fast-paced. Every route transition, modal stack, or page reload breaks flow. The Table screen loads once and stays loaded for the duration of the game session.

---

## Screen 1 — Home

### Purpose

Let a player create a room, join an existing room, or spectate. Get them to the Table screen as quickly as possible.

### Layout

```
┌──────────────────────────────────────────┐
│  GIRETRA                    [Your Name]  │  ← Header bar
├──────────────────────────────────────────┤
│                                          │
│  ┌────────────────────────────────────┐  │
│  │  Create a new room          [ + ]  │  │  ← Primary action, always visible
│  └────────────────────────────────────┘  │
│                                          │
│  Available Rooms                         │  ← Section label
│                                          │
│  ┌────────────────────────────────────┐  │
│  │  Room name        2/4   Waiting    │  │  ← Room card
│  │  ●  ○  ●  ○                 [Join] │  │     Dots = seats (filled/empty)
│  └────────────────────────────────────┘  │
│  ┌────────────────────────────────────┐  │
│  │  Room name        4/4   Playing    │  │
│  │  ●  ●  ●  ●               [Watch] │  │  ← Full room: Join becomes Watch
│  └────────────────────────────────────┘  │
│  ┌────────────────────────────────────┐  │
│  │  Room name        3/4   Waiting    │  │
│  │  ●  ●  ●  ○                [Join] │  │
│  └────────────────────────────────────┘  │
│                                          │
│  (empty state: "No rooms yet — create    │
│   one to get started")                   │
│                                          │
└──────────────────────────────────────────┘
```

### Behavior

- **Name entry.** On first visit, prompt for a display name before showing the lobby. Store it locally. Show it in the header with an option to change.
- **Create room.** Tapping the create action opens an inline form (not a modal) asking for room name only. On submit, call `POST /api/rooms`, receive `clientId`, and navigate immediately to the Table screen.
- **Room cards.** Each card shows: room name, player count as `n/4`, status badge (`Waiting` / `Playing`), four dot indicators for the four seats (filled = occupied, empty = open), and a single action button.
- **Action button logic.** If room status is `Waiting` and seats are available → "Join". If room is full or `Playing` → "Watch". If room is `Completed` → hide the card or show it grayed out with no action.
- **Joining.** Tapping "Join" calls `POST /api/rooms/{roomId}/join`. If `preferredPosition` matters to the player, show a quick inline seat picker (four labeled buttons: Bottom, Left, Top, Right — with unavailable ones disabled) **after** the tap, not before. If the player doesn't care, auto-assign. On success, navigate to the Table screen.
- **Watching.** Tapping "Watch" calls `POST /api/rooms/{roomId}/watch` and navigates to the Table screen in spectator mode.
- **Polling.** Refresh the room list on an interval (every 5 seconds) or via SignalR room events if you want real-time updates. Don't over-engineer this — the lobby is transient.

### Data needed

`GET /api/rooms` → room list with player slot info.

---

## Screen 2 — The Table

### Purpose

This is the entire game. It handles all phases from pre-game waiting through match completion. The player never leaves this screen until they choose to return to the Home screen.

### Foundational layout

The table has four fixed zones that are always present regardless of game phase:

```
┌──────────────────────────────────────────────────┐
│  Score Bar                                       │  ZONE A
├──────────────────────────────────────────────────┤
│            ┌──────────┐                          │
│            │   Top    │                          │
│            │  (ally)  │                          │
│            └──────────┘                          │
│  ┌──────┐                        ┌──────┐       │
│  │ Left │    ┌──────────────┐    │Right │       │  ZONE B
│  │(opp) │    │              │    │(opp) │       │
│  └──────┘    │ Center Stage │    └──────┘       │
│              │              │                    │
│              └──────────────┘                    │
│            ┌──────────────────┐                  │
│            │   You (Bottom)   │                  │
│            └──────────────────┘                  │
├──────────────────────────────────────────────────┤
│  Your Hand / Action Area                         │  ZONE C
│  [card] [card] [card] [card] [card] [card]       │
└──────────────────────────────────────────────────┘
```

**Zone A — Score Bar** (top strip, always visible)

Shows match-level and deal-level information. Detailed specification below.

**Zone B — Table Surface** (middle, largest area)

Contains the four player seats and the center stage. The seats are fixed. The center stage content changes per phase.

**Zone C — Hand / Action Area** (bottom strip)

Displays the player's cards during play, or action controls during other phases. This zone is the primary interactive surface and adapts per phase.

### Zone A — Score Bar (detailed)

```
┌──────────────────────────────────────────────────┐
│  Team 1: 48 pts    ♥ ×2     Deal 3    Team 2: 16 pts │
│  This deal: 45              [menu ☰]  This deal: 23  │
└──────────────────────────────────────────────────┘
```

- **Match points** — `team1MatchPoints` vs `team2MatchPoints`. Highlight the player's own team.
- **Trump indicator** — show the active game mode as a suit icon (♣♦♥♠) or label ("Sans As", "Tout As"). Only visible after negotiation resolves. This is the single most forgotten piece of info during play — make it prominent. Use color (red for hearts/diamonds, dark for clubs/spades).
- **Multiplier badge** — if `multiplier` is `Doubled` or `Redoubled`, show "×2" or "×4" next to the trump indicator.
- **Deal number** — `completedDealsCount + 1`.
- **Deal card points** — `team1CardPoints` vs `team2CardPoints`. Update live as tricks complete.
- **Menu** — a small icon leading to: leave room, game rules reference, sound toggle. Keep it unobtrusive.

When no game is active (waiting phase), the score bar shows a simplified state: room name, player count, and the menu.

### Zone B — Player Seats (detailed)

Each seat is a compact card-like element anchored to its table position.

```
┌─────────────────┐
│  Alice           │  ← Player name
│  🂠🂠🂠🂠🂠       │  ← Card backs (count = cards remaining)
│  ○○○             │  ← Tricks won this deal (dots or small number)
└─────────────────┘
```

- **Name.** Display name from API. For AI players, append a subtle bot icon.
- **Card count.** Show as miniature card back icons. As cards are played, remove one icon. This gives an at-a-glance sense of how many cards each opponent holds without numbers.
- **Tricks won.** A small counter or dots showing how many tricks this player's team has won this deal. Optional — can be shown as a team-level counter elsewhere.
- **Team color.** Apply a subtle left border or background tint. Team 1 (Bottom + Top) = one color. Team 2 (Left + Right) = another. Keep it soft — it's ambient information, not primary.
- **Turn indicator.** When it's a player's turn (`pendingActionPlayer`), highlight their seat with a visible ring, glow, or pulsing border. This is the single most important indicator during play. The active player must be instantly identifiable.
- **Empty seat.** During waiting phase, show as a dashed outline with "Waiting..." or "Open seat".

The **Bottom seat** (you) is positioned just above Zone C. It does not show card backs — your actual hand is in Zone C.

### Zone B — Center Stage

This area in the middle of the four seats is the primary dynamic zone. Its content depends entirely on the current phase.

---

## Phase-specific states

All phases below describe what appears in the **Center Stage** and **Zone C (Hand/Action area)**. Zones A and B remain as described above with minor adaptations noted.

---

### Phase: Waiting for Players

**When:** Room is created but game hasn't started. `roomStatus = Waiting`.

**Center Stage:**

```
┌──────────────────────────┐
│                          │
│    Waiting for players   │
│          2 / 4           │
│                          │
│    [ Start Game ]        │  ← Only visible to room creator
│                          │
│  (empty slots filled     │
│   by AI automatically)   │
│                          │
└──────────────────────────┘
```

- The four seats around the center show occupied/empty states.
- The "Start Game" button is only visible to the room creator (`clientId` matches creator). It's always enabled — the API fills empty slots with AI on start.
- If the current user is a watcher, show "Waiting for host to start..." without the button.

**Zone C:** Empty or shows a subtle "Game hasn't started yet" message.

**Data:** Poll `GET /api/rooms/{roomId}` or use SignalR `PlayerJoined` / `PlayerLeft` events to update seat states.

**Transition:** On SignalR `GameStarted` event → fetch initial game/player state, transition to cut phase.

---

### Phase: Awaiting Cut

**When:** `phase = AwaitingCut`. The player to the dealer's right must cut the deck.

**Center Stage:**

```
┌──────────────────────────┐
│                          │
│      ┌─────────┐         │
│      │ ░░░░░░░ │         │  ← Deck graphic
│      │ ░░░░░░░ │         │
│      │ ░░░░░░░ │         │
│      └─────────┘         │
│                          │
│   Alice is cutting...    │  ← If not your turn
│                          │
└──────────────────────────┘
```

**Zone C (if it's your turn to cut):**

```
┌──────────────────────────────────────────────────┐
│                                                  │
│   Tap to cut the deck          [ Cut ]           │
│                                                  │
└──────────────────────────────────────────────────┘
```

**Key design decision: simplify the cut.**

The API allows choosing a position (6–26) and direction (top/bottom). In practice, the cut is a formality — it has no strategic value. Do not present a slider with 21 options.

Preferred approach: a single "Cut" button. On tap, pick a random position between 6 and 26, always from top, submit `POST /api/games/{gameId}/cut`, and animate the deck splitting.

If you want to give the illusion of choice (some players enjoy the ritual), show the deck slightly fanned and let the player tap a position on the fan. Map the tap position to a number. One gesture, no controls, no labels.

**Zone C (if it's not your turn):** Empty or shows your hand if cards have been dealt already (they haven't at this point — this is pre-deal).

**Transition:** After cut → `InitialDistribution` → `Negotiation`. Cards are dealt. On SignalR event or next poll, fetch player state to get initial hand.

---

### Phase: Negotiation (Bidding)

**When:** `phase = Negotiation`. Players bid in turn to decide the game mode (trump suit or special mode).

**This is the most important phase to get right ergonomically.** Bidding involves fast back-and-forth decisions. The player needs to see: what's been bid so far, whose turn it is, and what their options are — all without leaving the table context.

**Center Stage:**

```
┌──────────────────────────────────────┐
│                                      │
│   Current bid: ♥ (by Left)           │  ← Current highest bid
│                                      │
│   Left: ♥  →  Top: Accept  →        │  ← Compact bid history trail
│   Right: ♠  →  You: ...             │
│                                      │
└──────────────────────────────────────┘
```

Additionally, as each player bids, show a transient speech bubble at their seat position (e.g. "♥" or "Accept" or "×2"). This ties the bidding spatially to the players.

**Zone C (your turn to bid):**

Replace the card display with bid action buttons. The player cannot play cards during negotiation, so the hand area is unused space — repurpose it.

```
┌──────────────────────────────────────────────────┐
│                                                  │
│  [♣] [♦] [♥] [♠] [Sans As] [Tout As]  [Accept]  │
│                                                  │
│                           [Double]  [Redouble]   │
│                                                  │
└──────────────────────────────────────────────────┘
```

**Critical rules for bid buttons:**

- Only render buttons that appear in `validActions` from `GET /api/games/{gameId}/player/{clientId}`. Do not show disabled buttons — **omit invalid options entirely**. Fewer visible options = faster decisions, fewer mistakes.
- Map `actionType: "Announce"` + `mode` to the suit/mode buttons.
- Map `actionType: "Accept"` to the Accept button.
- Map `actionType: "Double"` / `"Redouble"` to their respective buttons. Only show these when available.
- On tap, submit `POST /api/games/{gameId}/negotiate` with the selected action.

**Zone C (not your turn):**

Show your initial hand (the 5 cards dealt so far) as a preview, but no interactive elements. The cards are informational — they help the player decide what to bid when their turn comes. Overlay a subtle "[Player] is bidding..." indicator.

**Transition:** Negotiation ends after 3 consecutive Accepts. On the next state update: if a mode was chosen, the game moves to `FinalDistribution` (remaining cards dealt) then `Playing`. Fetch updated player state to get the full 8-card hand.

If all players Accept without any Announce, the deal is void and a new deal starts. Handle this as a brief center stage message ("No bid — redeal") then auto-transition.

---

### Phase: Playing (Trick Phase)

**When:** `phase = Playing`. This is the core gameplay loop. 8 tricks per deal, 4 cards per trick.

**Center Stage — The Trick Area:**

```
┌──────────────────────────────────────┐
│              [Top's card]            │
│                                      │
│  [Left's card]        [Right's card] │
│                                      │
│             [Your card]              │
└──────────────────────────────────────┘
```

Cards are placed spatially near the player who played them. As each `CardPlayed` event arrives, animate the card from that player's seat into their position in the center.

- Show each card face-up with rank and suit clearly visible.
- The trick builds incrementally: 1 card → 2 → 3 → 4.
- When the 4th card is played (`TrickCompleted` event), briefly highlight the winning card (bold border or glow) for ~1.5 seconds, then animate all 4 cards sliding toward the winning team's area. Update `team1CardPoints` / `team2CardPoints` in the score bar.
- Clear the center for the next trick.

**Zone C — Your Hand (your turn):**

```
┌──────────────────────────────────────────────────┐
│                                                  │
│   [7♣]  [J♥]  [A♠]  [K♦]  [10♥]  [Q♣]  [9♠]   │
│    ↑dim  ↑UP   ↑dim  ↑dim   ↑UP   ↑dim  ↑dim   │
│                                                  │
└──────────────────────────────────────────────────┘
```

**Card states — this is critical for usability:**

- **Playable cards** (`validCards` from player state): visually lifted upward (translate Y by ~10-15px), full color saturation, full opacity. Tap target is generous. Cursor changes to pointer on hover.
- **Non-playable cards**: pushed down, desaturated, and reduced opacity (~0.5). Not tappable. No hover effect.
- The distinction must be **spatial** (up vs down), not just color-based. Color alone is insufficient under varying screen brightness and for colorblind players.

On tap of a valid card, submit `POST /api/games/{gameId}/play` with the card's rank and suit. Animate the card from your hand into the center trick area at the Bottom position.

**Zone C — Your Hand (not your turn):**

All cards are in their neutral position (neither lifted nor pushed down). Full color, but no interactivity. Overlay a subtle turn indicator: "[Player] is thinking..." near the active player's seat, not over your cards.

**Turn ring:** The active player's seat in Zone B must have its turn indicator visible. This is the primary way the player tracks whose turn it is.

**Important animation note:** Card animations should be fast (~300ms). Belote rounds move quickly. Slow animations will feel sluggish by the third deal.

---

### Phase: Deal End Summary

**When:** `DealEnded` SignalR event fires, or `phase = Completed` on the deal.

**Do not use a blocking modal.** Players want to see the result and move on. Show the summary inline in the center stage for 4-5 seconds, then auto-transition to the next deal.

**Center Stage:**

```
┌──────────────────────────────────────┐
│                                      │
│        Deal 3 — Results              │
│                                      │
│   Team 1          │    Team 2        │
│   Card pts: 98    │    Card pts: 64  │
│   Earned: +16     │    Earned: +0    │
│                                      │
│   ──────────────────────────         │
│   Match: 48       │    Match: 16     │
│                                      │
│   (SWEEP! by Team 1)                 │  ← Only if wasSweep = true
│                                      │
└──────────────────────────────────────┘
```

- Animate the score bar updating from old match points to new match points.
- If `wasSweep` is true, add a brief celebratory callout (bold text, not confetti — keep it dignified).
- After the display duration, transition automatically. No "Next" button needed. If you want to give players time to absorb, add a small countdown indicator ("Next deal in 3...").
- Optionally, add a "Details" link that expands a fuller breakdown of the deal (all 8 tricks, card point calculations). Most players won't use it, but competitive players will.

**Zone C:** Show your emptied hand area or a brief summary of your cards from the deal just played (informational, non-interactive).

---

### Phase: Match End

**When:** `MatchEnded` SignalR event fires, or game state shows `isComplete = true`.

**This is the one phase where a blocking overlay is appropriate.** A match ending is a significant event.

**Full-screen overlay over the table:**

```
┌──────────────────────────────────────────────────┐
│                                                  │
│                                                  │
│              🏆  Team 1 Wins!                    │
│                                                  │
│         Final Score: 156 — 78                    │
│         Deals played: 12                         │
│                                                  │
│                                                  │
│         [ Play Again ]     [ Leave Table ]       │
│                                                  │
│                                                  │
└──────────────────────────────────────────────────┘
```

- "Play Again" creates a new game in the same room (calls `POST /api/rooms/{roomId}/start` if you're the creator, or signals readiness otherwise).
- "Leave Table" navigates back to the Home screen.
- Personalize the message: if the player's team won, frame it as a win. If they lost, show the result neutrally.

---

## Watcher / Spectator Mode

The watcher sees the exact same Table screen with these differences:

- **Zone C** does not show a hand. Instead, show a compact bar:

```
┌──────────────────────────────────────────────────┐
│  Spectating                                      │
│  Bottom: 5 cards   Left: 5   Top: 5   Right: 4  │
└──────────────────────────────────────────────────┘
```

Card counts come from `GET /api/games/{gameId}/watch` → `playerCardCounts`.

- **No interactive elements.** No card selection, no bid buttons, no cut button.
- **All phase transitions play out identically** — the watcher sees bids appear, tricks build, summaries display.
- **Score bar** is fully visible. No trump indicator advantage — the watcher sees the same info as players.

---

## Data flow and real-time strategy

### Connection lifecycle

1. On entering the Table screen, connect to SignalR hub at `/hubs/game`.
2. Call `JoinRoom(roomId, clientId)` to subscribe to events.
3. Listen to all server events (`PlayerJoined`, `GameStarted`, `YourTurn`, `CardPlayed`, `TrickCompleted`, `DealEnded`, `MatchEnded`, etc.).
4. On leaving the Table screen, call `LeaveRoom(roomId, clientId)` and disconnect.

### State refresh pattern

Use an **event-driven approach with state refresh on turn**:

1. SignalR events drive UI updates for broadcast information (card played, trick completed, scores).
2. When `YourTurn` fires (or `PlayerTurn` for tracking whose turn it is), fetch `GET /api/games/{gameId}/player/{clientId}` to get the authoritative list of `validCards` or `validActions`.
3. Do not cache valid actions between turns — always fetch fresh on each `YourTurn` event. The valid set changes every trick.

### Optimistic UI

When the player plays a card or submits a bid:

1. Immediately animate the card/bid into position (optimistic update).
2. Fire the POST request.
3. If the request fails (400), revert: snap the card back into the hand, show a brief error toast ("Invalid move — try again"), and re-fetch player state.
4. If the request succeeds, the next SignalR event will confirm the state.

This keeps the UI feeling instant. The round-trip to the server is invisible in the happy path.

### Timeout handling

Players have 2 minutes per action. Show a countdown timer near the active player's seat when it's their turn. When ~15 seconds remain, intensify the indicator (color shift, faster pulse). When timeout occurs, the server auto-plays and the next event will update state normally.

---

## Responsive design considerations

### Mobile (primary target)

- Zone C (hand area) should take up roughly 25-30% of the viewport height. Cards must be large enough to tap without error.
- Center stage takes the remaining middle space. Keep it square-ish.
- Player seats collapse to compact chips (name + card count, no card back icons — just a number).
- Score bar collapses to a single row: "48 — 16 | ♥ ×2 | Deal 3".
- During negotiation, bid buttons should be large enough for thumb taps. Two rows maximum.

### Desktop / Tablet

- More room for decorative elements: textured table background, proper card fan in Zone C, card back sprites for opponents.
- Score bar can expand to two rows with more detail.
- Player seats can show full card back icons.
- Center stage can be more spacious with larger card displays.

### Universal rules

- Cards must have a minimum tap target of 44×44 CSS pixels (Apple HIG minimum).
- The trump suit indicator in the score bar must be visible at all viewport sizes. Never collapse it.
- Turn indicator (whose turn) must be visible at all viewport sizes. Never collapse it.

---

## Component inventory

A summary of distinct UI components to build. Each is reusable across phases.

| Component | Used In | Notes |
|---|---|---|
| Room card | Home screen | Room name, seat dots, status badge, action button |
| Score bar | Table — Zone A | Match points, deal points, trump, multiplier, deal # |
| Player seat | Table — Zone B | Name, card count, team color, turn ring, empty state |
| Card (face up) | Zone B center, Zone C | Rank, suit, color. States: playable, non-playable, neutral |
| Card (back) | Zone B seats | Opponent card count indicator |
| Center stage | Table — Zone B | Container whose children swap per phase |
| Hand strip | Table — Zone C | Horizontal card layout with interaction states |
| Bid button row | Zone C (negotiation) | Dynamic set of valid bid actions |
| Cut control | Zone C (cut phase) | Single tap or simplified fan-tap |
| Trick area | Center stage (playing) | 4 positional slots for played cards |
| Deal summary | Center stage (deal end) | Timed display with score breakdown |
| Match end overlay | Full screen over table | Winner, final score, navigation actions |
| Speech bubble | Zone B seats | Transient bid/action indicator at player position |
| Turn timer | Zone B seats | Countdown ring or bar near active player |
| Watcher bar | Zone C (spectator) | Card counts per player, spectating label |

---

## State machine summary

The Table screen's behavior is driven by a single state machine. The current phase determines which center stage content and Zone C content to render.

```
[WAITING] ──GameStarted──▶ [CUT] ──cut submitted──▶ [NEGOTIATION]
                                                         │
                                        all accept ──▶ [PLAYING]
                                        no bid ────▶ [CUT] (redeal)
                                                         │
                                                    8 tricks ──▶ [DEAL_SUMMARY]
                                                                      │
                                                     match not over ──▶ [CUT]
                                                     match over ──────▶ [MATCH_END]
                                                                            │
                                                                play again ──▶ [WAITING]
                                                                leave ────────▶ [HOME]
```

Each state maps to:
- A center stage component
- A Zone C component
- A set of active SignalR event handlers
- An API endpoint to fetch fresh state from

---

## Things not to build

- **Chat.** Out of scope for v1. Belote tables don't have text chat traditionally.
- **Game history / replay.** Nice to have for v2, not needed now.
- **Player profiles / avatars.** Name string is sufficient.
- **Sound.** Design the visuals first. Sound can be layered on top without layout changes.
- **Animations beyond the essentials.** Card movement (hand → trick area), trick sweep (center → team side), and score updates. Skip particle effects, 3D card flips, and decorative transitions.