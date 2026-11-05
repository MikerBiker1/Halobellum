// AudioLibrary.cs
// Finds the audio file behind a slot name (see AudioSlots). The owner's file wins: anything in
// Art/Imported/Audio/Resources/AudioOverride named after the slot is used in place of the generated sound in
// Audio/Generated/Resources/GeneratedAudio. A slot can have several files, name_1, name_2 and so on: one is picked at random
// each time. Files are looked for once per slot and kept. A missing file is not an error: the slot is just silent.
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
        private int _last = -1;

        public AudioClipSet(string slot, AudioClip[] clips, bool isOverride)
        {
            Slot = slot;
            Clips = clips;
            IsOverride = isOverride;
        }

        public bool IsEmpty => Clips.Length == 0;

        /// <summary>A clip, not the same one twice in a row if there is a choice. roll is any number from 0 up to 1.</summary>
        public AudioClip Pick(float roll)
        {
            if (Clips.Length == 0) return null;
            if (Clips.Length == 1) return Clips[0];
            if (_last < 0)
            {
                _last = Mathf.Clamp((int)(roll * Clips.Length), 0, Clips.Length - 1); // Nothing to avoid yet: any of them.
                return Clips[_last];
            }
            int index = Mathf.Clamp((int)(roll * (Clips.Length - 1)), 0, Clips.Length - 2);
            if (index >= _last) index++;
            _last = index;
            return Clips[index];
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
            return new AudioClipSet(slot, Found.ToArray(), isOverride);
        }
    }
}
