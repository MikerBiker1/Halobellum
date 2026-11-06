// SandboxAutoplay.cs
// Test tool for built games: shows the Sandbox scene working without clicking. It does nothing
// unless the game is started with "-sandboxDemo <screenshot folder>" (MatchAutoplay then opens the
// Sandbox scene). It places a mixed army on each side (ground and air units), saves a screenshot of
// the placement, runs the battle at 4x, saves screenshots at 3 s and 12 s of battle time and at the
// end, then quits. It also saves one screenshot of a lobbed shot at the top of its arc, one of a beam
// charging, one of a beam firing, one of the first infected body and one of a unit leaping, the first time each is
// seen, close-ups of a baked Marine: held still, in the middle of a cross-fade, running, shooting and falling, and one of the Scorpion.
// If the towers are on (SandboxHud's "-sandboxTowers"), it also takes a close-up of each tower before the battle, and brings down blue's Command Center
// after 4 s of battle (the demo's armies don't get that far by themselves): a picture of the shockwave and a close-up of the wreck, and a line in the log.
// With "-gallery" as well, it instead goes through every unit that has a real model (M9, D45): each one standing, moving
// and firing, close up and from the game's camera, so that a person can judge them from pictures, and then every effect (EffectsGallery).
// With "-effects" it only takes the pictures of the effects, and with "-effectsBench" it only measures the frame time of a big battle.
// With "-galleryTowers" it only takes pictures of the towers: UNSC against Covenant, Covenant against Flood and Flood against UNSC, each
// tower close up and the whole field from straight above, so that the towers' faction colours can be judged.
// Depends on: SandboxController, BattleController, Halobellum.Combat, Halobellum.Core, UnityEngine.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Units;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Halobellum.Presentation
{
    [RequireComponent(typeof(SandboxController))]
    public sealed class SandboxAutoplay : MonoBehaviour
    {
        public const string Flag = "-sandboxDemo";
        private const string GalleryFlag = "-gallery";
        private const string EffectsFlag = "-effects";
        private const string BenchFlag = "-effectsBench";
        private const string TowersFlag = "-galleryTowers";

        private static readonly string[] Blue = { "Marine", "Marine", "Marine", "Marine", "Scorpion", "Spartan" };
        private static readonly string[] Red = { "Infection Forms", "Infection Forms", "Infection Forms", "Infection Forms", "Carrier Form", "Flood Abomination", "Scarab", "Wraith" };

        private SandboxController _sandbox;
        private BattleController _battle;
        private string _shots;
        private bool _gallery, _all, _effectsOnly, _benchOnly, _towersOnly;
        private string _only;
        private int _step;
        private float _wait = 1f;
        private bool _shotArc, _shotCharge, _shotBeam, _shotInfected, _shotLeap;
        private readonly List<ShotInFlight> _inFlight = new List<ShotInFlight>();
        private readonly List<BeamShot> _beams = new List<BeamShot>();
        private readonly HashSet<SimUnit> _towersShot = new HashSet<SimUnit>();
        private const float TowerFallsAtSeconds = 4f, ShockwaveShotAfter = 0.1f, WreckShotAfter = 1f;
        private SimUnit _fallenTower;
        private float _towerFellAt;
        private bool _shotShockwave, _shotWreck;

        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Flag);
            if (index < 0)
            {
                enabled = false;
                return;
            }
            _shots = index + 1 < args.Length ? args[index + 1] : "SandboxShots";
            Directory.CreateDirectory(_shots);
            _gallery = Array.IndexOf(args, GalleryFlag) >= 0;
            _all = Array.IndexOf(args, "-galleryAll") >= 0; // Placeholder bodies too.
            int only = Array.IndexOf(args, "-galleryOnly"); // Only these, split by commas: a name a unit is listed under, such as a sub-faction ("Noble"), or a unit's own name.
            if (only >= 0 && only + 1 < args.Length) _only = args[only + 1];
            _effectsOnly = Array.IndexOf(args, EffectsFlag) >= 0;
            _benchOnly = Array.IndexOf(args, BenchFlag) >= 0;
            _towersOnly = Array.IndexOf(args, TowersFlag) >= 0;
            // Measures one setting of anti-aliasing against another on one build. For this run only: nothing is saved.
            int msaa = Array.IndexOf(args, "-msaa");
            if (msaa >= 0 && msaa + 1 < args.Length && int.TryParse(args[msaa + 1], out int samples) && UniversalRenderPipeline.asset != null) UniversalRenderPipeline.asset.msaaSampleCount = samples;
        }

        /// <summary>The anti-aliasing the game's render pipeline is set to now: 1 is none.</summary>
        public static int ShownMsaa => UniversalRenderPipeline.asset != null ? UniversalRenderPipeline.asset.msaaSampleCount : 1;

        private void Start()
        {
            _sandbox = GetComponent<SandboxController>();
            _battle = GetComponent<BattleController>();
            if (_towersOnly) StartCoroutine(TowerGallery());
            else if (_effectsOnly || _benchOnly) StartCoroutine(EffectsOnly());
            else if (_gallery) StartCoroutine(Gallery());
        }

        /// <summary>Only the pictures of the effects, or only the frame-time measurement.</summary>
        private IEnumerator EffectsOnly()
        {
            yield return new WaitForSecondsRealtime(1f);
            EffectsGallery effects = gameObject.AddComponent<EffectsGallery>();
            effects.Prepare(_shots, _only);
            yield return _benchOnly ? effects.Benchmark() : effects.TakeAll();
            Application.Quit();
        }

        /// <summary>Both sides' towers in each faction's colours: close up, and the field from straight above, as the Battle stats window shows it.</summary>
        private IEnumerator TowerGallery()
        {
            yield return new WaitForSecondsRealtime(1f);
            _sandbox.SetTowers(true);
            string[][] armies = { new[] { "Marine", "Elite" }, new[] { "Elite", "Combat Form" }, new[] { "Combat Form", "Marine" } };
            foreach (string[] army in armies)
            {
                _sandbox.Clear(Team.A);
                _sandbox.Clear(Team.B);
                _sandbox.AddSquad(Team.A, Find(army[0]));
                _sandbox.AddSquad(Team.B, Find(army[1]));
                yield return new WaitForSecondsRealtime(0.5f);
                string label = $"towers_{_sandbox.FactionOf(Team.A)}_vs_{_sandbox.FactionOf(Team.B)}";
                Shot(label + "_field");
                yield return new WaitForSecondsRealtime(0.2f);
                foreach (SimUnit tower in _battle.Simulation.Units.Where(u => u.Stats.IsTower).ToList())
                {
                    yield return CloseUp(tower, $"{label}_{tower.Team}_{tower.Stats.Shockwave.Kind}");
                    yield return new WaitForSecondsRealtime(0.1f);
                }
                yield return FromAbove(label + "_above");
            }
            Debug.Log("Tower gallery finished.");
            Application.Quit();
        }

        /// <summary>The whole field from straight above, the enemy's edge at the top, as BattlePicture takes it for the Battle stats window.</summary>
        private IEnumerator FromAbove(string name)
        {
            Camera view = Camera.main;
            var control = view.GetComponent<BattleCameraController>();
            if (control != null) control.enabled = false;
            view.transform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);
            bool orthographic = view.orthographic;
            float size = view.orthographicSize, far = view.farClipPlane;
            bool fog = RenderSettings.fog;
            view.transform.SetPositionAndRotation(new Vector3(0f, 150f, 0f), Quaternion.Euler(90f, 0f, 0f));
            view.orthographic = true;
            view.orthographicSize = _battle.Rules.ArenaLength * 0.5f;
            view.farClipPlane = 400f;
            RenderSettings.fog = false;
            yield return null;
            Shot(name);
            yield return null;
            yield return null;
            RenderSettings.fog = fog;
            view.orthographic = orthographic;
            view.orthographicSize = size;
            view.farClipPlane = far;
            view.transform.SetPositionAndRotation(position, rotation);
            if (control != null) control.enabled = true;
        }

        /// <summary>Every unit with a real model, on its own against a small enemy force.</summary>
        private IEnumerator Gallery()
        {
            yield return new WaitForSecondsRealtime(1f);
            var views = GetComponent<UnitViewManager>();
            var shown = new List<UnitStats>();
            foreach (SandboxController.SandboxUnit entry in _sandbox.Units.Concat(_sandbox.DropUnits))
            {
                UnitStats type = entry.Stats;
                _sandbox.Clear(Team.A);
                _sandbox.Clear(Team.B);
                _sandbox.AddSquad(Team.A, type);
                _sandbox.AddSquad(Team.B, Find("Marine"));
                _sandbox.AddSquad(Team.B, Find("Hornet"));
                yield return null;
                if (!_all && !_battle.KeepsOwnMaterials(type) && _battle.BakedBodyFor(type) == null) continue;
                if (_only != null && !_only.Split(',').Any(name => entry.Faction.Contains(name) || type.Name == name)) continue;
                shown.Add(type);
                string name = type.Name.Replace(" ", "");

                SimUnit Body() => FirstOf(type);
                yield return CloseUp(Body(), name + "_still");
                yield return new WaitForSecondsRealtime(0.1f);
                Shot(name + "_field");
                yield return new WaitForSecondsRealtime(0.2f);

                _battle.Speed = 2f;
                _sandbox.Run();
                yield return null;
                float limit = 20f;
                while (limit > 0f && Body() != null && Body().State != UnitState.Move)
                {
                    limit -= Time.unscaledDeltaTime;
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(0.4f);
                if (Body() != null) yield return CloseUp(Body(), name + "_moving");
                limit = 25f;
                while (limit > 0f && Body() != null && Body().State != UnitState.Attack && !_battle.Simulation.IsFinished)
                {
                    limit -= Time.unscaledDeltaTime;
                    yield return null;
                }
                for (int i = 0; i < 3 && Body() != null && !_battle.Simulation.IsFinished; i++)
                {
                    yield return new WaitForSecondsRealtime(0.35f);
                    if (Body() != null) yield return CloseUp(Body(), $"{name}_firing{i}");
                }
                yield return new WaitForSecondsRealtime(0.1f);
                Shot(name + "_field_firing");
                yield return new WaitForSecondsRealtime(0.2f);
                _sandbox.ResetBattle();
                yield return null;
            }

            // All of them together, as they would stand on the field.
            _sandbox.Clear(Team.A);
            _sandbox.Clear(Team.B);
            foreach (UnitStats type in shown) _sandbox.AddSquad(Team.A, type);
            foreach (UnitStats type in shown) _sandbox.AddSquad(Team.B, type);
            yield return new WaitForSecondsRealtime(0.5f);
            Shot("all_field");
            yield return new WaitForSecondsRealtime(0.3f);
            _battle.Speed = 2f;
            _sandbox.Run();
            yield return new WaitForSecondsRealtime(5f);
            Shot("all_battle");
            yield return new WaitForSecondsRealtime(0.3f);
            Debug.Log($"Model gallery finished: {shown.Count} units with real models.");
            _sandbox.ResetBattle();
            yield return null;
            EffectsGallery effects = gameObject.AddComponent<EffectsGallery>();
            effects.Prepare(_shots);
            yield return effects.TakeAll();
            Application.Quit();
        }

        private SimUnit FirstOf(UnitStats type)
        {
            if (_battle.Simulation == null) return null;
            foreach (SimUnit unit in _battle.Simulation.Units)
            {
                if (unit.IsAlive && unit.Team == Team.A && unit.Stats.Name == type.Name) return unit;
            }
            return null;
        }

        private bool _closeUp, _shotFade, _shotTank;
        private readonly HashSet<BakedAnimation> _shotRoles = new HashSet<BakedAnimation>();

        /// <summary>Freezes the battle, looks closely at a body, saves a screenshot, and carries on.</summary>
        private IEnumerator CloseUp(SimUnit unit, string name)
        {
            _closeUp = true;
            float speed = _battle.Speed;
            _battle.Speed = 0f;
            Camera view = Camera.main;
            var control = view.GetComponent<BattleCameraController>();
            if (control != null) control.enabled = false;
            view.transform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);
            float top = GetComponent<UnitViewManager>().BodyTopOf(unit);
            var body = GetComponent<UnitViewManager>().WorldPosition(unit) + Vector3.up * Mathf.Max(0.8f, top * 0.5f);
            var facing = new Vector3(unit.Facing.X, 0f, unit.Facing.Y);
            float reach = Mathf.Max(1f, Mathf.Max(unit.Radius, top * 0.45f)); // Further back for something big.
            view.transform.position = body + (facing * 3.4f + Vector3.Cross(facing, Vector3.up) * 1.8f + Vector3.up * 0.9f) * reach;
            view.transform.LookAt(body);
            yield return null;
            Shot(name);
            yield return null;
            yield return null;
            view.transform.SetPositionAndRotation(position, rotation);
            if (control != null) control.enabled = true;
            _battle.Speed = speed;
            _closeUp = false;
        }

        private void ShootMarine()
        {
            BattleSimulation simulation = _battle.Simulation;
            if (simulation == null || _closeUp) return;
            var views = GetComponent<UnitViewManager>();
            foreach (SimUnit unit in simulation.Units)
            {
                if (unit.Stats.Name == "Scorpion" && !_shotTank && unit.IsAlive && simulation.ElapsedSeconds >= 1f)
                {
                    _shotTank = true;
                    StartCoroutine(CloseUp(unit, "scorpion"));
                    return;
                }
                if (unit.Stats.Name != "Marine") continue;
                BakedAnimation? playing = views.PlayingAnimation(unit);
                if (playing == null) return;
                // The fade takes 0.2 battle seconds and starts with the battle.
                if (_step >= 5 && !_shotFade && simulation.ElapsedSeconds >= 0.1f)
                {
                    _shotFade = true;
                    StartCoroutine(CloseUp(unit, "marine_fade"));
                    return;
                }
                bool settled = simulation.ElapsedSeconds >= 1f;
                if (playing == BakedAnimation.Still && _step == 4 && _shotRoles.Add(BakedAnimation.Still))
                {
                    StartCoroutine(CloseUp(unit, "marine_still"));
                    return;
                }
                if (settled && playing == BakedAnimation.Moving && unit.IsAlive && _shotRoles.Add(BakedAnimation.Moving))
                {
                    StartCoroutine(CloseUp(unit, "marine_moving"));
                    return;
                }
                if (settled && playing == BakedAnimation.Shooting && unit.IsAlive && unit.Weapons[0].Target != null && _shotRoles.Add(BakedAnimation.Shooting))
                {
                    StartCoroutine(CloseUp(unit, "marine_shooting"));
                    return;
                }
                // A corpse that rises as infected is gone before it has finished falling: try the next one.
                if (unit == _deadSeen && !views.IsShown(unit))
                {
                    _deadSeen = null;
                    _deadShotAt = 0f;
                }
                if (playing == BakedAnimation.Dead && !unit.IsAlive && views.IsShown(unit) && !_shotRoles.Contains(BakedAnimation.Dead))
                {
                    if (_deadSeen == null) _deadSeen = unit;
                    if (_deadSeen == unit && Time.unscaledTime >= _deadShotAt && _deadShotAt > 0f)
                    {
                        _shotRoles.Add(BakedAnimation.Dead);
                        StartCoroutine(CloseUp(unit, "marine_dead"));
                        return;
                    }
                    if (_deadSeen == unit && _deadShotAt <= 0f) _deadShotAt = Time.unscaledTime + 0.6f; // At 4x: well into the fall.
                }
            }
        }

        private SimUnit _deadSeen;
        private float _deadShotAt;

        private void Update()
        {
            if (_gallery || _effectsOnly || _benchOnly || _towersOnly) return;
            if (_step >= 4) ShootMarine();
            if (_closeUp) return;
            if (_step >= 5) ShootWeaponMechanics();
            if (_step >= 5 && _sandbox.TowersOn) ShootFallingTower();
            _wait -= Time.unscaledDeltaTime;
            if (_wait > 0f) return;
            switch (_step)
            {
                case 0:
                    foreach (string name in Blue) _sandbox.AddSquad(Team.A, Find(name));
                    foreach (string name in Red) _sandbox.AddSquad(Team.B, Find(name));
                    _step++;
                    _wait = 0.5f;
                    break;
                case 1:
                    // An item selected (hexagons over the squads) and one already on a squad.
                    if (_sandbox.Items.Count > 1)
                    {
                        _sandbox.BeginPlacingItem(_sandbox.Items[0]);
                        _sandbox.PlaceItemOn(_sandbox.ArmyA.Squads[0]);
                        _sandbox.BeginPlacingItem(_sandbox.Items[1]);
                    }
                    _step++;
                    _wait = 0.4f;
                    break;
                case 2:
                    Shot("0_items");
                    _step++;
                    _wait = 0.3f;
                    break;
                case 3:
                    _sandbox.CancelPlacement();
                    Shot("1_placement");
                    _step++;
                    _wait = 0.3f;
                    break;
                case 4:
                    SimUnit tower = NextTowerToShoot();
                    if (tower != null)
                    {
                        StartCoroutine(CloseUp(tower, $"tower_{tower.Team}_{tower.Stats.Shockwave.Kind}"));
                        break;
                    }
                    _battle.Speed = 4f;
                    _sandbox.Run();
                    _step++;
                    break;
                case 5:
                    if (_battle.Simulation.ElapsedSeconds >= 3f) { Shot("2_battle_3s"); _step++; }
                    break;
                case 6:
                    if (_battle.Simulation.ElapsedSeconds >= 12f || _battle.Simulation.IsFinished) { Shot("3_battle_12s"); _step++; }
                    break;
                case 7:
                    if (_battle.Simulation.IsFinished) { _step++; _wait = 0.5f; }
                    break;
                case 8:
                    Shot("4_result");
                    _step++;
                    _wait = 0.5f;
                    break;
                default:
                    enabled = false;
                    int infected = 0;
                    foreach (SimUnit unit in _battle.Simulation.Units) if (unit.Stats.InfectedFrom != null) infected++;
                    Debug.Log("Sandbox demo finished: " + _battle.Simulation.Result.ToSummaryLine() + $"; {infected} rose infected");
                    Application.Quit();
                    break;
            }
        }

        private SimUnit NextTowerToShoot()
        {
            foreach (SimUnit unit in _battle.Simulation.Units)
            {
                if (unit.Stats.IsTower && _towersShot.Add(unit)) return unit;
            }
            return null;
        }

        private void ShootFallingTower()
        {
            BattleSimulation simulation = _battle.Simulation;
            if (_fallenTower == null)
            {
                if (simulation.ElapsedSeconds < TowerFallsAtSeconds || simulation.IsFinished) return;
                foreach (SimUnit unit in simulation.Units)
                {
                    if (!unit.Stats.IsTower || unit.Team != Team.A || unit.Stats.Shockwave.Kind != TowerKind.CommandCenter) continue;
                    unit.Health = 0f;
                    _fallenTower = unit;
                    _towerFellAt = Time.unscaledTime;
                    break;
                }
                return;
            }
            if (!_shotShockwave && Time.unscaledTime >= _towerFellAt + ShockwaveShotAfter)
            {
                _shotShockwave = true;
                Shot("tower_shockwave");
                Debug.Log($"Sandbox demo: {_fallenTower.Stats.Name} fell; blue suffers a shockwave for {simulation.ShockwaveSecondsLeft(Team.A):0.0} s more; red for {simulation.ShockwaveSecondsLeft(Team.B):0.0} s");
            }
            if (!_shotWreck && Time.unscaledTime >= _towerFellAt + WreckShotAfter)
            {
                _shotWreck = true;
                StartCoroutine(CloseUp(_fallenTower, "tower_wreck"));
            }
        }

        private void ShootWeaponMechanics()
        {
            BattleSimulation simulation = _battle.Simulation;
            if (simulation == null) return;
            if (!_shotArc)
            {
                simulation.GetProjectiles(_inFlight);
                foreach (ShotInFlight shot in _inFlight)
                {
                    if (shot.ArcLength <= 0f || shot.Progress < 0.4f || shot.Progress > 0.6f) continue;
                    Shot("arc");
                    _shotArc = true;
                    break;
                }
            }
            if (!_shotLeap)
            {
                int leaping = 0;
                foreach (SimUnit unit in simulation.Units) if (unit.IsAlive && unit.IsLeaping && unit.LeapProgress > 0.3f) leaping++;
                if (leaping >= 5)
                {
                    Shot("leap");
                    _shotLeap = true;
                }
            }
            if (!_shotInfected)
            {
                foreach (SimUnit unit in simulation.Units)
                {
                    if (unit.Stats.InfectedFrom == null) continue;
                    Shot("infected");
                    _shotInfected = true;
                    break;
                }
            }
            simulation.GetBeams(_beams);
            foreach (BeamShot beam in _beams)
            {
                if (beam.Firing && !_shotBeam)
                {
                    Shot("beam_firing");
                    _shotBeam = true;
                }
                else if (!beam.Firing && beam.Charge > 0.6f && !_shotCharge)
                {
                    Shot("beam_charging");
                    _shotCharge = true;
                }
            }
        }

        private UnitStats Find(string name)
        {
            foreach (SandboxController.SandboxUnit unit in _sandbox.Units)
            {
                if (unit.Stats.Name == name) return unit.Stats;
            }
            throw new InvalidOperationException("No sandbox unit named " + name);
        }

        private void Shot(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(_shots, name + ".png"));
    }
}
