namespace Giretra.Web.Services.Rendering;

public sealed class ClipRenderOptions
{
    public const string Section = "Render";

    /// <summary>Run the render loop in this process. Off in the web container, on in the render container.</summary>
    public bool Enabled { get; set; }

    /// <summary>Origin the headless browser loads the replay pages (and the sound samples) from, e.g. http://web:8080.</summary>
    public string WebBaseUrl { get; set; } = "http://localhost:4200";

    /// <summary>Path of a Chromium binary; null uses the browser installed by Playwright.</summary>
    public string? ChromiumPath { get; set; }

    /// <summary>Needed when Chromium runs as root in a container.</summary>
    public bool NoSandbox { get; set; } = true;

    public int PollIntervalSeconds { get; set; } = 2;

    /// <summary>Whole job (poster + video). A full 8-trick clip is ~1200 frames at ~85 ms each on one core.</summary>
    public int JobTimeoutSeconds { get; set; } = 300;
    public int MaxAttempts { get; set; } = 2;
    /// <summary>Recycle the browser after this many jobs to bound memory growth.</summary>
    public int BrowserRecycleAfterJobs { get; set; } = 20;

    public int ViewportWidth { get; set; } = 540;
    public int ViewportHeight { get; set; } = 960;
    public float DeviceScaleFactor { get; set; } = 2f;

    // Video
    public string FfmpegPath { get; set; } = "ffmpeg";
    public int Fps { get; set; } = 30;
    /// <summary>JPEG quality of the captured frames (H.264 is lossy anyway; 90 is visually clean).</summary>
    public int JpegQuality { get; set; } = 90;
    public int Crf { get; set; } = 23;
    /// <summary>Set to false to skip the video and only render the poster.</summary>
    public bool Video { get; set; } = true;

    // Audio: the app's own card sounds mixed at the timeline's event times.
    public bool Audio { get; set; } = true;
    public float AudioGain { get; set; } = 0.7f;

    // Retention: clips nobody opened for this long lose their files (Evicted) and are
    // re-rendered on the next visit.
    public int RetentionDays { get; set; } = 180;
    public int RetentionSweepHours { get; set; } = 6;
}
