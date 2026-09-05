using Giretra.Model;
using Giretra.Model.Enums;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Web.Services;

public sealed class DbModeratorDirectory : IModeratorDirectory
{
    private readonly GiretraDbContext _db;

    public DbModeratorDirectory(GiretraDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<string>> GetModeratorEmailsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Users.AsNoTracking()
            .Where(u => u.Role != UserRole.Normal && !u.IsBanned && u.Email != null && u.Email != "")
            .Select(u => u.Email!)
            .ToListAsync(cancellationToken);
    }
}
