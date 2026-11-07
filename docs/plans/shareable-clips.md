# Shareable trick clips — implementation plan

Status: phases 0–3 done (2026-11-06), phase 4 (growth polish) next. Owner: Haga.

## 1. Goal and scope

**Goal.** Let a player turn one or more tricks of a finished match into a short vertical
video they can post on WhatsApp status, Facebook, TikTok or Instagram, plus a public
landing page (`/c/{shortId}`) with a player, a download button and a "Play Giretra" call to
action. The success metric is new sign-ups coming from landing-page visits, not clips
created.

**In scope.** Deterministic replay of a deal (data + rendering), clip creation UI, public
landing page, headless rendering to MP4 with ffmpeg in a Docker container, storage,
retention, privacy controls.

**Out of scope for v1.** Clips of a whole match, live streaming, editing (trim, text),
user-uploaded audio, the Unity client (the contracts are designed so it can reuse the
replay payload later).

## 2. Facts from the codebase this plan relies on

| Fact | Where | Consequence |
|---|---|---|
| Every card play is persisted with position, card, trick number, order | `Giretra.Model/Entities/DealAction.cs`, `MatchPersistenceService` | A deal can be replayed exactly; no new persistence needed for the content |
| Every completed deal has its 32 plays; the only partial deals are the last deal of an abandoned match | prod data check (memory notes) | Clips come only from completed, non-abandoned matches, so every candidate deal has 32 plays |
| `action_type` rows written 2026-03-10..2026-08-16 store the int `6` instead of `play_card` | prod data | Use the EF converter, never raw SQL, in the replay builder |
| Starting hands are rebuilt from plays | `Giretra.Core/Solving/HandAnalyzer.cs` | Same trick: hand at trick *n* = cards the player plays in tricks ≥ *n* |
| Table UI is input-driven (`table-surface`, `trick-area`, `player-seat`, `hand-area`, `card`) | `features/table/components/**` | Reusable by a replay component without SignalR |
| Card entry uses a CSS keyframe `throwIn` (0.2 s) plus `transition` on borders | `trick-area.component.ts` | Must be neutralised or driven by time in render mode |
| Fonts come from Google Fonts at runtime (Inter, Urbanist) | `index.html` | Non-deterministic in a headless browser without network; self-host |
| Matches are persisted only at match end (completed or abandoned) | `MatchPersistenceService.PersistCompletedMatchAsync` / `PersistAbandonedMatchAsync` | Clips are possible only after the match ends |
| **The whole Angular app is behind Keycloak `login-required` in an `APP_INITIALIZER`** | `app.config.ts`, `auth.service.ts` | A public replay route needs the initializer to skip Keycloak for that path |
| `MapFallbackToFile("index.html")` is the last route | `Program.cs` | Landing page endpoints must be mapped before it |
| `mcr.microsoft.com/dotnet/aspnet:10.0` is **Ubuntu 24.04** (verified by running the image) | `Dockerfile` | `apt install chromium` is a snap stub there and does not work; use the Playwright image or `playwright install --with-deps` |
| `environment.prod.ts` uses relative API URLs | `src/environments/environment.prod.ts` | The render browser can load `http://web:8080/replay/...` over the Docker network |
| In-memory per-user throttle pattern exists | `Services/FeedbackThrottle.cs` | Copy it for clip creation |
| `Giretra.Contracts` is synced to the Unity client | `sync-unity-contracts.sh` | Put the replay DTO there |
| Microsoft.Playwright latest NuGet is 1.64.1; matching image `mcr.microsoft.com/playwright/dotnet:v1.64.1-noble` | NuGet lookup | Pin both to the same version |

## 3. Locked decisions

1. **One renderer, two consumers.** The replay is a pure function `stateAt(t)` rendered by
   Angular. The same component drives the interactive replay in the browser (rAF loop) and
   the video (headless browser calls `seek(t)` per frame). What people see on the web is
   exactly what the MP4 shows, and there is no second drawing code to maintain.
2. **Two containers, one source tree.** `web` keeps the slim aspnet image. `render` is built
   from the same repo with a second Dockerfile target based on the Playwright .NET image
   plus `ffmpeg`, started with `--render-worker`. A render spike cannot stall SignalR, and a
   Chromium leak restarts only the worker.
3. **Replay data is public only through a clip.** `GET /api/clips/{shortId}` is anonymous.
   `GET /api/matches/{id}/deals/{n}/replay` is for participants only (in-app preview).
   Match GUIDs never become public URLs.
4. **Videos are a cache.** Files live on a Docker volume; metadata lives in Postgres. A video
   can always be re-rendered from deal actions, so eviction is safe.
