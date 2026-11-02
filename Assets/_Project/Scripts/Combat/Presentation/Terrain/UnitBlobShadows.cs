// UnitBlobShadows.cs
// A soft dark blob on the ground under every living unit (M10), so that units sit on the terrain instead of floating over it. Models cast no real shadows. Air units get a fainter, wider one, thrown a little aside by the sun: it
// also shows where a flyer is over the ground. All blobs share one mesh, rewritten each frame from arrays kept for the purpose, so it is one draw call and allocates nothing during battle.
// Looks only: it reads the simulation and the units' views. Depends on: BattleController, UnitViewManager, BattlefieldView's shader folder, Halobellum.Combat, UnityEngine.

using Halobellum.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace Halobellum.Presentation
{
    public sealed class UnitBlobShadows : MonoBehaviour
    {
        private const float Height = -0.02f;
        private const int Capacity = 2000;

        private BattleController _battle;
        private UnitViewManager _views;
        private Mesh _mesh;
        private Material _material;
        private Vector3[] _positions;
        private Vector3[] _uvs;
        private int _shownLast;

        /// <summary>Blobs drawn last frame (for tests).</summary>
        public int Shown => _shownLast;

        /// <summary>Where the sun throws a flyer's blob, per metre of flying height.</summary>
        public Vector2 ThrowPerMetre { get; set; }

        private void Start()
        {
            _battle = GetComponent<BattleController>();
            _views = GetComponent<UnitViewManager>();
            Shader shader = Resources.Load<Shader>("Terrain/UnitBlob");
            if (shader == null)
            {
                enabled = false;
                return;
            }
            _material = new Material(shader) { name = "Unit Blobs" };

            _positions = new Vector3[Capacity * 4];
            _uvs = new Vector3[Capacity * 4];
            var triangles = new int[Capacity * 6];
            for (int i = 0; i < Capacity; i++)
            {
                int v = i * 4, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            _mesh = new Mesh { name = "Unit Blobs", indexFormat = IndexFormat.UInt32 };
            _mesh.MarkDynamic();
            _mesh.vertices = _positions;
            _mesh.SetUVs(0, _uvs);
            _mesh.triangles = triangles;
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(4000f, 100f, 4000f));

            var part = new GameObject("Unit Blobs");
            part.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void LateUpdate()
        {
            if (_mesh == null) return;
            BattleSimulation simulation = _battle.Simulation;
            int shown = 0;
            if (simulation != null)
            {
                float air = _views.AirHeight;
                var units = simulation.Units;
                for (int i = 0; i < units.Count && shown < Capacity; i++)
                {
                    SimUnit unit = units[i];
                    if (!unit.IsAlive || !_views.TryBodyPosition(unit, out Vector3 at)) continue;
                    float radius = Mathf.Max(0.5f, unit.Radius) * 1.45f;
                    // A pack of tiny forms is dark against the ground already: a dark blob would swallow it.
                    float strength = unit.FormsAlive > 1 ? 0.25f : 1f;
                    if (unit.IsAir)
                    {
                        radius *= 1.35f;
                        strength = 0.5f;
                        at.x += ThrowPerMetre.x * air;
                        at.z += ThrowPerMetre.y * air;
                    }
                    int v = shown * 4;
                    float x0 = at.x - radius, x1 = at.x + radius, z0 = at.z - radius, z1 = at.z + radius;
                    _positions[v] = new Vector3(x0, Height, z0);
                    _positions[v + 1] = new Vector3(x0, Height, z1);
                    _positions[v + 2] = new Vector3(x1, Height, z1);
                    _positions[v + 3] = new Vector3(x1, Height, z0);
                    _uvs[v] = new Vector3(-1f, -1f, strength);
                    _uvs[v + 1] = new Vector3(-1f, 1f, strength);
                    _uvs[v + 2] = new Vector3(1f, 1f, strength);
                    _uvs[v + 3] = new Vector3(1f, -1f, strength);
                    shown++;
                }
            }
            // Fold away the blobs no longer used.
            for (int i = shown; i < _shownLast; i++)
            {
                int v = i * 4;
                _positions[v] = _positions[v + 1] = _positions[v + 2] = _positions[v + 3] = Vector3.zero;
            }
            _shownLast = shown;
            _mesh.SetVertices(_positions, 0, _positions.Length, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
            _mesh.SetUVs(0, _uvs, 0, _uvs.Length, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }
    }
}
