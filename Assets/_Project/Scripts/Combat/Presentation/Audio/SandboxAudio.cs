// SandboxAudio.cs
// The sound of the Sandbox: a random deployment song while placing and a random combat song while fighting, a squad placed drops in, a squad
// moved or turned sounds as in a match, a squad removed sounds sold, what can't be done sounds so, and pressing Run starts the battle with its sound. Squads coming and going and the battle starting are watched here each frame; the rest are the SandboxController's events.
// Added at run time to the object with the SandboxController (see AudioDirector).
// Depends on: AudioDirector, AudioSlots, SandboxController, WeaponFeel (BattleFactions), UnityEngine.

using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class SandboxAudio : MonoBehaviour
    {
        private SandboxController _sandbox;
        private bool _wasBattle;
        private bool _battleSongPicked;
        private int _lastSquads = -1;

        private void Awake()
        {
            _sandbox = GetComponent<SandboxController>();
            BattleFactions.Changed += Refresh;
            _sandbox.SquadMoved += UiAudio.SquadMoved;
            _sandbox.SquadTurned += UiAudio.SquadTurned;
            _sandbox.ActionRefused += OnRefused;
            Refresh();
        }

        private void OnDestroy()
        {
            BattleFactions.Changed -= Refresh;
            if (_sandbox == null) return;
            _sandbox.SquadMoved -= UiAudio.SquadMoved;
            _sandbox.SquadTurned -= UiAudio.SquadTurned;
            _sandbox.ActionRefused -= OnRefused;
        }

        private static void OnRefused() => GameAudio.Ui(UiSound.Error);

        private void Refresh()
        {
            AudioDirector director = AudioDirector.Instance;
            if (director == null) return;
            director.UiFaction = BattleFactions.TeamA;
            if (!_sandbox.IsBattle)
            {
                _battleSongPicked = false;
                director.Music.Play(AudioSlots.DeploymentMusic);
            }
            else if (!_battleSongPicked)
            {
                _battleSongPicked = true; // Once per battle: the sides' looks changing must not change the song.
                director.Music.Play(AudioSlots.CombatSong(AudioSlots.FloodIn(GetComponent<BattleController>().Simulation), director.NextRandom()), 1.5f);
            }
        }

        private void Update()
        {
            if (_sandbox.IsBattle != _wasBattle)
            {
                _wasBattle = _sandbox.IsBattle;
                Refresh();
                if (_wasBattle) GameAudio.Ui(UiSound.BattleStart);
            }
            int squads = _sandbox.ArmyA.Squads.Count + _sandbox.ArmyB.Squads.Count;
            if (_lastSquads >= 0 && !_sandbox.IsBattle)
            {
                if (squads > _lastSquads) GameAudio.Sfx(AudioCatalog.DropIn);
                else if (squads < _lastSquads) GameAudio.Ui(UiSound.Sell);
            }
            _lastSquads = squads;
        }
    }
}
