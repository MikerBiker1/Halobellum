// AudioLibrary.cs
// Finds the audio file behind a slot name (see AudioSlots). The owner's file wins: anything in
// Art/Imported/Audio/Resources/AudioOverride named after the slot is used in place of the generated sound in
// Audio/Generated/Resources/GeneratedAudio. A slot can have several files, name_1, name_2 and so on: one is picked at random
// each time. Files are looked for once per slot and kept. A missing file is not an error: the slot is just silent.
// A UI sound the owner gives once, as ui_<sound> (ui_click), serves every faction's set that has no file of its own in the folder.
// The owner's files come as loud as they were made, most of them far louder than the game's mix: each is measured when it is
// found (its loudest 50 ms), and a cue plays it at the loudness it asks for (Gain). The generated sounds are already mixed and
// play as they are.
// Depends on: UnityEngine.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Halobellum.Presentation
{
    /// <summary>The clips behind one slot.</summary>
    public sealed class AudioClipSet
    {
        public readonly string Slot;
        public readonly AudioClip[] Clips;
        /// <summary>True if the clips are the owner's rather than the generated ones.</summary>
        public readonly bool IsOverride;
        /// <summary>Each clip's loudest 50 ms in dB below full scale, or NaN where it isn't measured (the generated ones).</summary>
        public readonly float[] Loudness;
        private int _last = -1;

        public AudioClipSet(string slot, AudioClip[] clips, bool isOverride, float[] loudness = null)
        {
            Slot = slot;
            Clips = clips;
            IsOverride = isOverride;
            Loudness = loudness;
        }

        /// <summary>
        /// How much to turn a clip down (or a little up) so that its loudest moment is at this level, in dB below full scale. 1 for a clip
        /// that isn't measured, or when no level is asked for (NaN).
        /// </summary>
        public float Gain(int index, float target)
        {
            if (Loudness == null || index < 0 || index >= Loudness.Length || float.IsNaN(Loudness[index]) || float.IsNaN(target)) return 1f;
            return Mathf.Clamp(Mathf.Pow(10f, (target - Loudness[index]) / 20f), 0.05f, 1.5f);
        }

        public bool IsEmpty => Clips.Length == 0;

        /// <summary>A clip, not the same one twice in a row if there is a choice. roll is any number from 0 up to 1.</summary>
        public AudioClip Pick(float roll)
        {
            int index = PickIndex(roll);
            return index < 0 ? null : Clips[index];
        }

        /// <summary>As Pick, but the clip's place in Clips (-1 if there is none).</summary>
        public int PickIndex(float roll)
        {
            if (Clips.Length == 0) return -1;
            if (Clips.Length == 1) return 0;
            if (_last < 0)
            {
                _last = Mathf.Clamp((int)(roll * Clips.Length), 0, Clips.Length - 1); // Nothing to avoid yet: any of them.
                return _last;
            }
            int index = Mathf.Clamp((int)(roll * (Clips.Length - 1)), 0, Clips.Length - 2);
            if (index >= _last) index++;
            _last = index;
            return index;
        }

        /// <summary>The average length of the clips, in seconds (0 if none).</summary>
        public float AverageLength
        {
            get
            {
                if (Clips.Length == 0) return 0f;
                float sum = 0f;
                foreach (AudioClip clip in Clips) sum += clip.length;
                return sum / Clips.Length;
            }
        }
    }

    public static class AudioLibrary
    {
        public const string OverrideFolder = "AudioOverride/";
        public const string GeneratedFolder = "GeneratedAudio/";
        private const int MaxVariants = 12;

        private static readonly Dictionary<string, AudioClipSet> Sets = new Dictionary<string, AudioClipSet>();
        private static readonly List<AudioClip> Found = new List<AudioClip>();
        private static readonly AudioClipSet Nothing = new AudioClipSet("", Array.Empty<AudioClip>(), false);

        /// <summary>How a file is loaded by its Resources path; tests can swap this.</summary>
        public static Func<string, AudioClip> Loader = path => Resources.Load<AudioClip>(path);

        /// <summary>The clips of a slot (empty if it has none).</summary>
        public static AudioClipSet Get(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return Nothing;
            if (Sets.TryGetValue(slot, out AudioClipSet set)) return set;
            set = Find(slot, OverrideFolder, true);
            if (set.IsEmpty && SharedUiSlot(slot) is string shared) set = Find(shared, OverrideFolder, true);
            if (set.IsEmpty) set = Find(slot, GeneratedFolder, false);
            Sets[slot] = set;
            return set;
        }

        /// <summary>Forgets what was found, so the next Get looks again.</summary>
        public static void Forget()
        {
            Sets.Clear();
            AudioCatalog.ForgetClips();
        }

        /// <summary>The faction-free name of a faction's UI slot (ui_unsc_click: ui_click), or null for any other slot.</summary>
        public static string SharedUiSlot(string slot)
        {
            if (!slot.StartsWith("ui_", StringComparison.Ordinal)) return null;
            int second = slot.IndexOf('_', 3);
            return second < 0 ? null : "ui_" + slot.Substring(second + 1);
        }

        private static AudioClipSet Find(string slot, string folder, bool isOverride)
        {
            Found.Clear();
            AudioClip single = Loader(folder + slot);
            if (single != null) Found.Add(single);
            for (int i = 1; i <= MaxVariants; i++)
            {
                AudioClip variant = Loader(folder + slot + "_" + i);
                if (variant == null) break;
                Found.Add(variant);
            }
            AudioClip[] clips = Found.ToArray();
            float[] loudness = null;
            if (isOverride && clips.Length > 0 && (slot.StartsWith("sfx_", StringComparison.Ordinal) || slot.StartsWith("ui_", StringComparison.Ordinal))) // Effects only: music and stingers are compressed or streamed.
            {
                loudness = new float[clips.Length];
                for (int i = 0; i < clips.Length; i++) loudness[i] = Loudness(clips[i]);
            }
            return new AudioClipSet(slot, clips, isOverride, loudness);
        }

        /// <summary>A clip's loudest 50 ms (the RMS of all its channels together), in dB below full scale; NaN if its samples can't be read.</summary>
        public static float Loudness(AudioClip clip)
        {
            if (clip == null || clip.samples <= 0 || clip.loadType != AudioClipLoadType.DecompressOnLoad) return float.NaN;
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            int channels = Mathf.Max(1, clip.channels);
            var data = new float[clip.samples * channels];
            try
            {
                if (!clip.GetData(data, 0)) return float.NaN;
            }
            catch (Exception)
            {
                return float.NaN; // A streamed or compressed clip: it plays as it is.
            }
            int window = Mathf.Max(1, clip.frequency / 20) * channels, step = Mathf.Max(channels, window / 2);
            double best = 0.0;
            for (int start = 0; start < data.Length; start += step)
            {
                int end = Mathf.Min(data.Length, start + window);
                double sum = 0.0;
                for (int i = start; i < end; i++) sum += data[i] * data[i];
                best = Math.Max(best, sum / Mathf.Max(1, end - start));
                if (end == data.Length) break;
            }
            return best > 0.0 ? (float)(10.0 * Math.Log10(best)) : float.NaN;
        }
    }
}
