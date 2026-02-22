using System.Security.Claims;
using Giretra.Model.Entities;

namespace Giretra.Web.Services;

public interface IUserSyncService
{
    Task<User> SyncUserAsync(ClaimsPrincipal principal);
}
