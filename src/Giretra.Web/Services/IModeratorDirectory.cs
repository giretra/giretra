namespace Giretra.Web.Services;

/// <summary>
/// Looks up the e-mail addresses of the staff accounts (moderators and admins).
/// </summary>
public interface IModeratorDirectory
{
    Task<IReadOnlyList<string>> GetModeratorEmailsAsync(CancellationToken cancellationToken = default);
}
