extern alias UnityCore;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Audio;
using SpanWrapper = UnityCore::UnityEngine.Bindings.ManagedSpanWrapper;
#if BEPINEX
using GameSoundManager = global::Refactor.Sound.SoundManager;
#else
using GameSoundManager = Il2CppRefactor.Sound.SoundManager;
#endif

namespace DungeonSettlersDelvers.Core;

// Core's own one-shot sounds (CoreSoundRules; today Lifebloom's, LifebloomSoundRules): WAV files in <package>/Core/Sounds,
// played through the game's sound effect mixer group, so the game's effect volume, master volume and background mute apply.
// A sound at a unit fades with its distance from the camera. Every
// sound holds while the game is paused. A missing or broken file only means silence, logged once. The clip is built the way
// Frieren's Zoltraak sounds build theirs (ZoltraakSound): the interop's AudioClip.Create and SetData need span methods the
// game's build stripped, so name and samples go to the native calls as pinned spans.
internal static class CoreSound
{
    private sealed class Voice
    {
        internal AudioSource Source;
        internal float Volume;
        internal Vector2 Position;
        internal bool Paused;
    }

    private static readonly Dictionary<string, AudioClip> Clips = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Broken = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<Voice> Voices = new();
    private static object runner;
    private static float runnerBeat;

    // At the unit's place in the world; silent when it has none.
    internal static void PlayAt(UnitEntity unit, string file, float volume)
    {
        try
        {
            var inMap = CoreGame.InMap;
            if (unit == null || inMap == null) return;
            var world = inMap.GetWorldPosition(unit.Guid);
            Play(file, new Vector2(world.x, world.y), volume);
        }
        catch (Exception ex) { Failed(file, ex); }
    }

    // True when the file loads; used by the self-test.
    internal static bool Ready(string file) => Clip(file) != null;