5. **Default perspective is the sharer's seat.** Their hand face-up, the other three
   face-down until played. "All hands" is an explicit option available to every sharer and
   stored on the clip. *(Confirmed 2026-11-05.)*
6. **Names.** Humans appear by display name unless they opt out (new setting, default
   on, shown as "Player" + seat when off). Bots are always named. Room names never appear.
   *(Confirmed 2026-11-05.)*
7. **Output v1.** 1080×1920 H.264 MP4, yuv420p, `+faststart`, 30 fps, CRF 23, with a PNG
   poster and a sound track (card and trick-win samples). Square and landscape come in
   phase 4. *(Sound confirmed 2026-11-05.)*
8. **Public pages are served by ASP.NET** on `play.giretra.com`: `/c/{shortId}` (HTML with
   Open Graph tags), `/c/{shortId}/video.mp4`, `/c/{shortId}/poster.png`. The interactive
   replay is the Angular route `/replay/{shortId}` with Keycloak skipped for that path.
   *(Domain confirmed 2026-11-05.)*
9. **Only completed matches are clippable.** Matches with `WasAbandoned` or no
   `CompletedAt` are excluded entirely, including their completed deals.
   *(Confirmed 2026-11-05.)*

## 4. Architecture

```
 player (logged in)                       anyone (public)
   │ POST /api/clips                        │ GET /c/{shortId}           GET /replay/{shortId}
   ▼                                        ▼                            ▼
 ┌──────────── web container ─────────────────────────────────────────────────────┐
 │ ClipsController  ClipPagesEndpoints  ReplayController   Angular SPA (static)   │
 │        │               │                                   ▲                   │
 │        ▼               ▼                                   │ GET /api/clips/{id} (anon)
 │   clips table ◄── /data/clips (volume: mp4 + png) ─────────┘                   │
 └────────┬────────────────────────▲──────────────────────────────────────────────┘
          │ status=Pending         │ writes files, status=Ready
          ▼                        │
 ┌──────────── render container (same image source, --render-worker) ───────────┐
 │ ClipRenderHostedService: claim job → Chromium (Playwright)                   │
 │   opens http://web:8080/replay/{id}?render=1&view=…&lang=…                   │
 │   for t in 0..duration step 1000/30: seek(t) → screenshot → ffmpeg stdin      │
 │   ffmpeg → /data/clips/{id}.mp4 ; frame 0 → {id}.png                          │
 └──────────────────────────────────────────────────────────────────────────────┘
```

## 5. Phases

Each phase ships something usable on its own. Risks and ideas are listed per step.

### Phase 0 — Spike (1 day). Prove determinism and throughput before product code

**Work**
- Throwaway Angular route `/replay-spike` that renders `trick-area` + four `player-seat`s
  from a hard-coded deal, driven by `?t=` and exposing `window.__giretra.seek(ms)`.
- Scratchpad .NET console using Microsoft.Playwright against the local Google Chrome
  (`/usr/bin/google-chrome` exists) and local `ffmpeg`: 600 frames at 540×960 with
  `deviceScaleFactor: 2`, piped to ffmpeg.
- Render the same clip twice and hash the frames.

**Exit criteria**
- ≤ 60 ms per frame on the dev machine (target ≤ 100 ms on the VPS).
- Two runs are byte-identical.
- The MP4 plays in WhatsApp (Android and iOS), Facebook, iOS Safari.

**Risks**
- Screenshot time scales with pixels; 1080×1920 may be slow. Fallback: capture at
  720×1280 and let ffmpeg upscale, or lower to 24 fps.
- `page.screenshot` after `seek` may capture before Angular repaints. The `seek` promise
  resolves after two `requestAnimationFrame`s and `document.fonts.ready`.

**Ideas**
- Keep the spike console in `tools/` later as the manual "render one clip locally"
  debugging tool; it is cheaper than the full worker when tuning visuals.

**Results (2026-11-05, dev machine, Angular dev server, Google Chrome headless)**

Setup: throwaway route `/replay-spike` (`features/replay-spike/`) renders a hard-coded
4-trick All Trumps deal as `stateAt(t)` with `trick-area`, `player-seat`, `card-fan` and
`game-mode-badge`; render mode kills every animation and transition with one
`ViewEncapsulation.None` rule and exposes `window.__giretra.seek(ms)` (sets `t` inside
`NgZone.run`, resolves after `document.fonts.ready` and two `requestAnimationFrame`s).
Renderer: `tools/clip-spike/` (Microsoft.Playwright 1.64.1, system Chrome, ffmpeg via
`image2pipe`). Clip: 20.1 s, 603 frames at 30 fps, 540×960 viewport.

