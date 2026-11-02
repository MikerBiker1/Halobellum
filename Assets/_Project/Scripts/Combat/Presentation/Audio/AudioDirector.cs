// AudioDirector.cs
// All the sound of the game comes out of here (M10). Made at run time before the first scene, it survives scene loads. It keeps
// a fixed pool of AudioSources: nothing is created during a battle. The camera is high and far, so sounds are not placed in 3D: they
// are 2D, panned by where they are on screen, a little quieter off screen. Each kind of sound has a limit on how many may play at
// once (AudioCatalog) and a random pitch, so that 200 rifles neither deafen nor cost much. Battle sounds pause with the battle or the game;
// music and UI sounds do not. The volumes are read from PlayerPrefs every half second ("Volume.Master", "Volume.Music", "Volume.Sfx",
// "Volume.Ui"), so the settings screen only has to write them.
// Clicks are found without touching any HUD code: an IMGUI button takes "hot control" when the mouse goes down on it, and that is watched here.
// It goes silent by itself when the game runs for tests (batch mode, -autoplay and the like) unless -audioOn is given, so that test
// runs make no noise in the room.
// It also works out what to play for each scene; BattleAudio, MatchAudio and SandboxAudio listen to their scenes and call in here.
// Depends on: AudioCatalog, AudioLibrary, AudioSlots, MusicDeck, VoiceLimiter, BattleAudio, MatchAudio, SandboxAudio, UnityEngine.

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Halobellum.Presentation
{
    /// <summary>The way the rest of the game asks for sound. Safe to call when there is no director (tests in edit mode).</summary>
    public static class GameAudio
    {
        public static void Ui(UiSound sound, float volume = 1f) => AudioDirector.Instance?.PlayUi(sound, volume);

        public static void Sfx(SfxCue cue) => AudioDirector.Instance?.PlaySfx(cue);

        public static void Sfx(SfxCue cue, Vector3 world) => AudioDirector.Instance?.PlaySfx(cue, world);
    }

    public sealed class AudioDirector : MonoBehaviour
    {
        private const int SfxVoices = 20, UiVoices = 5;
        private const float PollSeconds = 0.5f;
        /// <summary>All effects together, so that they sit over the music with room to spare.</summary>
        private const float SfxTrim = 0.7f;
        private const int CrowdStarts = 5;
        public const string KeyMaster = "Volume.Master", KeyMusic = "Volume.Music", KeySfx = "Volume.Sfx", KeyUi = "Volume.Ui";
        public const float DefaultMaster = 1f, DefaultMusic = 0.6f, DefaultSfx = 1f, DefaultUi = 1f;

        public static AudioDirector Instance { get; private set; }

        /// <summary>Raised with the slot of every sound that really starts. For tests.</summary>
        public event Action<string> SlotPlayed;
        /// <summary>How many effects were asked for but not played, being over their limit.</summary>
        public int DroppedVoices { get; private set; }

        /// <summary>Which faction's UI sounds play; null before a side is chosen.</summary>
        public string UiFaction { get; set; }

        public MusicDeck Music { get; private set; }
        /// <summary>The volumes last read from the settings.</summary>
        public float VolumeMaster { get; private set; } = DefaultMaster;
        public float VolumeMusic { get; private set; } = DefaultMusic;
        public float VolumeSfx { get; private set; } = DefaultSfx;
        public float VolumeUi { get; private set; } = DefaultUi;
        /// <summary>True while the battle is paused, and the battle's sounds with it.</summary>
        public bool BattlePaused { get; private set; }
        public bool IsMuted { get; private set; }
        public int ActiveSfxVoices { get; private set; }
        /// <summary>True while an engine or hover hum is really sounding (not held by a pause).</summary>
        public bool AnyHumPlaying => _engineHum.isPlaying || _hoverHum.isPlaying;

        private sealed class Voice
        {
            public AudioSource Source;
            public int Group = -1;
            public int Priority;
            public float EndsAt;
            public float Started;
            public float Volume;
        }

        private readonly VoiceLimiter _limiter = new VoiceLimiter((int)SfxGroup.Count);
        private readonly Voice[] _sfx = new Voice[SfxVoices];
        private readonly AudioSource[] _ui = new AudioSource[UiVoices];
        private AudioSource _engineHum, _hoverHum;
        private float _engineTarget, _hoverTarget, _engineLevel, _hoverLevel;
        private int _nextUi;
        private float _battleClock;
        private float _nextPoll;
        private int _lastHotControl;
        private Camera _camera;
        private uint _random = 2463534242u;
        private AudioListener _ownListener;
        private bool _checkListener;
        private bool _notingCues;
        private string _notedMusic, _notedStinger;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (Instance != null) return;
            var go = new GameObject("Audio Director");
            DontDestroyOnLoad(go);
            go.AddComponent<AudioDirector>();
        }

        /// <summary>The test tools that run the game by themselves: each of these makes a run silent. A tool's flag belongs here as soon as the tool exists (AudioTests reads them from the code).</summary>
        public static readonly string[] TestRunFlags =
        {
            GameFlow.AutoplayFlag, SandboxAutoplay.Flag, GameFlow.GalleryFlag, CrowdBenchmark.Flag, GameFlow.BenchmarkFlag, GameFlow.MenuDemoFlag, GameFlow.CardsDemoFlag, "-terrainGallery",
        };

        /// <summary>True if this run should make no sound in the room: a test or a batch run, unless asked with -audioOn.</summary>
        public static bool ShouldMute(string[] commandLine, bool batchMode)
        {
            if (Array.IndexOf(commandLine, "-audioOn") >= 0) return false;
            if (Array.IndexOf(commandLine, AudioCapture.Flag) >= 0) return false; // The capture silences the room itself.
            if (batchMode) return true;
            foreach (string flag in TestRunFlags)
            {
                if (Array.IndexOf(commandLine, flag) >= 0) return true;
            }
            return false;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            AudioCatalog.SetLimits(_limiter);
            IsMuted = ShouldMute(Environment.GetCommandLineArgs(), Application.isBatchMode);
            if (IsMuted)
            {
                AudioListener.volume = 0f;
                Debug.Log("Audio: a test run, so the room stays silent (-audioOn to hear it).");
            }

            for (int i = 0; i < SfxVoices; i++)
            {
                _sfx[i] = new Voice { Source = NewSource(128) };
            }
            for (int i = 0; i < UiVoices; i++) _ui[i] = NewSource(16);
            _engineHum = NewSource(200);
            _hoverHum = NewSource(200);
            _engineHum.loop = _hoverHum.loop = true;
            Music = new MusicDeck(NewSource(0), NewSource(0), NewSource(8));
            _ownListener = gameObject.AddComponent<AudioListener>();
            _ownListener.enabled = false;

            ReadVolumes();
            Preload();
            SceneManager.sceneLoaded += OnSceneLoaded;
            BattleFactions.Changed += OnFactionsChanged;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            BattleFactions.Changed -= OnFactionsChanged;
            if (Instance == this) Instance = null;
        }

        private AudioSource NewSource(int priority)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.priority = priority;
            source.dopplerLevel = 0f;
            source.reverbZoneMix = 0f;
            return source;
        }

        /// <summary>Loads every effect and UI sound now, so that the first shot of a battle doesn't stutter. Music is streamed as it plays.</summary>
        private static void Preload()
        {
            foreach (SfxCue cue in AudioCatalog.Cues) _ = cue.Clips;
            foreach (string slot in AudioSlots.MusicAndUiSlots())
            {
                if (slot.StartsWith("ui_", StringComparison.Ordinal)) AudioLibrary.Get(slot);
            }
        }

        // ------------------------------------------------------------------ scenes

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _camera = null;
            _checkListener = true;
            UiAudio.ForgetPointer();
            StopBattleSounds();
            Music.StopStinger(); // It belongs to the screen that was left.
            HandleScene(scene.name);
        }

        /// <summary>Connects to what is in a scene and decides on its music.</summary>
        public void HandleScene(string sceneName)
        {
            if (sceneName == AudioSlots.MainMenuScene)
            {
                UiFaction = null;
                Music.Play(AudioSlots.MainMenuMusic);
                return;
            }

            var battle = FindAnyObjectByType<BattleController>();
            if (battle == null)
            {
                // A screen of its own, such as settings: whatever is playing goes on, or the menu's music if nothing is.
                if (string.IsNullOrEmpty(Music.CurrentSlot)) Music.Play(AudioSlots.MainMenuMusic);
                return;
            }
            if (battle.GetComponent<BattleAudio>() == null) battle.gameObject.AddComponent<BattleAudio>();
            var match = battle.GetComponent<MatchController>();
            var sandbox = battle.GetComponent<SandboxController>();
            if (match != null)
            {
                if (match.GetComponent<MatchAudio>() == null) match.gameObject.AddComponent<MatchAudio>();
            }
            else if (sandbox != null)
            {
                if (sandbox.GetComponent<SandboxAudio>() == null) sandbox.gameObject.AddComponent<SandboxAudio>();
            }
            else
            {
                // The plain Battle scene just plays a battle.
                UiFaction = BattleFactions.TeamA;
                Music.Play(AudioSlots.Music(BattleFactions.TeamA, MusicMood.Battle));
            }
        }

        private void OnFactionsChanged()
        {
            if (UiFaction != null) UiFaction = BattleFactions.TeamA;
        }

        /// <summary>If the scene has no listener, sounds still need one: this object's own.</summary>
        private void EnsureListener()
        {
            _checkListener = false;
            bool other = false;
            foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                if (listener != _ownListener && listener.isActiveAndEnabled) other = true;
            }
            _ownListener.enabled = !other;
            AudioCapture.AttachToActiveListener(other ? null : _ownListener);
            if (AudioCapture.IsRecording && !_notingCues)
            {
                _notingCues = true;
                SlotPlayed += AudioCapture.Note;
            }
        }

        // ------------------------------------------------------------------ playing

        /// <param name="volume">Of its own loudness, for something done so often that it should sit lower.</param>
        public void PlayUi(UiSound sound, float volume = 1f)
        {
            string slot = AudioSlots.Ui(UiFaction, sound);
            AudioClip clip = AudioLibrary.Get(slot).Pick(NextRandom());
            if (clip == null) return;
            AudioSource source = _ui[_nextUi];
            _nextUi = (_nextUi + 1) % _ui.Length;
            source.Stop();
            source.clip = clip;
            source.pitch = 1f;
            source.volume = VolumeMaster * VolumeUi * volume;
            source.Play();
            SlotPlayed?.Invoke(slot);
        }

        /// <summary>An effect with no place: centred, at its own loudness.</summary>
        public void PlaySfx(SfxCue cue) => Begin(cue, false, default);

        /// <summary>An effect from a place in the world: panned by where it is on screen.</summary>
        public void PlaySfx(SfxCue cue, Vector3 world) => Begin(cue, true, world);

        private void Begin(SfxCue cue, bool placed, Vector3 world)
        {
            if (cue == null || BattlePaused) return;
            if (!_limiter.TryStart((int)cue.Group, _battleClock))
            {
                DroppedVoices++; // Most shots of a big battle end here, before any more work.
                return;
            }
            AudioClip clip = cue.Clips.Pick(NextRandom());
            Voice voice = clip != null ? FindVoice(cue.Priority) : null;
            if (voice == null)
            {
                _limiter.Release((int)cue.Group);
                DroppedVoices++;
                return;
            }
            if (voice.Group >= 0)
            {
                _limiter.Release(voice.Group); // A voice taken from something that mattered less.
                ActiveSfxVoices--;
            }

            float pan = 0f, gain = 1f;
            if (placed) ScreenPlace(world, out pan, out gain);
            float pitch = 1f + cue.PitchSpread * (NextRandom() * 2f - 1f);
            voice.Group = (int)cue.Group;
            voice.Priority = cue.Priority;
            voice.Started = _battleClock;
            voice.EndsAt = _battleClock + clip.length / Mathf.Max(0.1f, pitch);
            // In the thick of it each new sound is a little quieter, so that the sum stays under the ceiling: a recorded big battle reached 0 dB before this.
            float crowd = ActiveSfxVoices <= CrowdStarts ? 1f : Mathf.Sqrt((float)CrowdStarts / ActiveSfxVoices);
            voice.Volume = cue.Volume * gain * crowd * SfxTrim;
            AudioSource source = voice.Source;
            source.Stop();
            source.clip = clip;
            source.pitch = pitch;
            source.panStereo = pan;
            source.volume = voice.Volume * VolumeMaster * VolumeSfx;
            source.Play();
            ActiveSfxVoices++;
            SlotPlayed?.Invoke(cue.Slot);
        }

        /// <summary>A free voice, or else the least important and oldest if it matters less than this.</summary>
        private Voice FindVoice(int priority)
        {
            Voice weakest = null;
            for (int i = 0; i < _sfx.Length; i++)
            {
                Voice voice = _sfx[i];
                if (voice.Group < 0) return voice;
                if (weakest == null || voice.Priority < weakest.Priority || (voice.Priority == weakest.Priority && voice.Started < weakest.Started)) weakest = voice;
            }
            return weakest.Priority > priority ? null : weakest;
        }

        private void ScreenPlace(Vector3 world, out float pan, out float gain)
        {
            if (_camera == null) _camera = Camera.main;
            pan = 0f;
            gain = 1f;
            if (_camera == null) return;
            Vector3 view = _camera.WorldToViewportPoint(world);
            pan = Mathf.Clamp((view.x - 0.5f) * 1.5f, -0.8f, 0.8f);
            float outside = Mathf.Max(Mathf.Abs(view.x - 0.5f), Mathf.Abs(view.y - 0.5f)) - 0.5f;
            if (view.z < 0f) outside = 1f;
            if (outside > 0f) gain = Mathf.Lerp(1f, 0.3f, outside / 0.8f);
        }

        /// <summary>How much of the engine and hover hums to play, from 0 to 1. BattleAudio sets these from what is alive.</summary>
        public void SetHums(float engine, float hover)
        {
            _engineTarget = Mathf.Clamp01(engine);
            _hoverTarget = Mathf.Clamp01(hover);
        }

        /// <summary>Stops or restarts every battle sound with the battle. Music and UI sounds go on.</summary>
        public void SetBattlePaused(bool paused)
        {
            if (paused == BattlePaused) return;
            BattlePaused = paused;
            for (int i = 0; i < _sfx.Length; i++)
            {
                if (_sfx[i].Group < 0) continue;
                if (paused) _sfx[i].Source.Pause();
                else _sfx[i].Source.UnPause();
            }
            if (paused)
            {
                _engineHum.Pause();
                _hoverHum.Pause();
            }
            else
            {
                _engineHum.UnPause();
                _hoverHum.UnPause();
            }
        }

        /// <summary>Stops every battle sound at once (a scene or a battle was left).</summary>
        public void StopBattleSounds()
        {
            BattlePaused = false;
            for (int i = 0; i < _sfx.Length; i++)
            {
                Voice voice = _sfx[i];
                voice.Source.Stop();
                voice.Group = -1;
            }
            _limiter.Clear();
            ActiveSfxVoices = 0;
            SetHums(0f, 0f);
        }

        // ------------------------------------------------------------------ every frame

        private void Update()
        {
            if (_checkListener) EnsureListener();
            float dt = Time.unscaledDeltaTime;
            if (Time.unscaledTime >= _nextPoll)
            {
                _nextPoll = Time.unscaledTime + PollSeconds;
                ReadVolumes();
            }
            if (!BattlePaused)
            {
                _battleClock += dt;
                FinishVoices();
            }
            WatchClicks();
            UpdateHum(_engineHum, AudioCatalog.HumEngine, ref _engineLevel, _engineTarget, dt);
            UpdateHum(_hoverHum, AudioCatalog.HumHover, ref _hoverLevel, _hoverTarget, dt);
            Music.Tick(dt, VolumeMaster * VolumeMusic);
            if (_notingCues && (_notedMusic != Music.CurrentSlot || _notedStinger != Music.LastStinger))
            {
                _notedMusic = Music.CurrentSlot;
                _notedStinger = Music.LastStinger;
                AudioCapture.Note($"MUSIC {_notedMusic} stinger {_notedStinger}");
            }
        }

        private void FinishVoices()
        {
            for (int i = 0; i < _sfx.Length; i++)
            {
                Voice voice = _sfx[i];
                if (voice.Group < 0 || _battleClock < voice.EndsAt) continue;
                _limiter.Release(voice.Group);
                voice.Group = -1;
                ActiveSfxVoices--;
            }
        }

        /// <summary>A click anywhere on any IMGUI button, whichever HUD drew it.</summary>
        private void WatchClicks()
        {
            int hot = GUIUtility.hotControl;
            if (hot != 0 && _lastHotControl == 0) PlayUi(UiSound.Click);
            _lastHotControl = hot;
        }

        private void UpdateHum(AudioSource source, SfxCue cue, ref float level, float target, float dt)
        {
            if (BattlePaused) return;
            level = Mathf.MoveTowards(level, target, dt * 0.6f);
            if (level <= 0.001f)
            {
                if (source.isPlaying) source.Stop();
                return;
            }
            if (!source.isPlaying)
            {
                // Nothing wants it any more, and it's still held from a pause the battle was left in: it must not start again just to fade out.
                if (target <= 0f)
                {
                    level = 0f;
                    source.Stop();
                    return;
                }
                AudioClip clip = cue.Clips.Pick(0f);
                if (clip == null) return;
                source.clip = clip;
                source.Play();
                SlotPlayed?.Invoke(cue.Slot);
            }
            source.volume = level * cue.Volume * VolumeMaster * VolumeSfx;
        }

        private void ReadVolumes()
        {
            float master = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMaster, DefaultMaster));
            float music = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMusic, DefaultMusic));
            float sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfx, DefaultSfx));
            float ui = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyUi, DefaultUi));
            if (master == VolumeMaster && music == VolumeMusic && sfx == VolumeSfx && ui == VolumeUi) return;
            VolumeMaster = master;
            VolumeMusic = music;
            VolumeSfx = sfx;
            VolumeUi = ui;
            for (int i = 0; i < _sfx.Length; i++)
            {
                if (_sfx[i].Group >= 0) _sfx[i].Source.volume = _sfx[i].Volume * master * sfx;
            }
        }

        /// <summary>A random number from 0 up to 1, from a generator of its own: the game's others must not be disturbed.</summary>
        private float NextRandom()
        {
            uint x = _random;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _random = x;
            return (x & 0xFFFFFF) / 16777216f;
        }
    }
}
