using System.Text;

namespace DungeonSettlersDelvers.Core;

// Core's own sounds (Lifebloom's cast, bloom and charge; CoreSound plays them): plain PCM WAV files in
// the package's Core\Sounds folder, 16 or 24 bit, read into interleaved floats from -1 to 1. The same reading as Frieren's
// Zoltraak sounds (ZoltraakWavRules), kept here so Core needs no pack.
internal static class CoreSoundRules
{
    internal const string Folder = "Sounds";

    // Far from the camera a sound fades out like an off-screen fight: full up to NearDistance, silent from FarDistance
    // (world units, as Zoltraak's).
    internal const float NearDistance = 10f, FarDistance = 30f;

    internal static float DistanceLevel(float distance)
        => 1f - Math.Min(1f, Math.Max(0f, (distance - NearDistance) / (FarDistance - NearDistance)));

    // The campaign's speed multiplier; at 0 (pause) every sound holds and goes on with the game.
    internal static bool Paused(float speed) => speed <= 0f;

    // The one sound loop notes a heartbeat each frame. A loop stopped from outside (a scene change) never reaches its end, so a
    // known loop without a heartbeat for StaleSeconds counts as gone and a new one starts (as ZoltraakLoopRules).
    internal const float StaleSeconds = 2f;

    internal static bool RunnerStale(bool known, float now, float beat) => known && now - beat > StaleSeconds;

    internal static CoreWav ReadWav(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 12 || Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF" || Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE")
            throw new InvalidDataException("keine WAV-Datei");
        int pos = 12, format = 0, channels = 0, rate = 0, bits = 0, dataPos = -1, dataLength = 0;
        while (pos + 8 <= bytes.Length)
        {
            var id = Encoding.ASCII.GetString(bytes, pos, 4);
            var length = BitConverter.ToInt32(bytes, pos + 4);
            if (length < 0) break;
            if (id == "fmt " && pos + 24 <= bytes.Length)
            {
                format = BitConverter.ToInt16(bytes, pos + 8);
                channels = BitConverter.ToInt16(bytes, pos + 10);
                rate = BitConverter.ToInt32(bytes, pos + 12);
                bits = BitConverter.ToInt16(bytes, pos + 22);
            }
            else if (id == "data")
            {
                dataPos = pos + 8;
                dataLength = Math.Min(length, bytes.Length - dataPos);
                break;
            }
            pos += 8 + length + (length & 1);
        }
        if (format != 1 || channels < 1 || rate < 1 || (bits != 16 && bits != 24))
            throw new InvalidDataException($"nur PCM mit 16 oder 24 Bit (Format {format}, {bits} Bit)");
        if (dataPos < 0) throw new InvalidDataException("WAV ohne Daten");
        var size = bits / 8;
        var frames = dataLength / (size * channels);
        if (frames < 1) throw new InvalidDataException("WAV ohne Daten");
        var samples = new float[frames * channels];
        for (var i = 0; i < samples.Length; i++)
        {
            var p = dataPos + i * size;
            samples[i] = bits == 16 ? BitConverter.ToInt16(bytes, p) / 32768f
                : (bytes[p] | (bytes[p + 1] << 8) | ((sbyte)bytes[p + 2] << 16)) / 8388608f;
        }
        return new CoreWav(channels, rate, frames, samples);
    }
}

internal sealed class CoreWav
{
    internal CoreWav(int channels, int rate, int frames, float[] samples)
    {
        Channels = channels;
        Rate = rate;
        Frames = frames;
        Samples = samples;
    }

    internal int Channels { get; }
    internal int Rate { get; }
    internal int Frames { get; }
    internal float[] Samples { get; }
}
