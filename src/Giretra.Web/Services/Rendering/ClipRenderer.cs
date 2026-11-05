using Giretra.Web.Services.Clips;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace Giretra.Web.Services.Rendering;

/// <summary>
/// Drives the public replay page of a clip in headless Chromium. Phase 2 renders the poster
/// (the frame where the first trick of the clip is complete); phase 3 adds the video loop.
/// One browser per process, one context per job.
/// </summary>
public sealed class ClipRenderer : IAsyncDisposable
{
    private readonly ClipRenderOptions _options;
    private readonly ClipStorage _storage;
    private readonly ILogger<ClipRenderer> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private int _jobsSinceLaunch;

    public ClipRenderer(IOptions<ClipRenderOptions> options, ClipStorage storage, ILogger<ClipRenderer> logger)
    {
        _options = options.Value;
        _storage = storage;
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
            var posterTime = await page.EvaluateAsync<double>("() => window.__giretra.posterTime ?? 0");
            var duration = await page.EvaluateAsync<double>("() => window.__giretra.duration");

            _storage.EnsureDirectoryFor(job.ShortId);
            var posterPath = _storage.PosterPath(job.ShortId);
            var tmp = posterPath + ".tmp";
            await page.EvaluateAsync("t => window.__giretra.seek(t)", posterTime);
            await page.ScreenshotAsync(new() { Path = tmp, Type = ScreenshotType.Png, Caret = ScreenshotCaret.Hide });
            File.Move(tmp, posterPath, overwrite: true);

            return new ClipRenderOutput(HasPoster: true, HasVideo: false, DurationMs: (int)Math.Round(duration));
        }
        finally
        {
            _gate.Release();
            if (_jobsSinceLaunch >= _options.BrowserRecycleAfterJobs)
                await ResetAsync();
        }
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
