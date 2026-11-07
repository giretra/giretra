using System.Buffers.Binary;

namespace Giretra.Web.Services.Rendering;

/// <summary>A sound to play at a time of the clip; names are the app's sound asset names.</summary>
public sealed record SoundEvent(double TimeMs, string Sound);

/// <summary>
/// Soundtrack of a clip: the app's short card samples added together (plain PCM addition) at
/// the timeline's event times. Deterministic, no DSP library.
/// </summary>
public static class ClipAudio
{
    public const int SampleRate = 44100;
    public const int Channels = 2;

    /// <summary>
    /// Mixes <paramref name="events"/> into one interleaved 16-bit stereo buffer of exactly
    /// <paramref name="durationMs"/>. Samples are interleaved stereo at <see cref="SampleRate"/>;
    /// unknown sounds and events outside the clip are ignored; the result is clipped, not
    /// normalised, so a lone card sounds the same in every clip.
    /// </summary>
    public static short[] Mix(IEnumerable<SoundEvent> events, IReadOnlyDictionary<string, short[]> samples, int durationMs, float gain = 1f)
    {
        var frames = (int)Math.Ceiling(Math.Max(0, durationMs) * (double)SampleRate / 1000.0);
        var acc = new int[frames * Channels];

        foreach (var e in events)
        {
            if (e.TimeMs < 0 || !samples.TryGetValue(e.Sound, out var sample))
                continue;
            var start = (int)Math.Round(e.TimeMs * SampleRate / 1000.0) * Channels;
            var count = Math.Min(sample.Length, acc.Length - start);
            for (var i = 0; i < count; i++)
                acc[start + i] += sample[i];
        }

        var pcm = new short[acc.Length];
        for (var i = 0; i < acc.Length; i++)
            pcm[i] = (short)Math.Clamp((int)Math.Round(acc[i] * gain), short.MinValue, short.MaxValue);
        return pcm;
    }

    /// <summary>Writes a canonical 44-byte-header PCM WAV.</summary>
    public static void WriteWav(Stream stream, short[] pcm, int sampleRate = SampleRate, int channels = Channels)
    {
        var dataBytes = pcm.Length * 2;
        Span<byte> header = stackalloc byte[44];
        "RIFF"u8.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], 36 + dataBytes);
        "WAVE"u8.CopyTo(header[8..]);
        "fmt "u8.CopyTo(header[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(header[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(header[20..], 1); // PCM
        BinaryPrimitives.WriteInt16LittleEndian(header[22..], (short)channels);
        BinaryPrimitives.WriteInt32LittleEndian(header[24..], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(header[28..], sampleRate * channels * 2);
        BinaryPrimitives.WriteInt16LittleEndian(header[32..], (short)(channels * 2));
        BinaryPrimitives.WriteInt16LittleEndian(header[34..], 16);
        "data"u8.CopyTo(header[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(header[40..], dataBytes);
        stream.Write(header);

        var buffer = new byte[Math.Min(dataBytes, 64 * 1024)];
        var offset = 0;
        while (offset < pcm.Length)
        {
            var n = Math.Min(buffer.Length / 2, pcm.Length - offset);
            for (var i = 0; i < n; i++)
                BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(i * 2), pcm[offset + i]);
            stream.Write(buffer, 0, n * 2);
            offset += n;
        }
    }

    /// <summary>Little-endian s16le bytes (ffmpeg's raw output) to samples.</summary>
    public static short[] FromS16Le(ReadOnlySpan<byte> bytes)
    {
        var pcm = new short[bytes.Length / 2];
        for (var i = 0; i < pcm.Length; i++)
            pcm[i] = BinaryPrimitives.ReadInt16LittleEndian(bytes[(i * 2)..]);
        return pcm;
    }
}
