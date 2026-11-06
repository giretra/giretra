using System.Diagnostics;

namespace Giretra.Web.Services.Rendering;

/// <summary>
/// One ffmpeg process encoding an image2pipe stream (JPEG or PNG frames on stdin) to H.264
/// MP4, optionally muxing a WAV soundtrack. stderr is drained asynchronously (ffmpeg blocks
/// otherwise); the process is killed on dispose if still running.
/// </summary>
public sealed class FfmpegEncoder : IAsyncDisposable
{
    private readonly Process _process;
    private readonly Task<string> _stderr;
    private readonly Stream _stdin;
    private bool _finished;

    private FfmpegEncoder(Process process)
    {
        _process = process;
        _stderr = process.StandardError.ReadToEndAsync();
        _ = process.StandardOutput.ReadToEndAsync();
        _stdin = process.StandardInput.BaseStream;
    }

    /// <summary>
    /// Phone-friendly H.264 Main 4.0, limited-range yuv420p (JPEG frames are full range),
    /// faststart; AAC 96k when a soundtrack is given, cut to the video's length.
    /// </summary>
    public static IReadOnlyList<string> BuildArguments(int fps, int crf, string? audioPath, string outputPath)
    {
        var args = new List<string>
        {
            "-y", "-hide_banner", "-loglevel", "error", "-nostdin",
            "-f", "image2pipe", "-framerate", fps.ToString(), "-i", "pipe:0",
        };
        if (audioPath != null)
            args.AddRange(["-i", audioPath]);
        args.AddRange(
        [
            "-vf", "scale=in_range=full:out_range=limited,format=yuv420p",
            "-c:v", "libx264", "-preset", "veryfast", "-crf", crf.ToString(),
            "-profile:v", "main", "-level", "4.0", "-pix_fmt", "yuv420p",
        ]);
        if (audioPath != null)
            args.AddRange(["-c:a", "aac", "-b:a", "96k", "-ar", ClipAudio.SampleRate.ToString(), "-ac", ClipAudio.Channels.ToString(), "-shortest"]);
        args.AddRange(["-movflags", "+faststart", "-r", fps.ToString(), "-f", "mp4", outputPath]);
        return args;
    }

    public static FfmpegEncoder Start(string ffmpegPath, IReadOnlyList<string> arguments)
    {
        var psi = new ProcessStartInfo(ffmpegPath)
        {
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var a in arguments)
            psi.ArgumentList.Add(a);
        var process = Process.Start(psi) ?? throw new InvalidOperationException("ffmpeg did not start");
        return new FfmpegEncoder(process);
    }

    public ValueTask WriteFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct) => _stdin.WriteAsync(frame, ct);

    /// <summary>Closes the input and waits for the file to be finalised; throws with ffmpeg's stderr on failure.</summary>
    public async Task FinishAsync(CancellationToken ct)
    {
        _finished = true;
        _stdin.Close();
        await _process.WaitForExitAsync(ct);
        var err = await _stderr;
        if (_process.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg exited with {_process.ExitCode}: {err.Trim()}");
    }

    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            try { _process.Kill(entireProcessTree: true); } catch { /* already gone */ }
        }
        if (!_finished)
        {
            try { await _stderr; } catch { /* ignore */ }
        }
        _process.Dispose();
    }
}
