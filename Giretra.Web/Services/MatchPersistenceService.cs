using Giretra.Model;
using Giretra.Web.Domain;
using Giretra.Web.Players;
using Giretra.Web.Repositories;
using ModelEntities = Giretra.Model.Entities;
using ModelEnums = Giretra.Model.Enums;

namespace Giretra.Web.Services;

public sealed class MatchPersistenceService(
    GiretraDbContext dbContext,
    IRoomRepository roomRepository,
    ILogger<MatchPersistenceService> logger) : IMatchPersistenceService
{
    public async Task PersistCompletedMatchAsync(GameSession session)
    {
        var matchState = session.MatchState;
        if (matchState == null)
        {
            logger.LogWarning("Cannot persist game {GameId}: no match state", session.GameId);
            return;
        }

        var room = roomRepository.GetById(session.RoomId);
        var roomName = room?.Name ?? session.RoomId;

        var matchId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

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
            IsRanked = true,
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

        dbContext.Matches.Add(match);

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

            dbContext.Deals.Add(deal);

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

                    dbContext.DealActions.Add(dealAction);
                }
            }
        }

        await dbContext.SaveChangesAsync();

        logger.LogInformation(
            "Persisted match {MatchId} for game {GameId}: {DealCount} deals, {ActionCount} actions",
            matchId, session.GameId, matchState.CompletedDeals.Count,
            recordedDeals.Sum(d => d.Actions.Count));
    }
}
