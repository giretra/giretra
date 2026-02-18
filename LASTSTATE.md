
● Everything looks good. Here's a summary of all changes made:

  Implementation Summary

  New Files (7)

  1. Giretra.Web/Domain/MatchPlayerInfo.cs — Record holding per-position player metadata (human/bot, UserId, agent type)
  2. Giretra.Web/Services/Elo/EloConstants.cs — All tunable constants (K-factor, multipliers, caps)
  3. Giretra.Web/Services/Elo/EloCalculationService.cs — Pure math service with ComputeNormalMatchDelta and
  ComputeAbandonDelta, plus EloResult, PlayerContext, and AbandonRole types
  4. Giretra.Web/Services/Elo/IEloService.cs — Interface for DB-aware Elo orchestrator
  5. Giretra.Web/Services/Elo/EloService.cs — DB-aware orchestrator: resolves players, queries weekly bot-Elo gains,
  stages MatchPlayer/EloHistory/Player updates
  6. Giretra.Web/Models/Events/MatchAbandonedEvent.cs — SignalR event for abandonment notification
  7. Giretra.Web.Tests/Services/Elo/EloCalculationServiceTests.cs — 16 unit tests covering normal Elo, bot gate,
  multipliers, weekly cap, and abandonment

  Modified Files (11)

  1. Giretra.Model/Entities/EloHistory.cs — Added InvolvedBots property
  2. Giretra.Model/Configuration/EloHistoryConfiguration.cs — Added default value + filtered index for bot-Elo cap
  queries
  3. Giretra.Web/Domain/GameSession.cs — Added PlayerComposition required property
  4. Giretra.Web/Services/GameService.cs — Builds PlayerComposition in CreateGame; added AbandonGameAsync (cancels game
  loop, persists abandoned match, notifies room, resets to Waiting)
  5. Giretra.Web/Services/IGameService.cs — Added AbandonGameAsync declaration
  6. Giretra.Web/Services/MatchPersistenceService.cs — Injected IEloService; wrapped saves in transactions; added
  PersistAbandonedMatchAsync with full deal persistence + Elo staging
  7. Giretra.Web/Services/IMatchPersistenceService.cs — Added PersistAbandonedMatchAsync declaration
  8. Giretra.Web/Services/RoomService.cs — Added logger; ScheduleDelayedRemoval now detects active-game disconnect and
  triggers AbandonGameAsync
  9. Giretra.Web/Services/UserSyncService.cs — Auto-creates Player row on user sync
  10. Giretra.Web/Services/NotificationService.cs + INotificationService.cs — Added NotifyMatchAbandonedAsync
  11. Giretra.Web/Program.cs — Registered EloCalculationService (singleton) and IEloService (scoped); bot Player row
  seeding at startup

  Test Fixes (2)

  - RoomServiceTests.cs — Added PlayerComposition to mock sessions + logger parameter
  - RoomToGameFlowTests.cs — Added logger parameter to RoomService constructor

  Results

  - All 234 Core tests pass
  - All 93 Web tests pass (77 existing + 16 new Elo tests)