| Frames | Scale | ms/frame (seek + screenshot) | Frame size | Verdict |
|---|---|---|---|---|
| PNG | 2x (1080×1920) | 209 (19 + 188) | 627 KiB | too slow |
| PNG | 1x (540×960) | 82 (15 + 65) | 205 KiB | soft output |
| JPEG q90 | 2x (1080×1920) | **84 (25 + 57)** | 144 KiB | **chosen** |
| JPEG q90 | 1x (540×960) | 50 (26 + 22) | 53 KiB | fallback for a slow VPS |

- Determinism: two full 30 fps runs with JPEG 2x → 603 frames each, **0 mismatched
  hashes** (byte-identical). The CSS kill rule plus time-driven state is sufficient; no
  Playwright `animations: disabled` needed.
- Output: H.264 Main 4.0, 1080×1920, 30 fps, `+faststart` (moov before mdat), 646 KiB
  for 20 s. JPEG input makes ffmpeg emit `yuvj420p` (full range); fixed with
  `-vf scale=in_range=full:out_range=limited,format=yuv420p` → `yuv420p`, `color_range=tv`.
- Visual check: cards, winner highlight (gold border, no transition artefacts), seat
  badges, score and caption all correct at arbitrary `t`; JPEG q90 shows no visible
  artefacts on the felt or card faces after H.264.
- Full 20 s clip renders in ~50 s per run on the dev machine. Expect 1.5–2× that on the
  VPS; still within the 3-minute job timeout.

Follow-ups carried into later phases:
- The seek step costs ~25 ms, mostly the two `requestAnimationFrame`s. Phase 3 can try a
  single rAF after a forced layout read, or `page.waitForFunction` on a frame counter.
- Dev server numbers are pessimistic (no production build); measure again in the
  `render` container with the production bundle.
- The spike has no card entry animation (cards appear instantly). Phase 1 adds
  `entryProgress`-driven opacity/scale so the video keeps the live table's feel.
- Not yet verified: playback on WhatsApp/Facebook/iOS Safari (needs a device; the file
  is at the standard Main-profile settings those platforms accept).

### Phase 1 — Replay data and the time-driven replay (4–5 days)

**1.1 Backend: `DealReplayBuilder`** (`Giretra.Web/Services/Replay/`)
- Loads `Deal` + `DealActions` + `MatchPlayers` (+ `User`/`Bot` for names), orders plays by
  `TrickNumber`, `ActionOrder`, replays them through Core (`HandState`/`TrickState`) to get
  the winner and cumulative card points of each trick, rebuilds the four starting hands
  from the plays, and computes the match score before the deal as the sum of the previous
  deals' match points.
- Rejects deals of abandoned or unfinished matches, and defensively any deal without 32
  plays, with a clear error.
- DTO `DealReplayResponse` in `Giretra.Contracts`: deal header (number, dealer, game mode,
  multiplier, announcer team, scores before/after, card points, sweep), negotiation
  actions, four players (seat, display name per privacy rule, isBot, avatar initials),
  tricks (`leader`, plays `[seat, card]`, winner, cumulative points), starting hands.

Risks: the mixed `action_type` encoding (covered by the EF converter; add a test that
loads both encodings). Core's trick evaluation is internal to `PlayValidator`
(`GetCurrentWinner` is private) so either expose a small public helper or drive a real
`HandState`; prefer the latter so the replay cannot drift from the rules.

Ideas: the same payload later feeds a post-game analysis board (loss per play from
`HandAnalyzer`) and the Unity client.

**1.2 Backend endpoint** `GET /api/matches/{matchId}/deals/{n}/replay`, `[Authorize]`,
participant check via `MatchPlayers`. Used by the in-app preview and the share dialog.

**1.3 Frontend: `ReplayTimeline`** (pure TypeScript, unit-tested with Vitest)
- Input: `DealReplayResponse`, trick range, perspective, timing constants
  (intro 1.5 s, card interval 0.7 s, trick hold 1.2 s, collect 0.4 s, outro 2 s).
- `duration` and `stateAt(t)` → `{ phase: intro|play|outro, trickOnTable, entryProgress per
  card (0..1), cardCounts, tricksWon, runningPoints, winnerHighlight, caption }`.
- Zero DOM, zero randomness, zero `Date.now()`.

**1.4 Frontend: `ReplayStageComponent`**
- Portrait layout: header (game mode badge, multiplier, score), the table (reuses
  `table-surface` via a small adapter that builds the `RoomResponse`-shaped seats it
  expects), the sharer's hand (`card-fan`), footer caption and branding.
