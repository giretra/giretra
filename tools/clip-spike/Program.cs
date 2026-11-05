// Local clip renderer (from the phase 0 spike): drives a replay page (?render=1) frame by frame, pipes frames to ffmpeg,
// hashes every frame, and compares two runs for determinism.
using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Playwright;

var pageUrl = args.Length > 0 ? args[0] : "http://localhost:4200/replay/match/00000000-0000-0000-0000-000000000001/1?render=1";
var outDir = args.Length > 1 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), "out");
var runs = args.Length > 2 ? int.Parse(args[2]) : 2;
var fps = args.Length > 3 ? int.Parse(args[3]) : 30;
var scale = args.Length > 4 ? float.Parse(args[4]) : 2f;
var chrome = Environment.GetEnvironmentVariable("CHROME") ?? "/usr/bin/google-chrome";
var useJpeg = (Environment.GetEnvironmentVariable("FORMAT") ?? "png") == "jpeg";
var quality = int.Parse(Environment.GetEnvironmentVariable("QUALITY") ?? "90");
Directory.CreateDirectory(outDir);

using var pw = await Playwright.CreateAsync();
await using var browser = await pw.Chromium.LaunchAsync(new()
{
    ExecutablePath = chrome,
    Headless = true,
    Args = ["--disable-gpu", "--font-render-hinting=none", "--disable-dev-shm-usage"],
});

var allHashes = new List<List<string>>();
var allFrames = new List<List<byte[]>>();
var dumpMismatches = Environment.GetEnvironmentVariable("DUMP_MISMATCH") == "1";
for (var run = 1; run <= runs; run++)
{
    var frames = new List<byte[]>();
    var hashes = await RenderOnce(run, dumpMismatches ? frames : null);
    allHashes.Add(hashes);
    allFrames.Add(frames);
}

if (runs >= 2)
{
    var a = allHashes[0];
    var b = allHashes[1];
    var mismatches = Enumerable.Range(0, Math.Min(a.Count, b.Count)).Where(i => a[i] != b[i]).ToList();
    Console.WriteLine($"DETERMINISM: frames={a.Count} mismatched={mismatches.Count}" +
        (mismatches.Count > 0 ? $" indices={string.Join(",", mismatches)}" : " (byte-identical)"));
    if (dumpMismatches)
    {
        foreach (var i in mismatches.Take(4))
        {
            await File.WriteAllBytesAsync(Path.Combine(outDir, $"mismatch-{i:D4}-run1.jpg"), allFrames[0][i]);
            await File.WriteAllBytesAsync(Path.Combine(outDir, $"mismatch-{i:D4}-run2.jpg"), allFrames[1][i]);
        }
    }
}

async Task<List<string>> RenderOnce(int run, List<byte[]>? keep)
{
    var context = await browser.NewContextAsync(new()
    {
        ViewportSize = new() { Width = 540, Height = 960 },
        DeviceScaleFactor = scale,
        Locale = "en-US",
        TimezoneId = "Europe/Paris",
        ReducedMotion = ReducedMotion.Reduce,
    });
    await context.AddInitScriptAsync(
        "localStorage.setItem('giretra-offline-username','Spike'); localStorage.setItem('giretra-lang','en');");
    var page = await context.NewPageAsync();
    page.Dialog += (_, d) => d.AcceptAsync("Spike");

    var nav = Stopwatch.StartNew();
    await page.GotoAsync(pageUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });
    await page.WaitForFunctionAsync("() => !!window.__giretra");
    var duration = await page.EvaluateAsync<double>("() => window.__giretra.duration");
    await page.EvaluateAsync("() => window.__giretra.seek(0)");
    await page.EvaluateAsync("() => document.fonts.ready");
    Console.WriteLine($"[run {run}] page ready in {nav.ElapsedMilliseconds} ms, duration {duration} ms");

    var frameCount = (int)Math.Ceiling(duration * fps / 1000.0);
    var mp4 = Path.Combine(outDir, $"spike-{(useJpeg ? "jpeg" : "png")}-{scale}x-run{run}.mp4");
    var poster = Path.Combine(outDir, $"spike-run{run}-poster.{(useJpeg ? "jpg" : "png")}");

    var psi = new ProcessStartInfo("ffmpeg")
    {
        RedirectStandardInput = true,
        RedirectStandardError = true,
        RedirectStandardOutput = true,
        UseShellExecute = false,
    };
    foreach (var a in new[]
             {
                 "-y", "-hide_banner", "-loglevel", "error",
                 "-f", "image2pipe", "-framerate", fps.ToString(), "-i", "pipe:0",
                 "-vf", "scale=in_range=full:out_range=limited,format=yuv420p",
                 "-c:v", "libx264", "-preset", "veryfast", "-crf", "23",
                 "-profile:v", "main", "-level", "4.0", "-pix_fmt", "yuv420p",
                 "-movflags", "+faststart", "-r", fps.ToString(), mp4,
             })
        psi.ArgumentList.Add(a);
    using var ffmpeg = Process.Start(psi)!;
    var stderrTask = ffmpeg.StandardError.ReadToEndAsync();
    var stdoutTask = ffmpeg.StandardOutput.ReadToEndAsync();
    var stdin = ffmpeg.StandardInput.BaseStream;

    var hashes = new List<string>(frameCount);
    var seekMs = 0L; var shotMs = 0L; var bytes = 0L;
    var total = Stopwatch.StartNew();
    for (var i = 0; i < frameCount; i++)
    {
        var t = i * 1000.0 / fps;
        var sw = Stopwatch.StartNew();
        await page.EvaluateAsync("t => window.__giretra.seek(t)", t);
        seekMs += sw.ElapsedMilliseconds; sw.Restart();
        var png = useJpeg
            ? await page.ScreenshotAsync(new() { Type = ScreenshotType.Jpeg, Quality = quality, Caret = ScreenshotCaret.Hide })
            : await page.ScreenshotAsync(new() { Type = ScreenshotType.Png, Caret = ScreenshotCaret.Hide });
        shotMs += sw.ElapsedMilliseconds;
        bytes += png.Length;
        hashes.Add(Convert.ToHexString(SHA256.HashData(png)));
        keep?.Add(png);
        if (i == 0) await File.WriteAllBytesAsync(poster, png);
        await stdin.WriteAsync(png);
    }
    stdin.Close();
    await ffmpeg.WaitForExitAsync();
    var err = await stderrTask; await stdoutTask;
    total.Stop();

    var size = new FileInfo(mp4).Length;
    Console.WriteLine($"[run {run}] frames={frameCount} total={total.ElapsedMilliseconds} ms " +
        $"({total.ElapsedMilliseconds / (double)frameCount:F1} ms/frame: seek {seekMs / (double)frameCount:F1}, " +
        $"screenshot {shotMs / (double)frameCount:F1}) png avg {bytes / frameCount / 1024} KiB " +
        $"mp4 {size / 1024} KiB ffmpeg exit {ffmpeg.ExitCode}{(err.Length > 0 ? " stderr: " + err.Trim() : "")}");
    await context.CloseAsync();
    return hashes;
}
