# Data Design Plan - Giretra

> PostgreSQL relational database schema for the Giretra Malagasy Belote platform.

---

## Design Decisions

| Decision | Choice |
|----------|--------|
| Database | PostgreSQL |
| Auth provider | Keycloak (external) |
| Active games | In-memory only, persisted to DB on completion |
| Replay depth | Full (every action: cuts, negotiations, card plays) |
| Elo model | Single global Elo per player (human + bot) |
| Elo history | Tracked per match for progression graphs |
| Social | Friend lists + block lists (no chat storage) |
| Roles | Admin, Normal (extensible via enum) |
| Soft deletes | On users only (Keycloak is source of truth) |

---

## Entity Relationship Overview

```
users ──┐
        ├──> players ──> match_players ──> matches
bots ───┘       │                            │
                │                            ├──> deals ──> deal_actions
                ▼
           elo_history

users ──> friendships (self-join)
users ──> blocks (self-join)
```

---

## Tables

### 1. `users`

Synced from Keycloak. This is your local projection of the Keycloak user — not the source of truth for auth, but the source of truth for app-specific data.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `UUID` | PK, DEFAULT gen_random_uuid() | Internal app ID |
| `keycloak_id` | `UUID` | UNIQUE, NOT NULL | Keycloak `sub` claim |
| `username` | `VARCHAR(50)` | UNIQUE, NOT NULL | From Keycloak, login name |
| `display_name` | `VARCHAR(100)` | NOT NULL | Shown in-game |
| `email` | `VARCHAR(255)` | UNIQUE, NULL | From Keycloak, nullable if not exposed |
| `avatar_url` | `TEXT` | NULL | Profile picture URL |
| `role` | `VARCHAR(20)` | NOT NULL, DEFAULT 'normal' | `admin`, `normal` (extensible) |
| `is_banned` | `BOOLEAN` | NOT NULL, DEFAULT FALSE | Soft-ban without Keycloak disable |
| `ban_reason` | `TEXT` | NULL | Admin note |
| `last_login_at` | `TIMESTAMPTZ` | NULL | Updated on each login |
| `created_at` | `TIMESTAMPTZ` | NOT NULL, DEFAULT now() | First login / sync |
| `updated_at` | `TIMESTAMPTZ` | NOT NULL, DEFAULT now() | Trigger-maintained |

**Indexes:**
- `ix_users_keycloak_id` ON (`keycloak_id`) — login lookup
- `ix_users_username` ON (`username`) — search/display

**Keycloak fields worth syncing:**
- `sub` → `keycloak_id`
- `preferred_username` → `username`
- `email` → `email`
- `name` / `given_name` → `display_name`
- `realm_access.roles` → `role` (map Keycloak realm roles to your enum)

**Fields you do NOT need to store** (stay in Keycloak):
- Password hash, MFA config, email verified status, session tokens

---

### 2. `bots`

Registered bot implementations. One row per bot type (not per game instance).

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `UUID` | PK, DEFAULT gen_random_uuid() | |
| `agent_type` | `VARCHAR(100)` | UNIQUE, NOT NULL | Maps to `IPlayerAgent` factory name (e.g. `DeterministicBot`) |
| `display_name` | `VARCHAR(100)` | NOT NULL | Shown in-game (e.g. "Rabe the Cautious") |
| `description` | `TEXT` | NULL | Flavor text or strategy description |
| `difficulty` | `SMALLINT` | NOT NULL, DEFAULT 1 | 1=easy, 2=medium, 3=hard (for UI filtering) |
| `is_active` | `BOOLEAN` | NOT NULL, DEFAULT TRUE | Can be selected for new games |
| `created_at` | `TIMESTAMPTZ` | NOT NULL, DEFAULT now() | |

---

### 3. `players`

