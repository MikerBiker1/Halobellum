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
// Loops that follow the battle (guns that fire fast, a mortar shell in the air, a Scarab walking or firing its beam) each have one voice of
// their own, held at a level BattleAudio sets (SetLoop) and faded in and out quickly. The owner's files are played at their cue's loudness
// (AudioClipSet.Gain): they come much louder than the game's mix. A long sound can also start part way in and fade out at a given time
// (PlaySfxShaped: the Glassing Beam, which must fit its beam), and a cue with a long quiet tail stops counting against its group's limit once
// the loud part is over (SfxCue.HoldSeconds).
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

        /// <summary>Dips the music to this level for a moment, longer if it is called again (a node of light hitting a base, the owner, 2026-11-06).</summary>
        public static void DipMusic(float seconds, float level) => AudioDirector.Instance?.Music?.Dip(seconds, level);

        public static void Sfx(SfxCue cue, Vector3 world) => AudioDirector.Instance?.PlaySfx(cue, world);

        /// <summary>See AudioDirector.PlaySfxShaped.</summary>
        public static void SfxShaped(SfxCue cue, Vector3 world, float skipSeconds, float fadeAfter, float fadeSeconds) => AudioDirector.Instance?.PlaySfxShaped(cue, world, skipSeconds, fadeAfter, fadeSeconds);
    }

    public sealed class AudioDirector : MonoBehaviour
    {
        private const int SfxVoices = 20, UiVoices = 5, LoopVoices = 8;
        private const float PollSeconds = 0.5f;
        /// <summary>All effects together, so that they sit under the music's level with room to spare (the owner's files are fuller than the generated ones).</summary>
        private const float SfxTrim = 0.4f; // A recorded battle with the owner's files sat 3 dB over the music at 0.55 (D70).
        /// <summary>How loud the owner's UI sounds are turned to: the loudest 50 ms, in dB below full scale. Clicks and hovers sit lower.</summary>
        private const float UiLoudness = -17f, UiQuietLoudness = -21f;
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
            /// <summary>When it starts fading out, by the battle clock, and over how long; 0 seconds if it plays to its end.</summary>
            public float FadeAt, FadeSeconds;
        }

        private sealed class LoopVoice
        {
            public AudioSource Source;
            public SfxCue Cue;
            public float Level, Target, Gain;
            /// <summary>Where it is from the camera: on screen or not, near or far (Placement).</summary>
            public float Place = 1f;
        }

        private readonly LoopVoice[] _loops = new LoopVoice[LoopVoices];
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
        private uint _random = (uint)Environment.TickCount | 1u; // Different every run, so that the songs are too. Never 0, which would stay 0.
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
            GameFlow.AutoplayFlag, SandboxAutoplay.Flag, GameFlow.GalleryFlag, CrowdBenchmark.Flag, GameFlow.BenchmarkFlag, GameFlow.MenuDemoFlag, GameFlow.ScreenDemoFlag, GameFlow.CardsDemoFlag, "-terrainGallery", GameFlow.TutorialDemoFlag, MatchReplay.LogFlag,
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
            for (int i = 0; i < LoopVoices; i++)
            {
                _loops[i] = new LoopVoice { Source = NewSource(100) };
                _loops[i].Source.loop = true;
            }
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
            foreach (SfxCue cue in AudioCatalog.OwnerOnly) _ = cue.Clips;
            _ = AudioCatalog.DeathLekgolo.Clips;
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

        /// <summary>True while something shown before the main menu holds its music back (the owner's disclaimer, D101). Set before the menu's scene is handled.</summary>
        public static bool HoldMenuMusic { get; set; }

        /// <summary>Lets the main menu's music start, once what held it back has gone (D101).</summary>
        public void ReleaseMenuMusic()
        {
            HoldMenuMusic = false;
            if (Music.CurrentSlot != AudioSlots.MainMenuMusic) Music.Play(AudioSlots.MainMenuMusic);
        }

        /// <summary>Connects to what is in a scene and decides on its music.</summary>
        public void HandleScene(string sceneName)
        {
            if (sceneName == AudioSlots.MainMenuScene || GameFlow.IsMainMenu(sceneName))
            {
                UiFaction = null;
                if (!HoldMenuMusic) Music.Play(AudioSlots.MainMenuMusic);
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
                Music.Play(AudioSlots.CombatSong(AudioSlots.FloodIn(battle.Simulation), NextRandom()));
            }
        }

        private void OnFactionsChanged()
        {
            // In Unlock Skirmish both sides stand on one land, but nobody is of it: the UI sounds stay neutral.
            if (UiFaction != null) UiFaction = BattleFactions.PerUnitLooks ? null : BattleFactions.TeamA;
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
            AudioClipSet set = AudioLibrary.Get(slot);
            int index = set.PickIndex(NextRandom());
            if (index < 0) return;
            AudioClip clip = set.Clips[index];
            volume *= set.Gain(index, sound == UiSound.Click || sound == UiSound.Hover ? UiQuietLoudness : UiLoudness);
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

        /// <summary>
        /// An effect from a place in the world, started skipSeconds into its clip, that fades out over fadeSeconds once it has played for fadeAfter seconds
        /// (by the battle clock: paused with the battle). For a long sound that must fit something shorter (the Glassing Beam).
        /// </summary>
        public void PlaySfxShaped(SfxCue cue, Vector3 world, float skipSeconds, float fadeAfter, float fadeSeconds)
        {
            Voice voice = Begin(cue, true, world);
            if (voice == null) return;
            AudioClip clip = voice.Source.clip;
            float skip = Mathf.Clamp(skipSeconds, 0f, Mathf.Max(0f, clip.length - 0.05f));
            if (skip > 0f) voice.Source.time = skip;
            float pitch = Mathf.Max(0.1f, voice.Source.pitch);
            voice.FadeSeconds = Mathf.Max(0.05f, fadeSeconds);
            voice.FadeAt = _battleClock + Mathf.Max(0f, fadeAfter);
            voice.EndsAt = Mathf.Min(_battleClock + (clip.length - skip) / pitch, voice.FadeAt + voice.FadeSeconds);
        }

        private Voice Begin(SfxCue cue, bool placed, Vector3 world)
        {
            if (cue == null || BattlePaused) return null;
            if (!_limiter.TryStart((int)cue.Group, _battleClock))
            {
                DroppedVoices++; // Most shots of a big battle end here, before any more work.
                return null;
            }
            int index = cue.Clips.PickIndex(NextRandom());
            AudioClip clip = index >= 0 ? cue.Clips.Clips[index] : null;
            Voice voice = clip != null ? FindVoice(cue.Priority) : null;
            if (voice == null)
            {
                _limiter.Release((int)cue.Group);
                DroppedVoices++;
                return null;
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
            if (cue.HoldSeconds > 0f) voice.EndsAt = Mathf.Min(voice.EndsAt, _battleClock + cue.HoldSeconds); // Its tail rings on, but no longer keeps the next one out.
            voice.FadeSeconds = 0f;
            // In the thick of it each new sound is a little quieter, so that the sum stays under the ceiling: a recorded big battle reached 0 dB before this.
            float crowd = ActiveSfxVoices <= CrowdStarts ? 1f : Mathf.Sqrt((float)CrowdStarts / ActiveSfxVoices);
            voice.Volume = cue.Volume * gain * crowd * SfxTrim * cue.Clips.Gain(index, cue.Loudness);
            AudioSource source = voice.Source;
            source.Stop();
            source.clip = clip;
            source.pitch = pitch;
            source.panStereo = pan;
            source.volume = voice.Volume * VolumeMaster * VolumeSfx;
            source.Play();
            ActiveSfxVoices++;
            SlotPlayed?.Invoke(cue.Slot);
            return voice;
        }

        /// <summary>A free voice, or else the least important and oldest if it matters less than this.</summary>
        private Voice FindVoice(int priority)
        {
            // A free voice that is silent first; then one whose tail still rings after its hold (SfxCue.HoldSeconds), so a tail is cut only when it must.
            Voice ringing = null, weakest = null;
            for (int i = 0; i < _sfx.Length; i++)
            {
                Voice voice = _sfx[i];
                if (voice.Group < 0)
                {
                    if (!voice.Source.isPlaying) return voice;
                    ringing ??= voice;
                    continue;
                }
                if (weakest == null || voice.Priority < weakest.Priority || (voice.Priority == weakest.Priority && voice.Started < weakest.Started)) weakest = voice;
            }
            if (ringing != null) return ringing;
            return weakest == null || weakest.Priority > priority ? null : weakest;
        }

        /// <summary>
        /// Fades out, over seconds, every shaped sound (PlaySfxShaped) not yet fading: a battle that ends before the Glassing Beam strikes takes its
        /// build-up away with it.
        /// </summary>
        public void FadeShaped(float seconds)
        {
            for (int i = 0; i < _sfx.Length; i++)
            {
                Voice voice = _sfx[i];
                if (voice.Group < 0 || voice.FadeSeconds <= 0f || voice.FadeAt <= _battleClock) continue;
                voice.FadeAt = _battleClock;
                voice.FadeSeconds = Mathf.Max(0.05f, seconds);
                voice.EndsAt = Mathf.Min(voice.EndsAt, _battleClock + voice.FadeSeconds);
            }
        }

        private void ScreenPlace(Vector3 world, out float pan, out float gain)
        {
            if (_camera == null) _camera = Camera.main;
            pan = 0f;
            gain = 1f;
            if (_camera == null) return;
            Placement(_camera.WorldToViewportPoint(world), Vector3.Distance(_camera.transform.position, world), out pan, out gain);
        }

        /// <summary>A sound this far from the camera plays at its own level: the game's starting view (D75, the owner, 2026-11-06).</summary>
        public const float ReferenceMetres = 150f;

        /// <summary>
        /// Pan and gain for a sound at this viewport point (z: in front of the camera if positive), this many metres from the camera.
        /// Off screen it fades to 12% within half a screen beyond the edge. Nearer than the starting view it is louder, up to 1.7 times when zoomed right in,
        /// and further away quieter, down to 0.4: zoom in on a fight to hear it over the rest (the owner, 2026-11-06).
        /// </summary>
        public static void Placement(Vector3 viewport, float metres, out float pan, out float gain)
        {
            pan = Mathf.Clamp((viewport.x - 0.5f) * 1.5f, -0.8f, 0.8f);
            float outside = Mathf.Max(Mathf.Abs(viewport.x - 0.5f), Mathf.Abs(viewport.y - 0.5f)) - 0.5f;
            if (viewport.z < 0f) outside = 1f;
            float screen = outside > 0f ? Mathf.Lerp(1f, 0.12f, outside / 0.5f) : 1f;
            float near = Mathf.Clamp(Mathf.Pow(ReferenceMetres / Mathf.Max(1f, metres), 0.7f), 0.4f, 1.7f);
            gain = screen * near;
        }

        /// <summary>
        /// Holds a loop at this level, from 0 (fades out and stops) to 1, panned by where it comes from. BattleAudio calls it every frame for the guns that are
        /// firing, and for anything else that goes on (a shell in the air, a Scarab walking). A loop with no voice left to take is not heard.
        /// </summary>
        public void SetLoop(SfxCue cue, float level, Vector3 world)
        {
            if (cue == null) return;
            LoopVoice free = null;
            for (int i = 0; i < _loops.Length; i++)
            {
                LoopVoice voice = _loops[i];
                if (voice.Cue == cue)
                {
                    voice.Target = Mathf.Clamp01(level);
                    if (voice.Level > 0f || voice.Target > 0f) Place(voice, world);
                    return;
                }
                if (free == null && voice.Cue == null) free = voice;
            }
            if (free == null || level <= 0f || BattlePaused) return;
            int index = cue.Clips.PickIndex(NextRandom());
            if (index < 0) return;
            free.Cue = cue;
            free.Level = 0f;
            free.Target = Mathf.Clamp01(level);
            free.Gain = cue.Clips.Gain(index, cue.Loudness);
            AudioSource source = free.Source;
            source.clip = cue.Clips.Clips[index];
            source.pitch = 1f;
            source.volume = 0f;
            source.time = source.clip.length * NextRandom() * 0.9f; // Two of the same loop starting together mustn't sound like one.
            Place(free, world);
            source.Play();
            SlotPlayed?.Invoke(cue.Slot);
        }

        /// <summary>The loop's level now, 0 if it isn't playing. For tests.</summary>
        public float LoopLevel(SfxCue cue)
        {
            foreach (LoopVoice voice in _loops)
            {
                if (voice.Cue == cue) return voice.Level;
            }
            return 0f;
        }

        private void Place(LoopVoice voice, Vector3 world)
        {
            ScreenPlace(world, out float pan, out float gain);
            voice.Source.panStereo = pan;
            voice.Place = gain;
        }

        private void UpdateLoops(float dt)
        {
            if (BattlePaused) return;
            for (int i = 0; i < _loops.Length; i++)
            {
                LoopVoice voice = _loops[i];
                if (voice.Cue == null) continue;
                // In fast, so the first shot is heard; out a little slower, so short pauses between bursts don't chop it.
                voice.Level = Mathf.MoveTowards(voice.Level, voice.Target, dt * (voice.Target > voice.Level ? 14f : 5f));
                if (voice.Level <= 0f && voice.Target <= 0f)
                {
                    voice.Source.Stop();
                    voice.Cue = null;
                    continue;
                }
                voice.Source.volume = voice.Level * voice.Cue.Volume * voice.Gain * voice.Place * SfxTrim * VolumeMaster * VolumeSfx;
            }
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
            foreach (LoopVoice voice in _loops)
            {
                if (voice.Cue == null) continue;
                if (paused) voice.Source.Pause();
                else voice.Source.UnPause();
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
            foreach (LoopVoice voice in _loops)
            {
                voice.Source.Stop();
                voice.Cue = null;
                voice.Level = voice.Target = 0f;
            }
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
            UpdateLoops(dt);
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
                if (voice.Group < 0) continue;
                if (voice.FadeSeconds > 0f && _battleClock >= voice.FadeAt)
                {
                    float left = 1f - (_battleClock - voice.FadeAt) / voice.FadeSeconds;
                    voice.Source.volume = voice.Volume * Mathf.Clamp01(left) * VolumeMaster * VolumeSfx;
                    if (left <= 0f) voice.Source.Stop();
                }
                if (_battleClock < voice.EndsAt) continue;
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

        /// <summary>Seconds of battle sound so far: it stops while the battle is paused. BattleAudio times what it plays in order (the Scarab's beam) by it.</summary>
        public float BattleClock => _battleClock;

        /// <summary>A random number from 0 up to 1, from a generator of its own: the game's others must not be disturbed.</summary>
        public float NextRandom()
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
