using Giretra.Core.Players;
using Giretra.Model;
using Giretra.Web.Domain;
using Giretra.Web.Repositories;
using Giretra.Web.Services.Elo;
using ModelEntities = Giretra.Model.Entities;
using ModelEnums = Giretra.Model.Enums;

namespace Giretra.Web.Services;

public sealed class MatchPersistenceService : IMatchPersistenceService
{
    private readonly GiretraDbContext _dbContext;
    private readonly IRoomRepository _roomRepository;
    private readonly IEloService _eloService;
    private readonly ILogger<MatchPersistenceService> _logger;

    public MatchPersistenceService(GiretraDbContext dbContext,
        IRoomRepository roomRepository,
        IEloService eloService,
        ILogger<MatchPersistenceService> logger)
    {
        _dbContext = dbContext;
        _roomRepository = roomRepository;
        _eloService = eloService;
        _logger = logger;
    }

    public async Task PersistCompletedMatchAsync(GameSession session)
    {
        var matchState = session.MatchState;
        if (matchState == null)
        {
            _logger.LogWarning("Cannot persist game {GameId}: no match state", session.GameId);
            return;
        }

        var room = _roomRepository.GetById(session.RoomId);
        var roomName = room?.Name ?? session.RoomId;

        var matchId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Stage Elo changes BEFORE Match/Deal entities so that ResolvePlayersAsync's
        // SaveChangesAsync (for new Player rows) doesn't prematurely commit the Match.
        var isRanked = true;
        if (isRanked)
        {
            await _eloService.StageMatchEloAsync(matchId, session);
        }

        // Create Match entity
        var match = new ModelEntities.Match
        {
            Id = matchId,
            RoomName = roomName,
            TargetScore = matchState.TargetScore,
            Team1FinalScore = matchState.Team1MatchPoints,
            Team2FinalScore = matchState.Team2MatchPoints,
            WinnerTeam = matchState.Winner.HasValue
                ? (ModelEnums.Team)(int)matchState.Winner.Value
                : null,
            TotalDeals = matchState.CompletedDeals.Count,
            IsRanked = isRanked,
            WasAbandoned = false,
            StartedAt = new DateTimeOffset(session.StartedAt, TimeSpan.Zero),
            CompletedAt = session.CompletedAt.HasValue
                ? new DateTimeOffset(session.CompletedAt.Value, TimeSpan.Zero)
                : now,
            DurationSeconds = session.CompletedAt.HasValue
                ? (int)(session.CompletedAt.Value - session.StartedAt).TotalSeconds
                : null,
            CreatedAt = now
        };

        _dbContext.Matches.Add(match);

        // Create Deal entities from CompletedDeals
        var recordedDeals = session.ActionRecorder?.GetDeals() ?? [];

        for (var i = 0; i < matchState.CompletedDeals.Count; i++)
        {
            var dealResult = matchState.CompletedDeals[i];
            var dealNumber = (short)(i + 1);
            var dealId = Guid.NewGuid();

            // Find matching recorded deal for dealer position
            var recordedDeal = recordedDeals.FirstOrDefault(rd => rd.DealNumber == dealNumber);

            var deal = new ModelEntities.Deal
            {
                Id = dealId,
                MatchId = matchId,
                DealNumber = dealNumber,
                DealerPosition = recordedDeal != null
                    ? (ModelEnums.PlayerPosition)(int)recordedDeal.DealerPosition
                    : default,
                GameMode = (ModelEnums.GameMode)(int)dealResult.GameMode,
                AnnouncerTeam = (ModelEnums.Team)(int)dealResult.AnnouncerTeam,
                Multiplier = (ModelEnums.MultiplierState)(int)dealResult.Multiplier,
                Team1CardPoints = dealResult.Team1CardPoints,
                Team2CardPoints = dealResult.Team2CardPoints,
                Team1MatchPoints = dealResult.Team1MatchPoints,
                Team2MatchPoints = dealResult.Team2MatchPoints,
                WasSweep = dealResult.WasSweep,
                SweepingTeam = dealResult.SweepingTeam.HasValue
                    ? (ModelEnums.Team)(int)dealResult.SweepingTeam.Value
                    : null,
                IsInstantWin = dealResult.IsInstantWin,
                AnnouncerWon = dealResult.AnnouncerWon,
                StartedAt = now, // We don't track per-deal timing yet
                CompletedAt = now
            };

            _dbContext.Deals.Add(deal);

            // Create DealAction entities from recorded actions
            if (recordedDeal != null)
            {
                foreach (var action in recordedDeal.Actions)
                {
                    var dealAction = new ModelEntities.DealAction
                    {
                        Id = Guid.NewGuid(),
                        DealId = dealId,
                        ActionOrder = (short)action.ActionOrder,
                        ActionType = (ModelEnums.ActionType)(int)action.ActionType,
                        PlayerPosition = (ModelEnums.PlayerPosition)(int)action.PlayerPosition,
                        CardRank = action.CardRank.HasValue
                            ? (ModelEnums.CardRank)(int)action.CardRank.Value
                            : null,
                        CardSuit = action.CardSuit.HasValue
                            ? (ModelEnums.CardSuit)(int)action.CardSuit.Value
                            : null,
                        GameMode = action.GameMode.HasValue
                            ? (ModelEnums.GameMode)(int)action.GameMode.Value
                            : null,
                        CutPosition = action.CutPosition.HasValue
                            ? (short)action.CutPosition.Value
                            : null,
                        CutFromTop = action.CutFromTop,
                        TrickNumber = action.TrickNumber.HasValue
                            ? (short)action.TrickNumber.Value
                            : null
                    };

                    _dbContext.DealActions.Add(dealAction);
                }
            }
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        _logger.LogInformation(
            "Persisted match {MatchId} for game {GameId}: {DealCount} deals, {ActionCount} actions",
            matchId, session.GameId, matchState.CompletedDeals.Count,
            recordedDeals.Sum(d => d.Actions.Count));
    }

    public async Task PersistAbandonedMatchAsync(GameSession session, PlayerPosition abandonerPosition)
    {
        var matchState = session.MatchState;
        var room = _roomRepository.GetById(session.RoomId);
        var roomName = room?.Name ?? session.RoomId;

        var matchId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var abandonerTeam = abandonerPosition.GetTeam();
        var winnerTeam = abandonerTeam == Core.Players.Team.Team1
            ? Core.Players.Team.Team2
            : Core.Players.Team.Team1;

        // Stage Elo changes BEFORE Match/Deal entities so that ResolvePlayersAsync's
        // SaveChangesAsync (for new Player rows) doesn't prematurely commit the Match.
        var isRanked = true;
        if (isRanked)
        {
            await _eloService.StageAbandonEloAsync(matchId, session, abandonerPosition);
        }

        // Create Match entity
        var match = new ModelEntities.Match
        {
            Id = matchId,
            RoomName = roomName,
            TargetScore = matchState?.TargetScore ?? 150,
            Team1FinalScore = matchState?.Team1MatchPoints ?? 0,
            Team2FinalScore = matchState?.Team2MatchPoints ?? 0,
            WinnerTeam = (ModelEnums.Team)(int)winnerTeam,
            TotalDeals = matchState?.CompletedDeals.Count ?? 0,
            IsRanked = isRanked,
            WasAbandoned = true,
            StartedAt = new DateTimeOffset(session.StartedAt, TimeSpan.Zero),
            CompletedAt = now,
            DurationSeconds = (int)(DateTimeOffset.UtcNow - new DateTimeOffset(session.StartedAt, TimeSpan.Zero)).TotalSeconds,
            CreatedAt = now
        };

        _dbContext.Matches.Add(match);

        // Persist any completed deals
        if (matchState != null)
        {
            var recordedDeals = session.ActionRecorder?.GetDeals() ?? [];

            for (var i = 0; i < matchState.CompletedDeals.Count; i++)
            {
                var dealResult = matchState.CompletedDeals[i];
                var dealNumber = (short)(i + 1);
                var dealId = Guid.NewGuid();
                var recordedDeal = recordedDeals.FirstOrDefault(rd => rd.DealNumber == dealNumber);

                var deal = new ModelEntities.Deal
                {
                    Id = dealId,
                    MatchId = matchId,
                    DealNumber = dealNumber,
                    DealerPosition = recordedDeal != null
                        ? (ModelEnums.PlayerPosition)(int)recordedDeal.DealerPosition
                        : default,
                    GameMode = (ModelEnums.GameMode)(int)dealResult.GameMode,
                    AnnouncerTeam = (ModelEnums.Team)(int)dealResult.AnnouncerTeam,
                    Multiplier = (ModelEnums.MultiplierState)(int)dealResult.Multiplier,
                    Team1CardPoints = dealResult.Team1CardPoints,
                    Team2CardPoints = dealResult.Team2CardPoints,
                    Team1MatchPoints = dealResult.Team1MatchPoints,
                    Team2MatchPoints = dealResult.Team2MatchPoints,
                    WasSweep = dealResult.WasSweep,
                    SweepingTeam = dealResult.SweepingTeam.HasValue
                        ? (ModelEnums.Team)(int)dealResult.SweepingTeam.Value
                        : null,
                    IsInstantWin = dealResult.IsInstantWin,
                    AnnouncerWon = dealResult.AnnouncerWon,
                    StartedAt = now,
                    CompletedAt = now
                };

                _dbContext.Deals.Add(deal);

                if (recordedDeal != null)
                {
                    foreach (var action in recordedDeal.Actions)
                    {
                        _dbContext.DealActions.Add(new ModelEntities.DealAction
                        {
                            Id = Guid.NewGuid(),
                            DealId = dealId,
                            ActionOrder = (short)action.ActionOrder,
                            ActionType = (ModelEnums.ActionType)(int)action.ActionType,
                            PlayerPosition = (ModelEnums.PlayerPosition)(int)action.PlayerPosition,
                            CardRank = action.CardRank.HasValue
                                ? (ModelEnums.CardRank)(int)action.CardRank.Value
                                : null,
                            CardSuit = action.CardSuit.HasValue
                                ? (ModelEnums.CardSuit)(int)action.CardSuit.Value
                                : null,
                            GameMode = action.GameMode.HasValue
                                ? (ModelEnums.GameMode)(int)action.GameMode.Value
                                : null,
                            CutPosition = action.CutPosition.HasValue
                                ? (short)action.CutPosition.Value
                                : null,
                            CutFromTop = action.CutFromTop,
                            TrickNumber = action.TrickNumber.HasValue
                                ? (short)action.TrickNumber.Value
                                : null
                        });
                    }
                }
            }
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        _logger.LogInformation(
            "Persisted abandoned match {MatchId} for game {GameId}, abandoner at {Position}",
            matchId, session.GameId, abandonerPosition);
    }
}
