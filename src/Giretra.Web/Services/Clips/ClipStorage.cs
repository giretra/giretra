using Microsoft.Extensions.Options;

namespace Giretra.Web.Services.Clips;

/// <summary>Where a clip's files live on disk: {root}/{first two chars}/{shortId}.{ext}.</summary>
public sealed class ClipStorage
{
    private readonly string _root;

    public ClipStorage(IOptions<ClipOptions> options)
    {
        _root = Path.GetFullPath(options.Value.RootPath);
    }

    public string Root => _root;

    public string PosterPath(string shortId) => PathFor(shortId, "png");

    public string VideoPath(string shortId) => PathFor(shortId, "mp4");

    public void EnsureDirectoryFor(string shortId)
        => Directory.CreateDirectory(Path.GetDirectoryName(PosterPath(shortId))!);

    public void DeleteFiles(string shortId)
    {
        foreach (var path in new[] { PosterPath(shortId), VideoPath(shortId) })
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private string PathFor(string shortId, string extension)
    {
        if (string.IsNullOrEmpty(shortId) || shortId.Any(c => !char.IsLetterOrDigit(c)))
            throw new ArgumentException("Invalid short id", nameof(shortId));
        return Path.Combine(_root, shortId[..2], $"{shortId}.{extension}");
    }
}
