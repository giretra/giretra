namespace Giretra.Web.Services.Rendering;

public sealed class ClipRenderOptions
{
    public const string Section = "Render";

    /// <summary>Run the render loop in this process. Off in the web container, on in the render container.</summary>
    public bool Enabled { get; set; }

    /// <summary>Origin the headless browser loads the replay pages from, e.g. http://web:8080.</summary>
    public string WebBaseUrl { get; set; } = "http://localhost:4200";

    /// <summary>Path of a Chromium binary; null uses the browser installed by Playwright.</summary>
    public string? ChromiumPath { get; set; }

    /// <summary>Needed when Chromium runs as root in a container.</summary>
    public bool NoSandbox { get; set; } = true;

    public int PollIntervalSeconds { get; set; } = 2;
    public int JobTimeoutSeconds { get; set; } = 180;
    public int MaxAttempts { get; set; } = 2;
    /// <summary>Recycle the browser after this many jobs to bound memory growth.</summary>
    public int BrowserRecycleAfterJobs { get; set; } = 20;

    public int ViewportWidth { get; set; } = 540;
    public int ViewportHeight { get; set; } = 960;
    public float DeviceScaleFactor { get; set; } = 2f;
}