**Unified player identity.** Both humans and bots participate in matches through this table. This avoids polymorphic FKs everywhere else.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `UUID` | PK, DEFAULT gen_random_uuid() | Referenced by match_players, elo_history |
| `player_type` | `VARCHAR(10)` | NOT NULL | `human` or `bot` |
| `user_id` | `UUID` | FK → users(id), NULL | Set for humans |
| `bot_id` | `UUID` | FK → bots(id), NULL | Set for bots |
| `elo_rating` | `INTEGER` | NOT NULL, DEFAULT 1000 | Current Elo |
| `elo_is_public` | `BOOLEAN` | NOT NULL, DEFAULT TRUE | FALSE for bots (shadow ranking) |
| `games_played` | `INTEGER` | NOT NULL, DEFAULT 0 | Denormalized counter |
| `games_won` | `INTEGER` | NOT NULL, DEFAULT 0 | Denormalized counter |
| `win_streak` | `INTEGER` | NOT NULL, DEFAULT 0 | Current consecutive wins |
| `best_win_streak` | `INTEGER` | NOT NULL, DEFAULT 0 | All-time record |
| `created_at` | `TIMESTAMPTZ` | NOT NULL, DEFAULT now() | |
| `updated_at` | `TIMESTAMPTZ` | NOT NULL, DEFAULT now() | |

**Constraints:**
- `chk_player_type_ref` — CHECK: (`player_type = 'human' AND user_id IS NOT NULL AND bot_id IS NULL`) OR (`player_type = 'bot' AND bot_id IS NOT NULL AND user_id IS NULL`)
- `uq_players_user_id` — UNIQUE(`user_id`) WHERE `user_id IS NOT NULL` (one player row per user)
- `uq_players_bot_id` — UNIQUE(`bot_id`) WHERE `bot_id IS NOT NULL` (one player row per bot type)

**Indexes:**
- `ix_players_user_id` ON (`user_id`) WHERE `user_id IS NOT NULL`
- `ix_players_bot_id` ON (`bot_id`) WHERE `bot_id IS NOT NULL`
- `ix_players_elo_rating` ON (`elo_rating` DESC) WHERE `elo_is_public = TRUE` — leaderboard

---

### 4. `matches`

A completed match (first to 150+). Written once when the match ends.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `UUID` | PK, DEFAULT gen_random_uuid() | |
| `room_name` | `VARCHAR(100)` | NOT NULL | Denormalized — rooms are ephemeral |
| `target_score` | `INTEGER` | NOT NULL, DEFAULT 150 | Can increase if both teams exceed |
| `team1_final_score` | `INTEGER` | NOT NULL | Match points at end |
| `team2_final_score` | `INTEGER` | NOT NULL | |
| `winner_team` | `SMALLINT` | NULL | 0=Team1, 1=Team2, NULL=abandoned |
| `total_deals` | `INTEGER` | NOT NULL | Number of deals played |
| `is_ranked` | `BOOLEAN` | NOT NULL, DEFAULT TRUE | Affects Elo calculation |
| `was_abandoned` | `BOOLEAN` | NOT NULL, DEFAULT FALSE | Match ended early (disconnect, forfeit) |
| `started_at` | `TIMESTAMPTZ` | NOT NULL | |
| `completed_at` | `TIMESTAMPTZ` | NULL | NULL if abandoned mid-write |
| `duration_seconds` | `INTEGER` | NULL | Computed: completed_at - started_at |
| `created_at` | `TIMESTAMPTZ` | NOT NULL, DEFAULT now() | Row insert time |

**Indexes:**
- `ix_matches_completed_at` ON (`completed_at` DESC) — recent games
- `ix_matches_is_ranked` ON (`is_ranked`) WHERE `is_ranked = TRUE` — ranked game queries

---

### 5. `match_players`

Links players to matches with their position and team.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `UUID` | PK, DEFAULT gen_random_uuid() | |
| `match_id` | `UUID` | FK → matches(id), NOT NULL | |
| `player_id` | `UUID` | FK → players(id), NOT NULL | |
| `position` | `SMALLINT` | NOT NULL | 0=Bottom, 1=Left, 2=Top, 3=Right |
| `team` | `SMALLINT` | NOT NULL | 0=Team1, 1=Team2 |
| `is_winner` | `BOOLEAN` | NOT NULL, DEFAULT FALSE | |
| `elo_before` | `INTEGER` | NULL | Snapshot for display |
| `elo_after` | `INTEGER` | NULL | Snapshot for display |
| `elo_change` | `INTEGER` | NULL | Convenience: after - before |