    private static void Play(string file, Vector2 position, float volume)
    {
        var clip = Clip(file);
        if (!clip) return;
        try
        {
            var holder = new GameObject("Delvers_Core_Sound");
            var source = holder.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = false;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            var mixer = Mixer();
            if (mixer) source.outputAudioMixerGroup = mixer;
            var voice = new Voice { Source = source, Volume = volume, Position = position };
            source.volume = Level(voice);
            source.Play();
            Voices.Add(voice);
            EnsureRunner();
            DelversHost.Info(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "CORE_SOUND_PLAY file={0} level={1:0.00}", file, source.volume));
        }
        catch (Exception ex) { Failed(file, ex); }
    }

    private static void Failed(string file, Exception ex)
    {
        if (Broken.Add("play:" + file)) DelversHost.Warning($"CORE_SOUND_FAILED file={file}: {ex.Message}");
    }

    private static string PathOf(string file) => Path.Combine(CoreAssets.Folder, CoreSoundRules.Folder, file);

    private static AudioClip Clip(string file)
    {
        if (string.IsNullOrEmpty(file) || Broken.Contains(file)) return null;
        if (Clips.TryGetValue(file, out var cached) && cached) return cached;
        try
        {
            var wav = CoreSoundRules.ReadWav(File.ReadAllBytes(PathOf(file)));
            var clip = NewClip("Delvers_Core_" + Path.GetFileNameWithoutExtension(file), wav.Frames, wav.Channels, wav.Rate);
            if (!Fill(clip, wav.Samples)) throw new InvalidOperationException("SetData abgelehnt");
            clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Clips[file] = clip;
            DelversHost.Info(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "CORE_SOUND_READY file={0} seconds={1:0.00} channels={2} rate={3}", file, wav.Frames / (float)wav.Rate, wav.Channels, wav.Rate));
            return clip;
        }
        catch (Exception ex)
        {
            Broken.Add(file);
            DelversHost.Warning($"CORE_SOUND_DISABLED file={file}: {ex.Message}");
            return null;
        }
    }

    private static AudioClip NewClip(string name, int frames, int channels, int rate)
    {
        var clip = AudioClip.Construct_Internal();
        if (!clip) throw new InvalidOperationException("Construct_Internal lieferte keinen Clip");
        Pinned(name.ToCharArray(), (begin, length) =>
        {
            var raw = new RawSpan { Begin = begin, Length = length };
            AudioClip.CreateUserSound_Injected(clip.m_CachedPtr,
                ref System.Runtime.CompilerServices.Unsafe.As<RawSpan, SpanWrapper>(ref raw), frames, channels, rate, false);
            return true;
        });
        return clip;
    }

    private static bool Fill(AudioClip clip, float[] samples)
        => Pinned(samples, (begin, length) =>
        {
            var raw = new RawSpan { Begin = begin, Length = length };
            return AudioClip.SetData_Injected(clip.m_CachedPtr,
                ref System.Runtime.CompilerServices.Unsafe.As<RawSpan, SpanWrapper>(ref raw), 0);
        });

    private static bool Pinned(Array data, Func<IntPtr, int, bool> use)
    {
        var pin = GCHandle.Alloc(data, GCHandleType.Pinned);
        try { return use(pin.AddrOfPinnedObject(), data.Length); }
        finally { pin.Free(); }
    }

    // The wrapper's fields are read-only and its constructor is a native call, so the span is written into a struct of the
    // same explicit layout (begin at 0, length at 8) and handed over as the wrapper.
    [StructLayout(LayoutKind.Explicit)]
    private struct RawSpan
    {
        [FieldOffset(0)] internal IntPtr Begin;
        [FieldOffset(8)] internal int Length;
    }

    private static AudioMixerGroup Mixer()
    {
        try { return GameSoundManager.Instance?._oneTimeSFXSoundHandler?._sfxAudioMixerGroup; }
        catch (Exception) { return null; }
    }

    private static float Level(Voice voice)
        => voice.Volume * CoreSoundRules.DistanceLevel(CameraDistance(voice.Position));

    private static float CameraDistance(Vector2 position)
    {
        var camera = Camera.main;
        if (!camera) return 0f;
        var eye = camera.transform.position;
        return Vector2.Distance(new Vector2(eye.x, eye.y), position);
    }

    // The campaign's speed multiplier; 1 when it cannot be read.
    private static float Speed()
    {
        try
        {
            var setting = CoreGame.Flow?._campaignSetting?.GameSpeedSetting;
            return setting == null ? 1f : setting.GetMultiplier(setting.GetSpeed());
        }
        catch (Exception) { return 1f; }
    }

    private static void EnsureRunner()
    {
        if (CoreSoundRules.RunnerStale(runner != null, Time.unscaledTime, runnerBeat))
        {
            DelversHost.Warning("CORE_SOUND_RUNNER_RESTART");
            runner = null;
        }
        if (runner != null) return;
        runnerBeat = Time.unscaledTime;
        runner = DelversHost.StartCoroutine(Run());
    }

    // Holds every voice while the game is paused, follows the camera and removes the ended ones.
    private static IEnumerator Run()
    {
        try
        {
            while (Voices.Count > 0)
            {
                runnerBeat = Time.unscaledTime;
                var paused = CoreSoundRules.Paused(Speed());
                for (var i = Voices.Count - 1; i >= 0; i--)
                {
                    var voice = Voices[i];
                    var source = voice.Source;
                    if (!source) { Voices.RemoveAt(i); continue; }
                    if (paused != voice.Paused)
                    {
                        if (paused) source.Pause(); else source.UnPause();
                        voice.Paused = paused;
                    }
                    source.volume = Level(voice);
                    if (paused || source.isPlaying) continue;
                    UnityEngine.Object.Destroy(source.gameObject);
                    Voices.RemoveAt(i);
                }
                yield return null;
            }
        }
        finally
        {
            runner = null;
        }
    }
}
