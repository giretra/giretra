using Giretra.Core.Players;
using Giretra.Model;
using Giretra.Web.Domain;
using Microsoft.EntityFrameworkCore;
using ModelEntities = Giretra.Model.Entities;
using ModelEnums = Giretra.Model.Enums;
using PlayerPosition = Giretra.Core.Players.PlayerPosition;

namespace Giretra.Web.Services.Elo;

public sealed class EloService : IEloService
{
    private readonly GiretraDbContext _db;
    private readonly EloCalculationService _calc;
    private readonly ILogger<EloService> _logger;

    public EloService(GiretraDbContext db, EloCalculationService calc, ILogger<EloService> logger)
    {
        _db = db;
        _calc = calc;
        _logger = logger;
    }

    public async Task StageMatchEloAsync(Guid matchId, GameSession session)
    {
        var matchState = session.MatchState;
        if (matchState?.Winner == null)
            return;

        var winnerTeam = matchState.Winner.Value;
        var now = DateTimeOffset.UtcNow;

        // Resolve Player IDs for all 4 positions
        var playerMap = await ResolvePlayersAsync(session.PlayerComposition);
        var involvedBots = session.PlayerComposition.Values.Any(p => p.IsBot);

        // Read current Elo ratings
        var currentElos = playerMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.EloRating);

        // Compute weekly bot-Elo totals per human (only if bots involved)
        var weeklyBotGains = new Dictionary<PlayerPosition, int>();
        if (involvedBots)
        {
            var sevenDaysAgo = now.AddDays(-7);
            var humanPlayerIds = playerMap
                .Where(kvp => !session.PlayerComposition[kvp.Key].IsBot)
                .Select(kvp => kvp.Value.Id)
                .ToList();

            if (humanPlayerIds.Count > 0)
            {
                var gains = await _db.EloHistories
                    .Where(eh => humanPlayerIds.Contains(eh.PlayerId)
                        && eh.InvolvedBots
                        && eh.EloChange > 0
                        && eh.RecordedAt >= sevenDaysAgo)
                    .GroupBy(eh => eh.PlayerId)
                    .Select(g => new { PlayerId = g.Key, Total = g.Sum(eh => eh.EloChange) })
                    .ToListAsync();

                var gainsByPlayerId = gains.ToDictionary(g => g.PlayerId, g => g.Total);

                foreach (var (pos, player) in playerMap)
                {
                    if (!session.PlayerComposition[pos].IsBot)
                        weeklyBotGains[pos] = gainsByPlayerId.GetValueOrDefault(player.Id, 0);
                }
            }
        }

        // Compute Elo deltas for each position
        foreach (var position in Enum.GetValues<PlayerPosition>())
        {
            var info = session.PlayerComposition[position];
            var player = playerMap[position];
            var isWinner = position.GetTeam() == winnerTeam;

            // Compute opponent composite Elo (average of opposing team)
            var oppTeam = position.GetTeam() == Team.Team1 ? Team.Team2 : Team.Team1;
            var oppPositions = Enum.GetValues<PlayerPosition>().Where(p => p.GetTeam() == oppTeam).ToList();
            var oppComposite = oppPositions.Average(p => (double)currentElos[p]);

            // Count bot-teammates and bot-opponents
            var hasBotTeammate = session.PlayerComposition[position.Teammate()].IsBot;
            var botOpponentCount = oppPositions.Count(p => session.PlayerComposition[p].IsBot);

            var ctx = new PlayerContext(
                PlayerId: player.Id,
                CurrentElo: currentElos[position],
                IsBot: info.IsBot,
                IsWinner: isWinner,
                OpponentCompositeElo: oppComposite,
                InvolvedBots: involvedBots,
                HasBotTeammate: hasBotTeammate,
                BotOpponentCount: botOpponentCount,
                WeeklyBotEloGained: weeklyBotGains.GetValueOrDefault(position, 0)
            );

            var eloResult = _calc.ComputeNormalMatchDelta(ctx);

            // Stage MatchPlayer record
            _db.MatchPlayers.Add(new ModelEntities.MatchPlayer
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                PlayerId = player.Id,
                Position = (ModelEnums.PlayerPosition)(int)position,
                Team = (ModelEnums.Team)(int)position.GetTeam(),
                IsWinner = isWinner,
                EloBefore = eloResult.EloBefore,
                EloAfter = eloResult.EloAfter,
                EloChange = eloResult.EloChange
            });

