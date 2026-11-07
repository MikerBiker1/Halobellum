// TerrainGallery.cs
// Test tool for built games: goes through every interior look of D80 (or, with "-terrainOutdoor", every outdoor layout of M10) at the game's camera and saves pictures, so that a person can judge the terrain without playing. It does nothing unless the game is started with
// "-terrainGallery <folder>". For each map it takes a picture with bare ground, one with armies placed during deployment, one in battle, a wide one zoomed out, a close one, and one looking low at the horizon, and logs the triangle count and build time.
// Add "-terrainLook <Id>" (UnscInstallation, CovenantWarship, FloodOnUnsc, FloodOnCovenant) for one look only. Add "-terrainMaps 2" to only do that many maps, for a quick look. Add "-terrainRing" to take instead, for each map, pictures from all round: the bare squares and the ghost of a squad being
// placed and the hexagons of an item being picked from four sides, then in battle from 8 directions at the camera's default tilt and at both of its limits, zoomed out, and from 8 directions close up across the floor's edge nearest the camera.
// Depends on: SandboxController, BattleController, BattlefieldView, BattleCameraController, DeploymentInput, Halobellum.Core, UnityEngine.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Halobellum.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Halobellum.Presentation
{
    public sealed class TerrainGallery : MonoBehaviour
    {
        private const string Flag = GameFlow.TerrainGalleryFlag;

        private string _folder;
        private SandboxController _sandbox;
        private BattleController _battle;
        private BattlefieldView _field;
        private int _limit = int.MaxValue;
        private bool _ring;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, Flag) < 0) return;
            SceneManager.sceneLoaded += OnLoaded;
            OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Sandbox")
            {
                SceneManager.LoadScene("Sandbox");
                return;
            }
            var sandbox = FindAnyObjectByType<SandboxController>();
            if (sandbox != null && sandbox.GetComponent<TerrainGallery>() == null) sandbox.gameObject.AddComponent<TerrainGallery>();
        }

        private void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Flag);
            _folder = index + 1 < args.Length ? args[index + 1] : "TerrainShots";
            Directory.CreateDirectory(_folder);
            int limitAt = Array.IndexOf(args, "-terrainMaps");
            if (limitAt >= 0 && limitAt + 1 < args.Length && int.TryParse(args[limitAt + 1], out int limit)) _limit = limit;
            _ring = Array.IndexOf(args, "-terrainRing") >= 0;
            _outdoor = Array.IndexOf(args, "-terrainOutdoor") >= 0;
            int onlyAt = Array.IndexOf(args, "-terrainLook");
            if (onlyAt >= 0 && onlyAt + 1 < args.Length) _only = args[onlyAt + 1];

            _sandbox = GetComponent<SandboxController>();
            _battle = GetComponent<BattleController>();
            _field = GetComponent<BattlefieldView>();
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1f);
            string[] factions = { BattleFactions.Unsc, BattleFactions.Covenant, BattleFactions.Flood };
            int done = 0;
            if (!_outdoor)
            {
                // The interior looks of D80, each with a match that would get it.
                var looks = new (TerrainLook look, string a, string b)[]
                {
                    (TerrainLooks.UnscInstallation, BattleFactions.Unsc, BattleFactions.Covenant),
                    (TerrainLooks.CovenantWarship, BattleFactions.Covenant, BattleFactions.Unsc),
                    (TerrainLooks.FloodOnUnsc, BattleFactions.Unsc, BattleFactions.Flood),
                    (TerrainLooks.FloodOnCovenant, BattleFactions.Flood, BattleFactions.Covenant),
                };
                foreach (var (look, a, b) in looks)
                {
                    if (done >= _limit || _only != null && look.Id != _only) continue;
                    _look = look;
                    yield return _ring ? Ring(a, -1, b, -1, look.Id) : Pictures(a, -1, b, -1, look.Id);
                    done++;
                }
                _look = null;
                Debug.Log("Terrain gallery finished.");
                Application.Quit();
                yield break;
            }
            for (int f = 0; f < factions.Length && done < _limit; f++)
            {
                IReadOnlyList<TerrainLayout> layouts = TerrainLayouts.For(factions[f]);
                for (int l = 0; l < layouts.Count && done < _limit; l++)
                {
                    // The other side: the next faction round, on the layout after, so that every layout is seen on both sides over the whole gallery.
                    string other = factions[(f + 1 + l) % factions.Length];
                    if (other == factions[f]) other = factions[(f + 1) % factions.Length];
                    int otherLayouts = TerrainLayouts.For(other).Count;
                    string name = $"{factions[f]}{l}_{layouts[l].Name.Replace(" ", "")}";
                    yield return _ring ? Ring(factions[f], l, other, (l + f) % otherLayouts, name) : Pictures(factions[f], l, other, (l + f) % otherLayouts, name);
                    done++;
                }
            }
            if (done < _limit && !_ring)
            {
                // A mirror match, and the Flood's dark units on their own ground.
                yield return Pictures(BattleFactions.Flood, 0, BattleFactions.Flood, 1, "FloodMirror");
                yield return Pictures(BattleFactions.Unsc, 1, BattleFactions.Covenant, 1, "UnscAgainstSanctum");
            }
            Debug.Log("Terrain gallery finished.");
            Application.Quit();
        }

        private IEnumerator Pictures(string a, int layoutA, string b, int layoutB, string name)
        {
            _sandbox.ResetBattle();
            _sandbox.Clear(Team.A);
            _sandbox.Clear(Team.B);
            yield return null;
            Show(a, b, layoutA, layoutB);
            yield return new WaitForSecondsRealtime(0.5f);
            Shot(name + "_a_bare");
            yield return new WaitForSecondsRealtime(0.3f);

            Army(Team.A, a);
            Army(Team.B, b);
            yield return new WaitForSecondsRealtime(1f);
            Shot(name + "_b_deployment");
            yield return new WaitForSecondsRealtime(0.3f);

            _battle.Speed = 2f;
            _sandbox.Run();
            float limit = 40f;
            while (limit > 0f && _battle.Simulation != null && _battle.Simulation.ElapsedSeconds < 9f && !_battle.Simulation.IsFinished)
            {
                limit -= Time.unscaledDeltaTime;
                yield return null;
            }
            float speed = _battle.Speed;
            _battle.Speed = 0f;
            yield return new WaitForSecondsRealtime(1.1f); // Let the squares finish fading.
            Shot(name + "_c_battle");
            yield return new WaitForSecondsRealtime(0.3f);
            float started = Time.realtimeSinceStartup;
            int frames = 0;
            while (Time.realtimeSinceStartup - started < 2f)
            {
                frames++;
                yield return null;
            }
            Debug.Log($"Terrain frame time, {name}, at the game's camera: {(Time.realtimeSinceStartup - started) * 1000f / Mathf.Max(1, frames):0.0} ms, {_field.Map.TriangleCount} triangles.");

            Camera view = Camera.main;
            var control = view.GetComponent<BattleCameraController>();
            control.enabled = false;
            view.transform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);

            // Zoomed all the way out, as far as the player can.
            view.transform.SetPositionAndRotation(new Vector3(0f, 197f, -143f), Quaternion.Euler(55f, 0f, 0f));
            yield return null;
            Shot(name + "_d_wide");
            yield return new WaitForSecondsRealtime(0.3f);

            // Halfway in on the fight, at the game's pitch: can one see the units on this ground?
            Vector3 fight = Vector3.zero;
            int fighters = 0;
            foreach (Combat.SimUnit unit in _battle.Simulation.Units)
            {
                if (!unit.IsAlive) continue;
                fight += new Vector3(unit.Position.X, 0f, unit.Position.Y);
                fighters++;
            }
            if (fighters > 0) fight /= fighters;
            view.transform.SetPositionAndRotation(fight + Quaternion.Euler(55f, 0f, 0f) * Vector3.back * 62f, Quaternion.Euler(55f, 0f, 0f));
            yield return null;
            Shot(name + "_h_units");
            yield return new WaitForSecondsRealtime(0.3f);

            // Zoomed in on a corner of the floor and what stands beyond it, on Team B's side.
            view.transform.SetPositionAndRotation(new Vector3(60f, 31f, 56f), Quaternion.Euler(55f, 0f, 0f));
            yield return null;
            Shot(name + "_e_close_far_side");
            yield return new WaitForSecondsRealtime(0.3f);

            // Zoomed in on Team A's side.
            view.transform.SetPositionAndRotation(new Vector3(-58f, 31f, -60f), Quaternion.Euler(55f, 0f, 0f));
            yield return null;
            Shot(name + "_f_close_near_side");
            yield return new WaitForSecondsRealtime(0.3f);

            // Looking low at the horizon. The game's camera never does this today.
            view.transform.SetPositionAndRotation(new Vector3(0f, 28f, -10f), Quaternion.Euler(6f, 0f, 0f));
            yield return null;
            Shot(name + "_g_horizon");
            yield return new WaitForSecondsRealtime(0.3f);

            view.transform.SetPositionAndRotation(position, rotation);
            control.enabled = true;
            _battle.Speed = speed;
        }

        /// <summary>The same map from all round, through the real camera controller.</summary>
        private IEnumerator Ring(string a, int layoutA, string b, int layoutB, string name)
        {
            _sandbox.ResetBattle();
            _sandbox.Clear(Team.A);
            _sandbox.Clear(Team.B);
            yield return null;
            Show(a, b, layoutA, layoutB);
            Army(Team.A, a);
            Army(Team.B, b);
            yield return new WaitForSecondsRealtime(1f);

            Camera view = Camera.main;
            var control = view.GetComponent<BattleCameraController>();
            var input = GetComponent<DeploymentInput>();
            float tilt = BattleCameraController.DefaultPitch;

            // Placing a squad: the ghost must land under the pointer, whichever way the camera faces.
            foreach (SandboxController.SandboxUnit unit in _sandbox.Units)
            {
                if (unit.Faction != a) continue;
                _sandbox.BeginPlacing(unit.Stats);
                break;
            }
            for (int yaw = 0; yaw < 360; yaw += 90)
            {
                control.SetView(Vector2.zero, yaw, tilt, 130f);
                yield return null;
                yield return null;
                // A point towards the back of the player's zone, away from the army that is already there.
                input.PointAt(view.WorldToScreenPoint(new Vector3(30f, 0f, -50f)), false);
                Shot($"{name}_place_y{yaw:000}");
                yield return null;
                yield return null;
            }
            _sandbox.CancelPlacement();

            // Picking an item: the hexagons must stand over their squads from every side.
            _sandbox.BeginPlacingItem(_sandbox.Items[0]);
            for (int yaw = 45; yaw < 360; yaw += 90)
            {
                control.SetView(new Vector2(0f, -30f), yaw, tilt, 70f);
                yield return null;
                yield return null;
                Shot($"{name}_hex_y{yaw:000}");
                yield return null;
                yield return null;
            }
            _sandbox.CancelPlacement();

            _battle.Speed = 2f;
            _sandbox.Run();
            float limit = 40f;
            while (limit > 0f && _battle.Simulation != null && _battle.Simulation.ElapsedSeconds < 9f && !_battle.Simulation.IsFinished)
            {
                limit -= Time.unscaledDeltaTime;
                yield return null;
            }
            float speed = _battle.Speed;
            _battle.Speed = 0f;
            yield return new WaitForSecondsRealtime(1.1f);

            foreach (float t in new[] { tilt, control.MinPitch, control.MaxPitch })
            {
                for (int yaw = 0; yaw < 360; yaw += 45)
                {
                    control.SetView(Vector2.zero, yaw, t, control.MaxDistance);
                    yield return null;
                    Shot($"{name}_wide_p{t:00}_y{yaw:000}");
                    yield return null;
                    yield return null;
                }
            }
            for (int yaw = 0; yaw < 360; yaw += 45)
            {
                // The edge of the floor nearest the camera: where something standing outside it could get in the way.
                float towards = yaw * Mathf.Deg2Rad;
                control.SetView(new Vector2(-Mathf.Sin(towards), -Mathf.Cos(towards)) * 300f, yaw, control.MinPitch, 70f);
                yield return null;
                Shot($"{name}_near_y{yaw:000}");
                yield return null;
                yield return null;
                // Lower and closer, where the camera could stand among the hills.
                control.SetView(new Vector2(-Mathf.Sin(towards), -Mathf.Cos(towards)) * 300f, yaw, control.MinPitch, 45f);
                yield return null;
                Shot($"{name}_low_y{yaw:000}");
                yield return null;
                yield return null;
            }
            // What the turned view costs against the default one, with nothing being saved.
            control.ResetView();
            yield return FrameTime($"{name}, default view");
            control.SetView(Vector2.zero, 180f, control.MinPitch, control.MaxDistance);
            yield return FrameTime($"{name}, from the far side at the lowest tilt, zoomed out");
            control.ResetView();
            _battle.Speed = speed;
        }

        private static IEnumerator FrameTime(string what)
        {
            yield return new WaitForSecondsRealtime(0.5f);
            float started = Time.realtimeSinceStartup;
            int frames = 0;
            while (Time.realtimeSinceStartup - started < 2f)
            {
                frames++;
                yield return null;
            }
            Debug.Log($"Terrain frame time, {what}: {(Time.realtimeSinceStartup - started) * 1000f / Mathf.Max(1, frames):0.0} ms.");
        }

        private void Army(Team team, string faction)
        {
            int added = 0;
            foreach (SandboxController.SandboxUnit unit in _sandbox.Units)
            {
                if (unit.Faction != faction) continue;
                if (unit.Stats.Cost > 700 && added > 1) continue;
                _sandbox.AddSquad(team, unit.Stats);
                if (++added >= 7) break;
            }
        }

        private bool _outdoor;
        private string _only;
        private TerrainLook _look;

        /// <summary>The look being shown, or the outdoor map with these layouts.</summary>
        private void Show(string a, string b, int layoutA, int layoutB)
        {
            if (_look != null) _field.ShowLook(_look, a, b, 12345);
            else _field.ShowOutdoor(a, b, 12345, layoutA, layoutB);
        }

        private void Shot(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(_folder, name + ".png"));
    }
}
