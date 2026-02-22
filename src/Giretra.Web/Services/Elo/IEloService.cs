using Giretra.Core.Players;
using Giretra.Web.Domain;

namespace Giretra.Web.Services.Elo;

public interface IEloService
{
    Task StageMatchEloAsync(Guid matchId, GameSession session);
    Task StageAbandonEloAsync(Guid matchId, GameSession session, PlayerPosition abandonerPosition);
}
