using System.Text.Json;
using Giretra.Web.Services.Rendering;

namespace Giretra.Web.Tests.Services.Rendering;

public sealed class ClipAudioTests
{
    private static readonly Dictionary<string, short[]> Samples = new()
    {
        // 2 stereo frames each: L R L R
        ["click"] = [1000, 1000, 2000, 2000],
        ["thud"] = [-500, -500, -500, -500],
    };

    [Fact]
    public void Mix_PlacesSamplesAtEventTimes_AndSumsOverlaps()
    {
        // 1 ms at 44.1 kHz = 44.1 frames; t=0 and t=1 ms → frame 0 and frame 44.
        var pcm = ClipAudio.Mix([new SoundEvent(0, "click"), new SoundEvent(1, "thud"), new SoundEvent(0, "thud")], Samples, durationMs: 10);

        Assert.Equal(10 * 44.1 * 2, pcm.Length, 0); // ceil(441) frames × 2 channels
        Assert.Equal(500, pcm[0]);     // click + thud at frame 0
        Assert.Equal(1500, pcm[2]);    // second frame of click + thud
        Assert.Equal(0, pcm[4]);       // silence
        Assert.Equal(-500, pcm[44 * 2]); // thud at 1 ms
    }

    [Fact]
    public void Mix_ClipsInsteadOfWrapping_AndAppliesGain()
    {
        var loud = new Dictionary<string, short[]> { ["max"] = [short.MaxValue, short.MaxValue] };

        var sum = ClipAudio.Mix([new SoundEvent(0, "max"), new SoundEvent(0, "max")], loud, 1);
        var half = ClipAudio.Mix([new SoundEvent(0, "max")], loud, 1, gain: 0.5f);

        Assert.Equal(short.MaxValue, sum[0]);
        Assert.Equal(16384, half[0]);
    }

    [Fact]
    public void Mix_IgnoresUnknownSounds_NegativeTimes_AndTailsPastTheEnd()
    {
        var pcm = ClipAudio.Mix(
            [new SoundEvent(0, "missing"), new SoundEvent(-5, "click"), new SoundEvent(0.99, "click"), new SoundEvent(50, "click")],
            Samples, durationMs: 1);

        Assert.Equal(90, pcm.Length); // 45 frames
        Assert.All(pcm.Take(86), v => Assert.Equal(0, v));
        Assert.Equal(1000, pcm[88]); // frame 44 fits one frame of the click, the second is cut
    }

    [Fact]
    public void WriteWav_ProducesCanonicalHeader()
    {
        short[] pcm = [1, -1, 2, -2];
        using var ms = new MemoryStream();

        ClipAudio.WriteWav(ms, pcm);
        var bytes = ms.ToArray();

        Assert.Equal(44 + 8, bytes.Length);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal(36 + 8, BitConverter.ToInt32(bytes, 4));
        Assert.Equal("WAVEfmt ", System.Text.Encoding.ASCII.GetString(bytes, 8, 8));
        Assert.Equal(1, BitConverter.ToInt16(bytes, 20));          // PCM
        Assert.Equal(2, BitConverter.ToInt16(bytes, 22));          // stereo
        Assert.Equal(44100, BitConverter.ToInt32(bytes, 24));
        Assert.Equal(44100 * 4, BitConverter.ToInt32(bytes, 28));  // byte rate
        Assert.Equal(16, BitConverter.ToInt16(bytes, 34));
        Assert.Equal("data", System.Text.Encoding.ASCII.GetString(bytes, 36, 4));
        Assert.Equal(8, BitConverter.ToInt32(bytes, 40));
        Assert.Equal(pcm, ClipAudio.FromS16Le(bytes.AsSpan(44)));
    }

    [Fact]
    public void FfmpegArguments_MuxAudioOnlyWhenGiven_AndForceMp4ForTmpOutput()
    {
        var silent = FfmpegEncoder.BuildArguments(30, 23, null, "/x/a.mp4.tmp");
        var sound = FfmpegEncoder.BuildArguments(25, 20, "/x/a.wav", "/x/a.mp4.tmp");

        Assert.DoesNotContain("-shortest", silent);
        Assert.DoesNotContain("aac", silent);
        Assert.Contains("-shortest", sound);
        Assert.Equal("/x/a.wav", sound[sound.ToList().IndexOf("-i", sound.ToList().IndexOf("pipe:0")) + 1]);
        foreach (var args in new[] { silent, sound })
        {
            Assert.Equal("/x/a.mp4.tmp", args[^1]);
            Assert.Equal("mp4", args[^2]);
            Assert.Contains("+faststart", args);
            Assert.Contains("scale=in_range=full:out_range=limited,format=yuv420p", args);
            Assert.Contains("main", args);
        }
        Assert.Equal("25", sound[sound.ToList().IndexOf("-framerate") + 1]);
        Assert.Equal("20", sound[sound.ToList().IndexOf("-crf") + 1]);
    }

    [Fact]
    public void ParseEvents_ReadsTheExposedShape_AndSkipsGarbage()
    {
        using var doc = JsonDocument.Parse("""[{"t":1500,"sound":"card_played"},{"t":"x","sound":"a"},{"sound":"b"},7,{"t":2200.5,"sound":"card_folding_started"}]""");

        var events = ClipRenderer.ParseEvents(doc.RootElement);

        Assert.Equal([new SoundEvent(1500, "card_played"), new SoundEvent(2200.5, "card_folding_started")], events);
        Assert.Empty(ClipRenderer.ParseEvents(null));
        Assert.Empty(ClipRenderer.ParseEvents(JsonDocument.Parse("{}").RootElement));
    }
}
