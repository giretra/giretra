using System.Diagnostics;
using System.Text.Json;
using Giretra.Web.Services.Clips;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace Giretra.Web.Services.Rendering;

/// <summary>
/// Drives the public replay page of a clip in headless Chromium: one screenshot per video
/// frame (the page is time-driven, so <c>seek(t)</c> is deterministic), piped to ffmpeg, with
/// the app's card sounds mixed at the timeline's event times. Also writes the poster (the
/// frame where the first trick of the clip is complete). One browser per process, one
/// context per job.
/// </summary>
public sealed class ClipRenderer : IAsyncDisposable
{
    private readonly ClipRenderOptions _options;
    private readonly ClipStorage _storage;
    private readonly ClipSoundBank _sounds;
    private readonly ILogger<ClipRenderer> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private int _jobsSinceLaunch;

    public ClipRenderer(IOptions<ClipRenderOptions> options, ClipStorage storage, ClipSoundBank sounds, ILogger<ClipRenderer> logger)
    {
        _options = options.Value;
        _storage = storage;
        _sounds = sounds;
        _logger = logger;
    }

    public async Task<ClipRenderOutput> RenderAsync(ClipJob job, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var browser = await GetBrowserAsync();
            _jobsSinceLaunch++;

            await using var context = await browser.NewContextAsync(new()
            {
                ViewportSize = new() { Width = _options.ViewportWidth, Height = _options.ViewportHeight },
                DeviceScaleFactor = _options.DeviceScaleFactor,
                Locale = job.Locale switch { "fr" => "fr-FR", "mg" => "mg-MG", _ => "en-US" },
                TimezoneId = "Europe/Paris",
                ReducedMotion = ReducedMotion.Reduce,
            });
            var page = await context.NewPageAsync();
            page.SetDefaultTimeout(30_000);

            var url = $"{_options.WebBaseUrl.TrimEnd('/')}/replay/clip/{job.ShortId}?render=1";
            await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle });
            await page.WaitForFunctionAsync("() => !!window.__giretra");
            await page.EvaluateAsync("() => document.fonts.ready");
            var posterTime = await page.EvaluateAsync<double>("() => window.__giretra.posterTime ?? 0");
            var duration = await page.EvaluateAsync<double>("() => window.__giretra.duration");
            var events = ParseEvents(await page.EvaluateAsync<JsonElement?>("() => window.__giretra.events ?? []"));
            ct.ThrowIfCancellationRequested();

            _storage.EnsureDirectoryFor(job.ShortId);
            await RenderPosterAsync(page, job.ShortId, posterTime);

            var hasVideo = false;
            if (_options.Video)
            {
                await RenderVideoAsync(page, job.ShortId, duration, events, ct);
                hasVideo = true;
            }

            return new ClipRenderOutput(HasPoster: true, HasVideo: hasVideo, DurationMs: (int)Math.Round(duration));
        }
        finally
        {
            _gate.Release();
            if (_jobsSinceLaunch >= _options.BrowserRecycleAfterJobs)
                await ResetAsync();
        }
    }

    private async Task RenderPosterAsync(IPage page, string shortId, double posterTime)
    {
        var posterPath = _storage.PosterPath(shortId);
        var tmp = posterPath + ".tmp";
        await page.EvaluateAsync("t => window.__giretra.seek(t)", posterTime);
        await page.ScreenshotAsync(new() { Path = tmp, Type = ScreenshotType.Png, Caret = ScreenshotCaret.Hide });
        File.Move(tmp, posterPath, overwrite: true);
    }

    private async Task RenderVideoAsync(IPage page, string shortId, double durationMs, SoundEvent[] events, CancellationToken ct)
    {
        var fps = Math.Clamp(_options.Fps, 1, 60);
        var frameCount = (int)Math.Ceiling(durationMs * fps / 1000.0);
        var videoPath = _storage.VideoPath(shortId);
        var tmp = videoPath + ".tmp";
        var wavPath = _options.Audio ? Path.Combine(Path.GetTempPath(), $"giretra-clip-{shortId}-{Environment.ProcessId}.wav") : null;
        var watch = Stopwatch.StartNew();
        long seekMs = 0, shotMs = 0, bytes = 0;

        try
        {
            if (wavPath != null)
                await WriteSoundtrackAsync(wavPath, events, frameCount * 1000 / fps, ct);

            await using var ffmpeg = FfmpegEncoder.Start(_options.FfmpegPath, FfmpegEncoder.BuildArguments(fps, _options.Crf, wavPath, tmp));
            var shot = new PageScreenshotOptions { Type = ScreenshotType.Jpeg, Quality = _options.JpegQuality, Caret = ScreenshotCaret.Hide };
            var lap = new Stopwatch();

            for (var i = 0; i < frameCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                lap.Restart();
                await page.EvaluateAsync("t => window.__giretra.seek(t)", i * 1000.0 / fps);
                seekMs += lap.ElapsedMilliseconds;
                lap.Restart();
                var frame = await page.ScreenshotAsync(shot);
                shotMs += lap.ElapsedMilliseconds;
                bytes += frame.Length;
                await ffmpeg.WriteFrameAsync(frame, ct);
            }

            await ffmpeg.FinishAsync(ct);
            File.Move(tmp, videoPath, overwrite: true);

            _logger.LogInformation(
                "Clip {ShortId} video: {Frames} frames in {Elapsed} ms ({PerFrame:F1} ms/frame: seek {Seek:F1}, screenshot {Shot:F1}), frames {FrameKiB} KiB avg, mp4 {Size} KiB, {Sounds} sound events",
                shortId, frameCount, watch.ElapsedMilliseconds, watch.ElapsedMilliseconds / (double)Math.Max(1, frameCount),
                seekMs / (double)Math.Max(1, frameCount), shotMs / (double)Math.Max(1, frameCount),
                bytes / Math.Max(1, frameCount) / 1024, new FileInfo(videoPath).Length / 1024, events.Length);
        }
        finally
        {
            TryDelete(tmp);
            if (wavPath != null)
                TryDelete(wavPath);
        }
    }

    private async Task WriteSoundtrackAsync(string wavPath, SoundEvent[] events, int durationMs, CancellationToken ct)
    {
        var samples = await _sounds.LoadAsync(events.Select(e => e.Sound), ct);
        var pcm = ClipAudio.Mix(events, samples, durationMs, _options.AudioGain);
        await using var file = new FileStream(wavPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024);
        ClipAudio.WriteWav(file, pcm);
    }

    /// <summary>The page exposes <c>events: [{ t, sound }]</c>; anything malformed is skipped.</summary>
    public static SoundEvent[] ParseEvents(JsonElement? json)
    {
        if (json is not { ValueKind: JsonValueKind.Array } array)
            return [];
        var events = new List<SoundEvent>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            if (item.TryGetProperty("t", out var t) && t.ValueKind == JsonValueKind.Number
                && item.TryGetProperty("sound", out var sound) && sound.ValueKind == JsonValueKind.String)
                events.Add(new SoundEvent(t.GetDouble(), sound.GetString()!));
        }
        return events.ToArray();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException) { /* best effort */ }
    }

    /// <summary>Closes the browser; the next job launches a fresh one.</summary>
    public async Task ResetAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_browser != null)
            {
                try { await _browser.CloseAsync(); }
                catch (Exception ex) { _logger.LogDebug(ex, "Browser close failed"); }
                _browser = null;
            }
            _jobsSinceLaunch = 0;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await ResetAsync();
        _playwright?.Dispose();
        _gate.Dispose();
    }

    private async Task<IBrowser> GetBrowserAsync()
    {
        if (_browser is { IsConnected: true })
            return _browser;

        _playwright ??= await Playwright.CreateAsync();
        var args = new List<string> { "--disable-gpu", "--font-render-hinting=none", "--disable-dev-shm-usage" };
        if (_options.NoSandbox)
            args.Add("--no-sandbox");

        _browser = await _playwright.Chromium.LaunchAsync(new()
        {
            ExecutablePath = string.IsNullOrWhiteSpace(_options.ChromiumPath) ? null : _options.ChromiumPath,
            Headless = true,
            Args = args,
        });
        _jobsSinceLaunch = 0;
        _logger.LogInformation("Chromium {Version} launched for clip rendering", _browser.Version);
        return _browser;
    }
}
