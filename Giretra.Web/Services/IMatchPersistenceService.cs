using Giretra.Web.Domain;

namespace Giretra.Web.Services;

public interface IMatchPersistenceService
{
    Task PersistCompletedMatchAsync(GameSession session);
}
