// TerrainGallery.cs
// Test tool for built games: goes through every faction and every layout at the game's camera and saves pictures, so that a person can judge the terrain without playing. It does nothing unless the game is started with
// "-terrainGallery <folder>". For each map it takes a picture with bare ground, one with armies placed during deployment, one in battle, a wide one zoomed out, a close one, and one looking low at the horizon, and logs the triangle count and build time.
// Add "-terrainMaps 2" to only do that many maps, for a quick look.
// Depends on: SandboxController, BattleController, BattlefieldView, Halobellum.Core, UnityEngine.

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
        private const string Flag = "-terrainGallery";

        private string _folder;
        private SandboxController _sandbox;
        private BattleController _battle;
        private BattlefieldView _field;
        private int _limit = int.MaxValue;

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
            for (int f = 0; f < factions.Length && done < _limit; f++)
            {
                IReadOnlyList<TerrainLayout> layouts = TerrainLayouts.For(factions[f]);
                for (int l = 0; l < layouts.Count && done < _limit; l++)
                {
                    // The other side: the next faction round, on the layout after, so that every layout is seen on both sides over the whole gallery.
                    string other = factions[(f + 1 + l) % factions.Length];
                    if (other == factions[f]) other = factions[(f + 1) % factions.Length];
                    int otherLayouts = TerrainLayouts.For(other).Count;
                    yield return Pictures(factions[f], l, other, (l + f) % otherLayouts, $"{factions[f]}{l}_{layouts[l].Name.Replace(" ", "")}");
                    done++;
                }
            }
            if (done < _limit)
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
            _field.ShowExactly(a, b, 12345, layoutA, layoutB);
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

        private void Shot(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(_folder, name + ".png"));
    }
}