**Constraints:**
- `uq_match_player_position` — UNIQUE(`match_id`, `position`) — one player per seat
- `uq_match_player_unique` — UNIQUE(`match_id`, `player_id`) — no duplicates

**Indexes:**
- `ix_match_players_player_id` ON (`player_id`) — "my game history" queries
- `ix_match_players_match_id` ON (`match_id`)

---

### 6. `deals`

Each deal (hand) within a match. A match typically has 5-20+ deals.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `UUID` | PK, DEFAULT gen_random_uuid() | |
| `match_id` | `UUID` | FK → matches(id), NOT NULL | |
| `deal_number` | `SMALLINT` | NOT NULL | 1-based within match |
| `dealer_position` | `SMALLINT` | NOT NULL | 0-3 |
| `game_mode` | `SMALLINT` | NULL | 0-5 (NULL if negotiation failed/abandoned) |
| `announcer_team` | `SMALLINT` | NULL | 0=Team1, 1=Team2 |
| `multiplier` | `SMALLINT` | NOT NULL, DEFAULT 1 | 1=Normal, 2=Doubled, 4=Redoubled |
| `team1_card_points` | `INTEGER` | NULL | Including last trick bonus |
| `team2_card_points` | `INTEGER` | NULL | |
| `team1_match_points` | `INTEGER` | NULL | After multiplier |
| `team2_match_points` | `INTEGER` | NULL | |
| `was_sweep` | `BOOLEAN` | NOT NULL, DEFAULT FALSE | All 8 tricks by one team |
| `sweeping_team` | `SMALLINT` | NULL | |
| `is_instant_win` | `BOOLEAN` | NOT NULL, DEFAULT FALSE | Colour sweep |
| `announcer_won` | `BOOLEAN` | NULL | |
| `started_at` | `TIMESTAMPTZ` | NOT NULL | |
| `completed_at` | `TIMESTAMPTZ` | NULL | |

**Constraints:**
- `uq_deal_number` — UNIQUE(`match_id`, `deal_number`)

**Indexes:**
- `ix_deals_match_id` ON (`match_id`)

---

### 7. `deal_actions`

Full replay log. Every action in chronological order within a deal.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `UUID` | PK, DEFAULT gen_random_uuid() | |
| `deal_id` | `UUID` | FK → deals(id), NOT NULL | |
| `action_order` | `SMALLINT` | NOT NULL | Sequential within deal (1-based) |
| `action_type` | `VARCHAR(20)` | NOT NULL | See enum below |
| `player_position` | `SMALLINT` | NOT NULL | 0-3, who performed action |
| `card_rank` | `SMALLINT` | NULL | 7-14 (for `play_card`) |
| `card_suit` | `SMALLINT` | NULL | 0-3 (for `play_card`) |
| `game_mode` | `SMALLINT` | NULL | 0-5 (for `announce`, `double`, `redouble`) |
| `cut_position` | `SMALLINT` | NULL | 6-26 (for `cut`) |
| `cut_from_top` | `BOOLEAN` | NULL | (for `cut`) |
| `trick_number` | `SMALLINT` | NULL | 1-8 (for `play_card`) |

**`action_type` values:**
- `cut` — deck cut
- `announce` — negotiation announcement
- `accept` — negotiation accept
- `double` — negotiation double
- `redouble` — negotiation redouble
- `play_card` — card played in trick

**Constraints:**
- `uq_deal_action_order` — UNIQUE(`deal_id`, `action_order`)

**Indexes:**
- `ix_deal_actions_deal_id` ON (`deal_id`, `action_order`) — replay queries

**Note:** No `created_at` here — `action_order` is sufficient for replay ordering and this table will be high-volume. If you need timing for analytics (average think time per action), add `performed_at TIMESTAMPTZ NULL`.

---

### 8. `rooms`