- Render mode (`?render=1`): host class that sets `animation: none !important;
  transition: none !important` on everything; card entry comes from inline
  opacity/transform computed from `entryProgress`; no turn timer, no idle deadline, no
  avatars (initials only); exposes `window.__giretra = { seek(ms): Promise<void>, duration }`.
- Interactive mode: rAF loop, play/pause, scrubber, previous/next trick, speed.

Risks: `trick-area`'s `throwIn` keyframe and `.winning` transition (add an
`entryProgress`/`static` input rather than fighting CSS); the felt noise overlay must be
static (check it is not an animated or randomly seeded filter); `table-surface` expects
`RoomResponse` (adapter, do not fork the component).

Ideas: ship the interactive replay inside the table too ("review last deal" from the
deal summary); it is free once the component exists and is a retention feature.

**1.5 Frontend routing and auth**
- Route `/replay/:shortId` outside `AppLayout` (like `/table/:roomId`), lazy-loaded.
- `AuthService.init()` skips Keycloak when `location.pathname` starts with `/replay/`
  (anonymous mode). The auth interceptor must not throw without a token. Transloco
  language from `?lang=` with the usual fallback.
- Self-host Inter and Urbanist under `public/assets/fonts` (also better for users on slow
  connections and removes a Google dependency).

Risks: hidden assumptions that a user is always present (header, `getMe`). The replay
route only uses public endpoints and shared components, so keep it free of `AuthService`
and `GameStateService` imports.

**Acceptance.** `/replay/{id}?t=4200&render=1` shows a static frame; `/replay/{id}` plays
and scrubs; opening it in a private window never redirects to Keycloak.

**Results (2026-11-05)**

