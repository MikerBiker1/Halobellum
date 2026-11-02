// SandboxAudio.cs
// The sound of the Sandbox: its music follows BattleFactions, which SandboxController sets from the armies (deployment while placing, battle while fighting), a squad placed drops in, a squad
// removed sounds sold, and pressing Run starts the battle with its sound. The SandboxController has no events, so its state is watched here each frame.
// Added at run time to the object with the SandboxController (see AudioDirector).
// Depends on: AudioDirector, AudioSlots, SandboxController, WeaponFeel (BattleFactions), UnityEngine.

using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class SandboxAudio : MonoBehaviour
    {
        private SandboxController _sandbox;
        private bool _wasBattle;
        private int _lastSquads = -1;

        private void Awake()
        {
            _sandbox = GetComponent<SandboxController>();
            BattleFactions.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy() => BattleFactions.Changed -= Refresh;

        private void Refresh()
        {
            AudioDirector director = AudioDirector.Instance;
            if (director == null) return;
            director.UiFaction = BattleFactions.TeamA;
            director.Music.Play(AudioSlots.Music(BattleFactions.TeamA, _sandbox.IsBattle ? MusicMood.Battle : MusicMood.Deployment));
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