Room history. Written when a room is closed/completed. Active rooms live in memory only.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `UUID` | PK | Same UUID as the in-memory room |
| `name` | `VARCHAR(100)` | NOT NULL | Room display name |
| `creator_player_id` | `UUID` | FK → players(id), NOT NULL | Who created it |
| `match_id` | `UUID` | FK → matches(id), NULL | NULL if room closed without playing |
| `turn_timer_seconds` | `INTEGER` | NOT NULL | 10-300 |
| `player_count` | `SMALLINT` | NOT NULL | Humans who joined (1-4) |
| `bot_count` | `SMALLINT` | NOT NULL | Bot seats (0-3) |
| `watcher_count` | `SMALLINT` | NOT NULL, DEFAULT 0 | Peak watchers |
| `created_at` | `TIMESTAMPTZ` | NOT NULL | |
| `closed_at` | `TIMESTAMPTZ` | NOT NULL | |

**Indexes:**
- `ix_rooms_creator` ON (`creator_player_id`)
- `ix_rooms_match_id` ON (`match_id`) WHERE `match_id IS NOT NULL`

---

### 9. `elo_history`

One row per player per completed ranked match. Powers progression graphs.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `UUID` | PK, DEFAULT gen_random_uuid() | |
| `player_id` | `UUID` | FK → players(id), NOT NULL | |
| `match_id` | `UUID` | FK → matches(id), NOT NULL | |
| `elo_before` | `INTEGER` | NOT NULL | |
| `elo_after` | `INTEGER` | NOT NULL | |
| `elo_change` | `INTEGER` | NOT NULL | Positive or negative |
| `recorded_at` | `TIMESTAMPTZ` | NOT NULL, DEFAULT now() | |

**Constraints:**
- `uq_elo_history_entry` — UNIQUE(`player_id`, `match_id`)

**Indexes:**
- `ix_elo_history_player_time` ON (`player_id`, `recorded_at` DESC) — progression graph
- `ix_elo_history_match` ON (`match_id`)

---

### 10. `friendships`

Bidirectional friend requests between real users.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `UUID` | PK, DEFAULT gen_random_uuid() | |
| `requester_id` | `UUID` | FK → users(id), NOT NULL | Who sent request |
| `addressee_id` | `UUID` | FK → users(id), NOT NULL | Who received request |
| `status` | `VARCHAR(10)` | NOT NULL, DEFAULT 'pending' | `pending`, `accepted`, `declined` |
| `created_at` | `TIMESTAMPTZ` | NOT NULL, DEFAULT now() | |
| `updated_at` | `TIMESTAMPTZ` | NOT NULL, DEFAULT now() | |

**Constraints:**
- `chk_no_self_friend` — CHECK(`requester_id != addressee_id`)
- `uq_friendship_pair` — UNIQUE(`LEAST(requester_id, addressee_id)`, `GREATEST(requester_id, addressee_id)`) — prevents duplicate/reverse pairs

**Indexes:**
- `ix_friendships_requester` ON (`requester_id`) WHERE `status = 'accepted'`
- `ix_friendships_addressee` ON (`addressee_id`) WHERE `status = 'pending'` — pending request inbox

---

### 11. `blocks`

One-directional. Blocker no longer sees blocked user in matchmaking/rooms.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `UUID` | PK, DEFAULT gen_random_uuid() | |
| `blocker_id` | `UUID` | FK → users(id), NOT NULL | |
| `blocked_id` | `UUID` | FK → users(id), NOT NULL | |
| `reason` | `TEXT` | NULL | Optional note for admin review |
| `created_at` | `TIMESTAMPTZ` | NOT NULL, DEFAULT now() | |

**Constraints:**
- `chk_no_self_block` — CHECK(`blocker_id != blocked_id`)
- `uq_block_pair` — UNIQUE(`blocker_id`, `blocked_id`)

**Indexes:**
- `ix_blocks_blocker` ON (`blocker_id`) — "am I blocking this person?" checks

---

## Things You Might Be Missing

### Likely needed soon