Shipped in the working tree (one PR's worth):

- Backend: `DealReplayResponse` (+ player/hand/trick DTOs) in `Giretra.Contracts`;
  `Services/Replay/DealReplayAssembler` drives a real `HandState` from the 32 recorded plays
  (winners, cumulative points and the last-trick bonus come from the engine, play order is
  validated); `DealReplayService` (participant check, completed and non-abandoned matches
  only, scores-before from earlier deals, bot/human names); `OfflineDealReplayService`
  serves a deterministic engine-played demo deal for any match id in `--offline` mode;
  `GET /api/matches/{matchId}/deals/{n}/replay` in `MatchesController` (200/403/404/422).
- Tests: `DealReplayServiceTests` with a hand-crafted All Trumps oracle deal whose winners
  and points are known without the engine; 8 tests, full suite 647 green.
- Frontend: `features/replay/replay-timeline.ts` (pure `frameAt(t)`, Vitest spec, 8 tests),
  `replay-stage.component.ts` (540×960 stage, render-mode CSS kill switch, intro/outro
  cards, optional all-hands view), `replay-page.component.ts` (route
  `/replay/match/:matchId/:dealNumber`, rAF player, scrubber, trick/deal navigation,
  `window.__giretra.seek` in `?render=1`). `trick-area` gained an optional `entryProgress`
  input (time-driven opacity/scale instead of the CSS throw-in). "Replay" link in the
  settings match history. Translations en/fr/mg.
- Fonts self-hosted via `@fontsource/inter` and `@fontsource/urbanist` (Google Fonts tags
  removed from `index.html`); 38 woff2 files bundled.
- `ng test` now exists (`@angular/build:unit-test` + Vitest 4 + jsdom); it did not before.
- Verified: offline demo deal renders through the local renderer at the same 84 ms/frame as
  the spike; two 30 fps runs of tricks 2–4 (501 frames) are byte-identical; captions in
  mg/fr; spike route and component deleted, `tools/clip-spike` now takes a page URL and can
  dump mismatched frames (`DUMP_MISMATCH=1`).
- Lesson: in render mode the page exposes `window.__giretra` only after the Transloco
  translations have loaded; before that guard, the first frames of a run could differ
  (captions appearing late), which showed up as 66 mismatched frames.

Decisions and deviations:

- The route is match-scoped and authenticated for now. The public, anonymous replay route
  keyed by a clip short id (and the Keycloak skip for that path) moves to phase 2, because
  without a clip there is nothing public to show and the participant endpoint needs a token.
- NSwag generation (`npm run api:generate`) does not work on this codebase (runtime pinned
  to Net80 and the minimal hosting model has no `CreateHostBuilder`); the generated files
  were last touched 2026-08-18 and the project already hand-types responses in
  `api.service.ts`, so the replay DTOs are hand-typed there too.
- Default viewer seat comes from the `view` query param (the history link passes the
  player's seat); the page does not try to infer the caller's seat from the payload.

### Phase 2 — Clips, landing page, worker infrastructure with poster only (3 days)

**2.1 Data**
- `Clip` entity: `Id`, `ShortId` (10 chars base62, unique), `MatchId`, `DealNumber`,
  `FromTrick`, `ToTrick`, `Perspective` (seat or AllHands), `Locale`, `Format`,
  `Status` (Pending, Rendering, Ready, Failed, Evicted, Removed), `CreatedByPlayerId`,
  `CreatedAt`, `RenderedAt`, `DurationMs`, `Attempts`, `Error`, `ViewCount`, `LastViewedAt`.
- Unique index on (`MatchId`, `DealNumber`, `FromTrick`, `ToTrick`, `Perspective`,
  `Locale`, `Format`) for dedupe; catch the unique violation and return the existing row.
- New `Player.AppearInSharedClips` (default true) exposed in settings.

**2.2 API** (`ClipsController`)
- `POST /api/clips` — participant of a completed, non-abandoned match, deal has 32 plays,
  range within 1..8, throttle 10/day per user (copy `FeedbackThrottle`). Returns the
  existing clip on dedupe.
- `GET /api/clips/{shortId}` — anonymous; status, URLs, and the `DealReplayResponse`
  (names already filtered by the privacy rule).
- `GET /api/clips/mine`, `DELETE /api/clips/{shortId}` (creator or moderator → Removed).

**2.3 Public pages** (minimal endpoints mapped before the SPA fallback)
- `GET /c/{shortId}` — server-rendered HTML in the clip's locale: `og:type video.other`,
  `og:video`, `og:video:secure_url`, `og:video:type video/mp4`, `og:video:width/height`,
  `og:image` (poster), `twitter:card player`, `<video controls playsinline poster>`,
  Download, Open interactive replay, Play Giretra (with UTM), share buttons (WhatsApp
  `wa.me` text, Facebook sharer, copy link, Web Share API with the file when
  `navigator.canShare({files})`).
- While Pending/Rendering: a "preparing" page with `noindex`, generic `og:image`, and
  JS polling. Removed → 410, unknown → 404.
- `GET /c/{shortId}/video.mp4` — `FileStream` with range processing,
  `Cache-Control: public, max-age=31536000, immutable`; `?download=1` adds
  `Content-Disposition` with a readable filename. Increments `ViewCount` (fire and forget).
- `GET /c/{shortId}/poster.png` — same caching.

Risks: Facebook and WhatsApp scrape once and cache; the in-app share UI must only
appear when the clip is Ready. The reverse proxy in front of `tetezana` must pass
`Range` requests and not buffer or cap 5 MB bodies (check proxy config). Facebook
needs `https` for `og:video:secure_url`.

**2.4 In-app UI**
- "Share this deal" from the deal summary, match end overlay and match history: dialog
  with a trick range selector (8 chips, live preview with the replay component),
  perspective, language; progress while rendering; link, Download and Share when Ready.
- "My clips" section in settings.

**2.5 Worker infrastructure (poster only in this phase)**
- Dockerfile target `render`: `FROM mcr.microsoft.com/playwright/dotnet:v1.64.1-noble`,
  `apt-get install ffmpeg fonts-noto-color-emoji`, copy the same publish output. Pin
  `Microsoft.Playwright` 1.64.1 in `Giretra.Web.csproj`.
- `docker-compose.yml`: service `render` (same build context, `target: render`,
  `command: ["dotnet","Giretra.Web.dll","--render-worker"]`,
  `Render__WebBaseUrl=http://web:8080`, `mem_limit: 1500m`, `cpus: 1.0`, `shm_size: 1g`,
  no ports); volume `clips:/data/clips` on both services.
- `Program.cs` role switch: the worker registers only `DbContext`, options, the
  `ClipRenderHostedService` and a `/health` endpoint. No hubs, no AI registry, no
  achievement sync, no migrations (the worker waits until the `clips` table exists).
- Job claim: `UPDATE clips SET status='Rendering', attempts=attempts+1 WHERE id = (SELECT id
  FROM clips WHERE status='Pending' ORDER BY created_at LIMIT 1 FOR UPDATE SKIP LOCKED)
  RETURNING ...`, polled every 2 s. In this phase the job renders frame 0 to the poster and
  marks the clip Ready, so the landing page and share flow are complete end to end.

Risks: Chromium sandbox in Docker (run as the image's `pwuser`, add `--no-sandbox` only
if needed); `--disable-dev-shm-usage` or `shm_size`; the worker must not run anything
that assumes a single instance (gate every hosted service behind the role flag).

Ideas: a `ClipRenderOptions.Enabled=false` default makes `--offline` dev unaffected; a
`giretra-manage render-clip <shortId>` command reuses the same service for debugging.

**Results (2026-11-05)**

Shipped on `feature/shareable-clips`:

- Data: `Clip` entity (+ `ClipStatus`), unique dedupe index on (match, deal, range, view,
  all-hands, locale), `(status, created_at)` index for the queue, `claimed_at` for stale
  claims; `Player.AppearInSharedClips` (default true). Migration `AddClips`.
- API: `POST /api/clips` (201 new / 200 existing / 400 / 403 / 404 / 422 / 429),
  `GET /api/clips/mine`, `GET /api/clips/{shortId}` (anonymous, replay payload with names
  of opted-out humans replaced by a localised placeholder and `NameHidden=true`),
  `DELETE /api/clips/{shortId}` (creator or moderator, status Removed, files deleted),
  `PUT /api/settings/profile/clip-name-visibility`. Throttle 10 new clips per rolling 24 h
  (dedupe hits are free). Short ids: 10 chars base62.
- Public pages (`Endpoints/ClipPagesEndpoints`, mapped before the SPA fallback):
  `/c/{shortId}` server-rendered HTML with og:title/description/url/locale/image and, when
  a video exists, og:type video.other + og:video(:secure_url/:type/:width/:height) +
  twitter player; pending clips are `noindex` with a 6 s refresh and the generic image;
  removed → 410. `/c/{shortId}/poster.png` and `/c/{shortId}/video.mp4` stream from the
  clips volume with range support and a one-year immutable cache; `?download=1` sets a
  filename. All names HTML-encoded (tested with a `<script>` display name).
- Render worker: `--render-worker` runs a minimal host (DB + `ClipRenderHostedService`
  + `/health`); `PostgresClipJobQueue` claims with `UPDATE … WHERE id = (SELECT … FOR UPDATE
  SKIP LOCKED) RETURNING id`, re-queues stale claims, fails after 2 attempts.
  `ClipRenderer` (Microsoft.Playwright 1.64.1) loads `/replay/clip/{id}?render=1`, seeks to
  `window.__giretra.posterTime` (first trick of the range complete, winner highlighted) and
  writes the PNG poster; browser recycled every 20 jobs or on failure. In `--offline` mode
  `Render__Enabled=true` runs the same loop in-process against in-memory clips.
- Docker: `render` target in the Dockerfile (aspnet:10.0 + Playwright's own
  `cli.js install --with-deps chromium` + ffmpeg, `--no-sandbox` by default); compose
  service `render` with `shm_size 1g`, `mem_limit 1500m`, `cpus 1.0`, shared `clips` volume,
  `Render__WebBaseUrl=http://web:8080`; `web` gets `Clips__RootPath` and
  `Clips__PublicBaseUrl`.
- Frontend: anonymous mode in `AuthService` for `/replay/clip/*` (no Keycloak, interceptor
  sends no header); route `/replay/clip/:shortId` reuses the replay page with the clip's
  range/view/hands/locale; Share button on the participant replay opens
  `clip-share-dialog` (trick chips, all-hands, language → create → polls until Ready →
  copy link / open page / download when a video exists); settings: "Clips" section (list,
  copy, open, delete) and a "Name in shared clips" toggle. Translations en/fr/mg.
- Tests: `ClipServiceTests` (create, dedupe, validation, throttle with `FakeTimeProvider`,
  privacy masking, delete permissions, ready URLs) and landing page tests (OG video tags,
  escaping, pending/removed states): 11 tests; full suite 658 + 8 Vitest green.
- Verified locally end to end in offline mode: create via API → in-process Chromium
  rendered the poster in 1.5 s → landing page served the OG tags and the immutable poster →
  share dialog and public replay checked in the browser.

Deviations and notes:

- NSwag still unusable; the clip DTOs are hand-typed in `api.service.ts`.
- The render image is based on aspnet:10.0 rather than the Playwright .NET image, to keep
  the SDK out of production and avoid a .NET version mismatch; the browser is installed by
  Playwright's bundled CLI from the publish output. Not yet built on the VPS.
- Migration not applied anywhere yet; `web` uses `EnsureCreated` at startup, so the
  migration must be applied explicitly (`dotnet ef database update`) on production.
- The `web` container must be able to reach itself as `http://web:8080` from the render
  container; the landing page's replay link uses `Clips__PublicBaseUrl`.

### Phase 3 — Video rendering (3 days)

**3.1 Frame loop** in `ClipRenderer`
- One browser per worker process, a new context and page per job, viewport 540×960 at
  `deviceScaleFactor 2` (or 1080×1920 at 1 if the spike shows it is fast enough).
- `goto` the replay URL, wait for `window.__giretra`, `seek(0)`, read `duration`.
- For each frame: `seek(t)`, `ScreenshotAsync(Png)`, write to ffmpeg stdin. Read ffmpeg
  stderr asynchronously (deadlock otherwise). Timeouts: 3 min per job, kill both
  processes; 2 attempts then Failed with the error stored.
- ffmpeg:
  `ffmpeg -y -f image2pipe -framerate 30 -i pipe:0 [-i audio.wav] -c:v libx264 -preset veryfast
  -crf 23 -profile:v main -level 4.0 -pix_fmt yuv420p -movflags +faststart -r 30 -shortest out.mp4`
- Audio (on; a per-clip flag can disable it later): mix short card and trick-win samples at the
  timeline's event times into a WAV in C# (plain PCM addition), then mux. Simpler than an
  ffmpeg filter graph and fully deterministic.
- Write to `{id}.mp4.tmp` then rename; set Ready only after both files exist.

**3.2 Browser hygiene.** Recycle the browser every 20 jobs or after any failure; log ms per
frame, total time, output size per job.

**3.3 Retention.** Nightly: clips not viewed for 180 days → delete files, status Evicted.
A request for an Evicted clip re-queues it (Pending) and shows the preparing page.

**3.4 Admin.** List failed clips with error text, retry, remove (takedown).

Risks: any nondeterminism shows up as flicker between frames (fonts, avatars, timers,
`:hover` under the virtual mouse, animated gradients). Add an integration test in
`tests/Giretra.Web.Tests` that renders five frames twice through the real pipeline and
asserts identical bytes (skipped when Chromium is absent). CPU contention is contained by
the container limits. iOS needs `playsinline` and `-profile:v main`.

Ideas: JPEG screenshots at quality 90 are 2–3× faster if PNG is too slow; H.264 is lossy
anyway. Keep the frame count constant per second; do not try to skip static frames.

**Results (2026-11-06)**

Shipped on `feature/shareable-clips`:

- Frame loop in `ClipRenderer`: after the poster, `seek(i/fps)` + JPEG q90 screenshot per
  frame at 540×960 @2x, piped to `FfmpegEncoder` (image2pipe → libx264 veryfast, CRF 23,
  Main 4.0, `scale=in_range=full:out_range=limited,format=yuv420p`, `+faststart`, `-f mp4`
  because the output is `{id}.mp4.tmp`, renamed when ffmpeg exits 0). stderr drained
  asynchronously, process killed on dispose/timeout. `Render__Video=false` keeps poster only.
- Sound: the replay page exposes `window.__giretra.events` from
  `ReplayTimeline.soundEvents()` (card sound per play, the trick's winning card gets
  `card_played_master`, `card_folding_started` when the trick is collected). `ClipSoundBank`
  fetches the app's own `/assets/sounds/*.mp3` from the web origin, decodes once with ffmpeg
  to 44.1 kHz stereo PCM; `ClipAudio.Mix` adds them at the event times (clipped, gain 0.7)
  and writes a WAV that ffmpeg muxes as AAC 96k with `-shortest`. A missing sample never
  fails a job (silent instead).
- Measured on the dev machine (Angular dev server, Google Chrome 156): 84 ms/frame
  (seek 26, screenshot 56), 3-trick clip = 501 frames, 16.7 s, 826 KiB, 42 s wall; 1-trick
  clip = 237 frames, 20 s wall. Two renders of the same clip are byte-identical (mp4 and
  poster). ffprobe: h264 Main 1080×1920 yuv420p tv range 30 fps + aac LC 44.1 kHz stereo,
  `ftyp` first (faststart). Default job timeout raised to 300 s for 8-trick clips on a slow core.
- Retention: `ClipRetentionHostedService` (render worker only) runs `PostgresClipRetention`
  every 6 h: Ready clips with `coalesce(last_viewed_at, rendered_at, created_at)` older than
  180 days lose their files and become Evicted. Any public read of an Evicted clip re-queues
  it (Pending, attempts reset) so the landing page shows "preparing" and the worker
  re-renders it. Failed clips get a landing page that explains and does not auto-refresh.
- Admin: `GET /api/admin/clips?status=&page=` (title, creator, status, attempts, error,
  claimed_at), `POST /api/admin/clips/{id}/retry`, `DELETE /api/admin/clips/{id}`; Angular
  page `/admin/clips` (filter chips, Failed by default; retry / take down / open), linked from
  the admin overview and the sidebar. Share dialog shows the finished video inline.
- Tests: `ClipAudioTests` (mix placement, clipping, gain, WAV header, ffmpeg args, event
  parsing), `ClipRetentionTests`, and in `ClipServiceTests` evicted re-queue, admin list /
  filter / retry, failed landing page; `soundEvents` in Vitest. Totals 668 .NET + 9 Vitest.
- Verified end to end in offline mode: API-created clip rendered with sound, landing page
  served og:video + `<video>` + range requests (206, immutable), admin list/filter/retry over
  HTTP, admin page and share dialog with the playing video in the browser.

Deviations and notes:

- The planned xUnit "render five frames twice" integration test is not there: it needs the
  Angular app served, which the test host cannot do. `tools/clip-spike` remains the
  determinism harness (run it against the dev server after any change to the stage).
- Sounds come over HTTP from the web container rather than from disk so the Angular bundle
  stays the single source; in the render container that is `http://web:8080/assets/sounds/`.
- `card_played_master.mp3` peaks at 0 dBFS; with gain 0.8 the AAC output touched 0 dB, hence 0.7.
- Not device-tested yet (iOS Safari, WhatsApp, Facebook scraper); the file is at the
  standard settings those platforms accept.

### Phase 4 — Growth polish (2–4 days)

- Square (1080×1080) and landscape (1280×720) formats; landscape is what X and Telegram
  embed inline from `og:video`.
- Intro title card ("Haga & Lova vs Bot A & Bot B — All Trumps, doubled") and outro end
  card with the URL and a QR code, translated. Subtle watermark during play.
- **Moment suggestions.** At match end run `HandAnalyzer` on the deal: the trick with the
  biggest swing, a zero-loss play where every alternative loses a lot, a sweep, or an
  achievement earned → "Share this moment" prompt pre-filled with that range. This is the
  conversion driver: people share when the app tells them they did something notable.
- Web Share API with the file on mobile (direct to WhatsApp status and Instagram), and
  pre-filled `wa.me` text in Malagasy and French.
- Analytics: UTM on the landing CTA; count landing views and sign-ups within 7 days.
- Bot-vs-bot clips for bot authors (the Manage CLI already runs matches).
- The interactive replay grows into an analysis board with solver hints.

## 6. Risk register

| # | Risk | Severity | Mitigation | Phase |
|---|---|---|---|---|
| 1 | App is login-walled; public replay redirects to Keycloak | High | Path-aware `AuthService.init()`; replay route uses only anonymous endpoints | 1 |
| 2 | Ubuntu-based aspnet image has no working `chromium` apt package | High | Playwright .NET image for the `render` target; pin versions | 2 |
| 3 | Non-deterministic frames (CSS animations, fonts, avatars, timers) | High | Render mode kills animations; time-driven entry; self-hosted fonts; initials; determinism test | 1, 3 |
| 4 | Render load degrades gameplay latency | High | Separate container, `cpus`/`mem_limit`, concurrency 1, job timeout | 2 |
| 5 | Scrapers cache Open Graph tags before the video is ready | Medium | Share UI only when Ready; preparing page is `noindex` with generic image | 2 |
| 6 | Opponents' names or hands in public videos | Medium | Opt-out setting, sharer's-seat default, takedown, no room names | 2 |
| 7 | Volume growth | Medium | Videos are a cache; eviction + re-render; ~3 MB per clip | 3 |
| 8 | Two containers from one image run singleton jobs twice | Medium | Role flag gates every hosted service; migrations only in `web` | 2 |
| 9 | ffmpeg or Chromium hangs | Medium | Per-job timeout, kill process tree, attempts ≤ 2 | 3 |
| 10 | Reverse proxy buffers or rejects `Range` | Medium | Verify proxy config; test seeking on iOS | 2 |
| 11 | Dedupe race on concurrent create | Low | Unique index + catch | 2 |
| 12 | Abuse (spam clips, offensive names) | Low | Throttle, participant-only create, moderator remove | 2 |
| 13 | Hidden rule details in the replay (last-trick bonus, sweep points) | Low | Drive a real `HandState` instead of recomputing | 1 |

## 7. Effort and sequencing

| Phase | Days | Shippable result |
|---|---|---|
| 0 Spike | 1 | Go/no-go numbers |
| 1 Replay | 4–5 | Interactive replay of any deal (private, participants) |
| 2 Clips + landing + worker (poster) | 3 | Public shareable link with poster image |
| 3 Video | 3 | MP4 download and native sharing |
| 4 Polish | 2–4 | Formats, suggestions, end card, analytics |

Total roughly 13–16 working days. Phases 1 and 2 can be reviewed as separate PRs; phase
2 is the first point where users see the feature.

## 8. Decisions taken on the open questions (2026-11-05)

1. Names are visible. Humans by display name with an opt-out setting; bots always.
2. The "all hands" perspective is an option available to every sharer, not the default.
3. Links live on `play.giretra.com/c/{shortId}`.
4. Sound is on in the MP4.
5. Abandoned matches are never clippable, even their completed deals.
