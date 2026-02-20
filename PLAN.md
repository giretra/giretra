# Plan: Fix Navigation, Reconnection & Network Resilience

## Problem Summary

Three related UX gaps where the client doesn't handle session/connection lifecycle properly:

1. **#5-6 Navigation destroys game session** — Browser back, tab close, or manual URL change abandons the game with no warning and no way to rejoin.
2. **#37-38 No reconnection feedback** — SignalR has `withAutomaticReconnect()` but no `onreconnecting`/`onreconnected`/`onclose` handlers, so the user sees a frozen game with no indication of what's happening.
3. **#25 Card play during network issues** — If the network drops mid-play, the user sees a generic error toast, the card stays in hand, and `refreshState()` also fails silently.

---

## A. Connection Status Infrastructure (GameHubService + UI)

**Files:** `game-hub.service.ts`, `game-state.service.ts`, `table.component.ts`

### A1. Expose connection state signal from GameHubService

In `game-hub.service.ts`:
- Add a `connectionStatus` signal: `'connected' | 'reconnecting' | 'disconnected'`
- Register three handlers on the HubConnection in `connect()`:
  - `onreconnecting()` → set `'reconnecting'`
  - `onreconnected()` → set `'connected'`, emit a new `reconnected$` Subject
  - `onclose()` → set `'disconnected'`
- Set `'connected'` after successful `start()`

### A2. Auto-refresh state on reconnect

In `game-state.service.ts`:
- Subscribe to `hub.reconnected$`
- On reconnect: re-invoke `hub.joinRoom(roomId, clientId)` (re-joins SignalR groups) then call `refreshState()`
- This ensures the client gets fresh state after any connection blip

### A3. Connection status banner in table UI

In `table.component.ts` template:
- Add a thin banner at top of `.table-container` that shows:
  - **Reconnecting**: amber bar with "Reconnecting..." + spinner
  - **Disconnected**: red bar with "Connection lost" + manual "Retry" button
- Bar is hidden when `'connected'`
- Read `hub.connectionStatus` signal directly (it's a root-level service)

---

## B. Session Preservation on Navigation (#5-6)

**Files:** `table.component.ts`, `app.routes.ts`, `home.component.ts`

### B1. CanDeactivate guard on table route

In `app.routes.ts`:
- Add a `canDeactivate` functional guard for the table route
- Check if `GameStateService.gameId()` exists and `phase()` is not `'waiting'` / `'matchEnd'`
- If game in progress: show `confirm('Leaving during a match may result in a rating loss. Are you sure?')`
- If confirmed: call `gameState.leaveRoom()` + `session.leaveRoom()` before allowing navigation
- If cancelled: block navigation

This replaces the ad-hoc `confirm()` in `onLeaveTable()` with a guard that also catches browser back button and programmatic navigation.

### B2. beforeunload handler for tab close / refresh

In `table.component.ts`:
- `ngOnInit`: register `window.addEventListener('beforeunload', handler)`
- `ngOnDestroy`: remove it
- Handler: if game in progress, call `event.preventDefault()` (shows browser's native "Leave site?" dialog)
- Also use `navigator.sendBeacon()` to POST to `/api/rooms/{roomId}/leave` with `{clientId}` body — this works even during page unload (unlike fetch/XHR)
- This gives the server immediate notice rather than waiting for the 20s disconnect grace period

### B3. Resume game banner on home page

In `home.component.ts`:
- On init, check `ClientSessionService.roomId()` — if set, user has an active session
- Validate it's still live: call `api.getRoom(roomId)` — if it returns and status is `Playing`, show a "You have a game in progress" banner with a "Resume" button
- "Resume" navigates to `/table/:roomId`
- If getRoom returns 404 or room is `Completed`/`Waiting`, clear the stale session (`session.leaveRoom()`)

---

## C. Card Play Network Resilience (#25)

**Files:** `table.component.ts`, `game-state.service.ts`, `hand-area.component.ts`, `api.service.ts`

### C1. Submitting state to prevent double-play

In `game-state.service.ts`:
- Add `_isSubmittingAction` signal (boolean), exposed as `isSubmittingAction` readonly
- Set `true` before any action API call (playCard, negotiate, cut, continue)
- Set `false` on success or error

In `table.component.ts`:
- Wrap each action method (onPlayCard, onSubmitNegotiation, onSubmitCut) with an early return if `isSubmittingAction()` is already true

In `hand-area.component.ts`:
- Accept a new `disabled` input
- When `disabled` is true, set `interactive` to false on CardFan regardless of turn state

### C2. Distinguish network errors from validation errors

In `api.service.ts` `handleError`:
- Detect network errors: `error.status === 0` or `error instanceof ProgressEvent` (no response from server)
- For network errors: show "Connection issue — your action may not have been sent" (different from generic toast)
- For server validation errors (400): show the specific `detail` message (e.g., "Card not valid to play")
- For server errors (500): show "Server error — please try again"

### C3. Refresh state after failed action + reconnect

Already handled by A2 — when connection is restored, `refreshState()` runs automatically. This covers the case where:
1. User plays card → network drops → error shown
2. Connection restored → `onreconnected` fires → `refreshState()` fetches true state
3. If card was accepted by server before disconnect: hand now shows correct state
4. If card was not received: hand still has the card, user can replay

---

## File Change Summary

| File | Changes |
|------|---------|
| `game-hub.service.ts` | Add `connectionStatus` signal, `reconnected$` subject, register `onreconnecting`/`onreconnected`/`onclose` handlers |
| `game-state.service.ts` | Add `isSubmittingAction` signal, subscribe to `hub.reconnected$` for auto-rejoin + refresh |
| `table.component.ts` | Add `beforeunload` handler, connection status banner in template, guard action methods with `isSubmittingAction` |
| `hand-area.component.ts` | Add `disabled` input to block interaction during submission |
| `app.routes.ts` | Add `canDeactivate` guard on table route |
| `home.component.ts` | Add resume-game banner when active session exists |
| `api.service.ts` | Differentiate network vs server errors in `handleError` |

## Order of Implementation

1. **A1-A2** — Connection state + auto-rejoin (foundation for everything else)
2. **A3** — Connection banner UI (immediate user-visible improvement)
3. **C1-C2** — Submitting state + error differentiation (prevents confusing double-plays)
4. **B1-B2** — Navigation guards + beforeunload (prevents accidental abandonment)
5. **B3** — Resume game banner (recovery path)
