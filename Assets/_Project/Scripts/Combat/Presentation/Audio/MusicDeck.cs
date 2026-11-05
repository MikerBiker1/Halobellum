// MusicDeck.cs
// Plays the music: one track at a time, crossfading between two sources when the track changes, plus a stinger that ducks the
// track for a moment. A slot can have several songs, and one is picked at random. Each song remembers where it was, because rounds are short: a player who only ever
// heard the first minute of a song would never hear the rest of it. Plain C# around three AudioSources from the AudioDirector.
// Depends on: AudioLibrary, UnityEngine.

using System.Collections.Generic;
using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class MusicDeck
    {
        private const float StingerFadeSeconds = 0.25f;
        private readonly AudioSource[] _sources;
        private readonly float[] _gains = new float[2];
        private readonly AudioSource _stinger;
        private readonly Dictionary<string, float> _positions = new Dictionary<string, float>();
        private int _current;
        private float _fadeSeconds = 2.5f;
        private float _duck = 1f;
        private float _stingerEnds;
        private float _stingerGain = 1f;
        private bool _stingerStopping;
        private float _clock;
        private float _volume;
        private bool _silent = true;

        public MusicDeck(AudioSource a, AudioSource b, AudioSource stinger)
        {
            _sources = new[] { a, b };
            a.loop = b.loop = true;
            _stinger = stinger;
            _stinger.loop = false;
        }

        /// <summary>The slot of the track playing or fading in; null if none.</summary>
        public string CurrentSlot { get; private set; }
        public string LastStinger { get; private set; }
        /// <summary>The clip on the source the track is fading in on, or null.</summary>
        public AudioClip CurrentClip => _sources[_current].clip;
        public float CurrentGain => _gains[_current];
        /// <summary>How far the track is ducked: 1 is not at all.</summary>
        public float Duck => _duck;
        /// <summary>True from a stinger's start until it ends or is stopped.</summary>
        public bool StingerOn => _clock < _stingerEnds;

        /// <summary>Crossfades to one of a slot's songs, resuming where that song last was. The slot already playing goes on with its song.</summary>
        public void Play(string slot, float fadeSeconds = 2.5f)
        {
            if (slot == CurrentSlot) return;
            RememberPosition();
            CurrentSlot = slot;
            _fadeSeconds = Mathf.Max(0.05f, fadeSeconds);
            AudioClip clip = AudioLibrary.Get(slot).Pick(Random01());
            _silent = clip == null;
            if (_silent) return; // A slot with nothing behind it: the old track just fades out.

            // The new track goes on the quieter source.
            int next = _gains[0] <= _gains[1] ? 0 : 1;
            if (next == _current && _sources[next].isPlaying) next = 1 - next;
            _current = next;
            AudioSource source = _sources[next];
            source.Stop();
            source.clip = clip;
            _gains[next] = 0f;
            source.volume = 0f;
            source.Play();
            if (_positions.TryGetValue(clip.name, out float position) && clip.length > 0f) source.time = position % clip.length;
        }

        /// <summary>Fades the track out and leaves silence, until the next Play. That Play picks a new song, even from the same slot.</summary>
        public void Stop(float fadeSeconds = 1.5f)
        {
            RememberPosition();
            CurrentSlot = null;
            _fadeSeconds = Mathf.Max(0.05f, fadeSeconds);
            _silent = true;
        }

        /// <summary>Plays a one-off stinger, with the track ducked until it has finished.</summary>
        public void PlayStinger(string slot, float delaySeconds = 0f)
        {
            AudioClip clip = AudioLibrary.Get(slot).Pick(Random01());
            LastStinger = slot;
            if (clip == null) return;
            _stinger.Stop();
            _stinger.clip = clip;
            _stingerStopping = false;
            _stingerGain = 1f;
            _stinger.volume = _volume;
            if (delaySeconds > 0f) _stinger.PlayDelayed(delaySeconds);
            else _stinger.Play();
            _stingerEnds = _clock + delaySeconds + clip.length;
        }

        /// <summary>Lets go of a stinger when its screen has been left: gone in a moment, and the track comes back up.</summary>
        public void StopStinger()
        {
            if (!StingerOn) return;
            _stingerEnds = _clock;
            _stingerStopping = true;
        }

        /// <summary>Forgets where every track had got to (a new match starts them from the top).</summary>
        public void ForgetPositions()
        {
            _positions.Clear();
        }

        public void Tick(float dt, float volume)
        {
            _clock += dt;
            _volume = volume;
            float duckTarget = _clock < _stingerEnds ? 0.2f : 1f;
            _duck = Mathf.MoveTowards(_duck, duckTarget, dt / 0.6f);
            if (_stingerStopping)
            {
                _stingerGain = Mathf.MoveTowards(_stingerGain, 0f, dt / StingerFadeSeconds);
                if (_stingerGain <= 0f)
                {
                    _stinger.Stop();
                    _stingerStopping = false;
                }
            }
            _stinger.volume = volume * _stingerGain;

            for (int i = 0; i < 2; i++)
            {
                bool wanted = i == _current && !_silent;
                _gains[i] = Mathf.MoveTowards(_gains[i], wanted ? 1f : 0f, dt / _fadeSeconds);
                AudioSource source = _sources[i];
                // Equal power, so that the crossfade doesn't dip.
                source.volume = Mathf.Sin(_gains[i] * Mathf.PI * 0.5f) * volume * _duck;
                if (_gains[i] <= 0f && !wanted && source.isPlaying) source.Stop();
            }
        }

        private void RememberPosition()
        {
            if (string.IsNullOrEmpty(CurrentSlot)) return;
            AudioSource source = _sources[_current];
            if (source.clip != null && source.isPlaying) _positions[source.clip.name] = source.time;
        }

        private static float Random01() => (Time.frameCount * 0.6180339f) % 1f;
    }
}
