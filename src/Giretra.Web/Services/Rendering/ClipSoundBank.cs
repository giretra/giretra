using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Giretra.Web.Services.Rendering;

/// <summary>
/// The app's sound assets (<c>/assets/sounds/{name}.mp3</c>, fetched from the web origin so the
/// bundle stays the single source of truth), decoded once by ffmpeg to 44.1 kHz stereo PCM.
/// A sample that cannot be loaded is skipped: the clip is rendered without it, never failed.
/// </summary>
public sealed partial class ClipSoundBank
{
    private readonly ClipRenderOptions _options;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<ClipSoundBank> _logger;
    private readonly ConcurrentDictionary<string, short[]?> _cache = new();

    public ClipSoundBank(IOptions<ClipRenderOptions> options, IHttpClientFactory http, ILogger<ClipSoundBank> logger)
    {
        _options = options.Value;
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, short[]>> LoadAsync(IEnumerable<string> names, CancellationToken ct)
    {
        var result = new Dictionary<string, short[]>();
        foreach (var name in names.Distinct())
        {
            if (!SoundName().IsMatch(name))
                continue;
            if (!_cache.TryGetValue(name, out var pcm))
            {
                pcm = await DecodeAsync(name, ct);
                _cache[name] = pcm;
            }
            if (pcm != null)
                result[name] = pcm;
        }
        return result;
    }

    private async Task<short[]?> DecodeAsync(string name, CancellationToken ct)
    {
        var url = $"{_options.WebBaseUrl.TrimEnd('/')}/assets/sounds/{name}.mp3";
        try
        {
            using var http = _http.CreateClient();
            var encoded = await http.GetByteArrayAsync(url, ct);

            var psi = new ProcessStartInfo(_options.FfmpegPath)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var a in new[]
                     {
                         "-hide_banner", "-loglevel", "error", "-i", "pipe:0",
                         "-f", "s16le", "-ac", ClipAudio.Channels.ToString(), "-ar", ClipAudio.SampleRate.ToString(), "pipe:1",
                     })
                psi.ArgumentList.Add(a);

            using var ffmpeg = Process.Start(psi) ?? throw new InvalidOperationException("ffmpeg did not start");
            var stderr = ffmpeg.StandardError.ReadToEndAsync(ct);
            var output = new MemoryStream();
            var read = ffmpeg.StandardOutput.BaseStream.CopyToAsync(output, ct);
            await ffmpeg.StandardInput.BaseStream.WriteAsync(encoded, ct);
            ffmpeg.StandardInput.Close();
            await read;
            await ffmpeg.WaitForExitAsync(ct);
            if (ffmpeg.ExitCode != 0)
                throw new InvalidOperationException($"ffmpeg exit {ffmpeg.ExitCode}: {(await stderr).Trim()}");

            var pcm = ClipAudio.FromS16Le(output.GetBuffer().AsSpan(0, (int)output.Length));
            _logger.LogInformation("Sound {Name} loaded ({Ms} ms)", name, pcm.Length / ClipAudio.Channels * 1000 / ClipAudio.SampleRate);
            return pcm;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Sound {Name} unavailable from {Url}; clips render without it", name, url);
            return null;
        }
    }

    [GeneratedRegex("^[a-z0-9_]{1,40}$")]
    private static partial Regex SoundName();
}