            // Stage EloHistory and update Player for humans only
            if (!info.IsBot)
            {
                _db.EloHistories.Add(new ModelEntities.EloHistory
                {
                    Id = Guid.NewGuid(),
                    PlayerId = player.Id,
                    MatchId = matchId,
                    EloBefore = eloResult.EloBefore,
                    EloAfter = eloResult.EloAfter,
                    EloChange = eloResult.EloChange,
                    InvolvedBots = involvedBots,
                    RecordedAt = now
                });

                player.EloRating = eloResult.EloAfter;
                player.GamesPlayed++;
                if (isWinner)
                {
                    player.GamesWon++;
                    player.WinStreak++;
                    if (player.WinStreak > player.BestWinStreak)
                        player.BestWinStreak = player.WinStreak;
                }
                else
                {
                    player.WinStreak = 0;
                }
                player.UpdatedAt = now;
            }
        }

        _logger.LogInformation("Staged Elo changes for match {MatchId}", matchId);
    }

    public async Task StageAbandonEloAsync(Guid matchId, GameSession session, PlayerPosition abandonerPosition)
    {
        var now = DateTimeOffset.UtcNow;
        var abandonerTeam = abandonerPosition.GetTeam();

        var playerMap = await ResolvePlayersAsync(session.PlayerComposition);

        foreach (var position in Enum.GetValues<PlayerPosition>())
        {
            var info = session.PlayerComposition[position];
            var player = playerMap[position];

            AbandonRole role;
            bool isWinner;

            if (position == abandonerPosition)
            {
                role = AbandonRole.Abandoner;
                isWinner = false;
            }
            else if (position.GetTeam() == abandonerTeam)
            {
                role = AbandonRole.TeammateOfAbandoner;
                isWinner = false;
            }
            else
            {
                role = AbandonRole.Opponent;
                isWinner = true;
            }

            EloResult eloResult;
            if (info.IsBot)
            {
                eloResult = new EloResult(player.Id, player.EloRating, player.EloRating, 0);
            }
            else
            {
                eloResult = _calc.ComputeAbandonDelta(player.Id, player.EloRating, role);
            }

            // Stage MatchPlayer record
            _db.MatchPlayers.Add(new ModelEntities.MatchPlayer
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                PlayerId = player.Id,
                Position = (ModelEnums.PlayerPosition)(int)position,
                Team = (ModelEnums.Team)(int)position.GetTeam(),
                IsWinner = isWinner,
                EloBefore = eloResult.EloBefore,
                EloAfter = eloResult.EloAfter,
                EloChange = eloResult.EloChange
            });

            // Stage EloHistory and update Player for humans only
            if (!info.IsBot && eloResult.EloChange != 0)
            {
                _db.EloHistories.Add(new ModelEntities.EloHistory
                {
                    Id = Guid.NewGuid(),
                    PlayerId = player.Id,
                    MatchId = matchId,
                    EloBefore = eloResult.EloBefore,
                    EloAfter = eloResult.EloAfter,
                    EloChange = eloResult.EloChange,
                    InvolvedBots = session.PlayerComposition.Values.Any(p => p.IsBot),
                    RecordedAt = now
                });

                player.EloRating = eloResult.EloAfter;
                player.UpdatedAt = now;
            }
        }

        _logger.LogInformation("Staged abandon Elo changes for match {MatchId}, abandoner at {Position}",
            matchId, abandonerPosition);
    }

    private async Task<Dictionary<PlayerPosition, ModelEntities.Player>> ResolvePlayersAsync(
        IReadOnlyDictionary<PlayerPosition, MatchPlayerInfo> composition)
    {
        var result = new Dictionary<PlayerPosition, ModelEntities.Player>();

        foreach (var (position, info) in composition)
        {
            ModelEntities.Player? player;

            if (!info.IsBot && info.UserId.HasValue)
            {
                // Human: find by UserId
                player = await _db.Players.FirstOrDefaultAsync(p => p.UserId == info.UserId.Value);
                if (player == null)
                {
                    // Auto-create (same pattern as ProfileService)
                    player = new ModelEntities.Player
                    {
                        PlayerType = ModelEnums.PlayerType.Human,
                        UserId = info.UserId.Value,
                        EloRating = 1000,
                        EloIsPublic = true,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    _db.Players.Add(player);
                    await _db.SaveChangesAsync();
                }
            }
            else
            {
                // Bot: find by AgentType → Bot → Player
                var bot = await _db.Bots
                    .Include(b => b.Player)
                    .FirstOrDefaultAsync(b => b.AgentType == info.AiAgentType);

                if (bot?.Player != null)
                {
                    player = bot.Player;
                    // Sync Player.EloRating with Bot.Rating (authoritative source for bots)
                    if (player.EloRating != bot.Rating)
                    {
                        player.EloRating = bot.Rating;
                        player.UpdatedAt = DateTimeOffset.UtcNow;
                    }
                }
                else if (bot != null)
                {
                    // Bot exists but no Player row yet
                    player = new ModelEntities.Player
                    {
                        PlayerType = ModelEnums.PlayerType.Bot,
                        BotId = bot.Id,
                        EloRating = bot.Rating,
                        EloIsPublic = true,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    _db.Players.Add(player);
                    await _db.SaveChangesAsync();
                }
                else
                {
                    // Bot type not in database — create Bot + Player
                    var newBot = new ModelEntities.Bot
                    {
                        Id = Guid.NewGuid(),
                        AgentType = info.AiAgentType ?? "Unknown",
                        DisplayName = info.AiAgentType ?? "Unknown Bot",
                        Difficulty = 1,
                        IsActive = true,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    _db.Bots.Add(newBot);

                    player = new ModelEntities.Player
                    {
                        PlayerType = ModelEnums.PlayerType.Bot,
                        BotId = newBot.Id,
                        EloRating = newBot.Rating,
                        EloIsPublic = true,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    _db.Players.Add(player);
                    await _db.SaveChangesAsync();
                }
            }

            result[position] = player;
        }

        return result;
    }
}