| Topic | Recommendation |
|-------|---------------|
| **Player statistics view** | Create a `VIEW v_player_stats` that aggregates from `match_players` + `deals` — win rate by mode, favorite position, average deal count, etc. No extra table needed. |
| **Leaderboard caching** | For the ranked leaderboard, consider a materialized view or Redis cache refreshed periodically, rather than querying `players ORDER BY elo_rating` on every page load. |
| **User preferences** | A `user_preferences` table (or JSONB column on `users`) for settings like preferred position, notification prefs, UI theme. Not critical for v1. |
| **Moderation / reports** | If admins need to review player behavior, a `reports` table (reporter, reported, reason, status, admin_notes). Pairs with `is_banned`. |

### Probably not needed yet

| Topic | Why skip for now |
|-------|-----------------|
| **Seasons / rating resets** | Only needed if you plan periodic competitive seasons. Add a `season_id` FK to `elo_history` later. |
| **Achievements / badges** | Gamification feature — can be a `player_achievements` join table added post-launch. |
| **Chat / messages** | You chose basic social. If needed later, better handled by a dedicated service (or a simple `messages` table). |
| **Audit log** | `created_at`/`updated_at` on most tables is sufficient. Full audit logging (who changed what) is overkill for a game. |
| **Replays as blob** | Storing replay as JSONB blob instead of `deal_actions` rows is an alternative. Row-based is better for querying (e.g. "how often does Jack of Spades win trick 1?") but heavier on storage. Your call if analytics matter. |

---

## Volume Estimates

Rough sizing to validate the schema makes sense:

| Entity | Estimate | Notes |
|--------|----------|-------|
| `users` | 1K–100K rows | Depends on adoption |
| `bots` | 5–20 rows | Few bot types |
| `players` | users + bots | ~same as users |
| `matches` | ~10 per active user per week | Moderate |
| `deals` | ~10–20 per match | 10–20x matches |
| `deal_actions` | ~40–60 per deal | 1 cut + ~8 negotiations + ~32 card plays = ~41 min |
| `elo_history` | 4 per match (one per player) | 4x matches |
| `rooms` | ~1 per match | 1:1 with matches |

At 10K users, 100K matches/year → ~5M `deal_actions` rows/year. PostgreSQL handles this fine. Partition `deal_actions` by year if it grows past 50M.

---

## Enum Reference

For documentation — store as `SMALLINT` in DB, map in application code.

```
PlayerPosition: Bottom=0, Left=1, Top=2, Right=3
Team:           Team1=0, Team2=1
GameMode:       ColourClubs=0, ColourDiamonds=1, ColourHearts=2, ColourSpades=3, NoTrumps=4, AllTrumps=5
Multiplier:     Normal=1, Doubled=2, Redoubled=4
CardRank:       Seven=7, Eight=8, Nine=9, Ten=10, Jack=11, Queen=12, King=13, Ace=14
CardSuit:       Clubs=0, Diamonds=1, Hearts=2, Spades=3
ActionType:     cut, announce, accept, double, redouble, play_card (VARCHAR)
RoomStatus:     waiting, playing, completed (VARCHAR — in-memory only, not persisted)
Role:           admin, normal (VARCHAR — extensible)
FriendStatus:   pending, accepted, declined (VARCHAR)
PlayerType:     human, bot (VARCHAR)
```

---

## Open Questions

1. **Initial hand storage** — Should `deal_actions` also store the dealt cards (initial 5 + final 3 per player) so replays can fully reconstruct the hand? Without this, you can infer hands from play order but not show "what cards did each player hold?" at deal start. Suggest adding action types `deal_initial` and `deal_final` with all cards.

2. **Bot Elo K-factor** — Should bots use the same K-factor as humans, or a lower one so their shadow rating stabilizes faster? This is a tuning decision, not a schema one.

3. **Abandoned match Elo** — If a match is abandoned (`was_abandoned = TRUE`), should it still affect Elo? Probably not — but you might want to track a `forfeit_count` on `players` to penalize serial quitters.

4. **Multi-device sessions** — If a user can be logged in on multiple devices, do you need a `sessions` table, or does Keycloak handle this entirely?
