// TerrainCanvas.cs
// What a terrain layout draws on (M10). It holds the three meshes (solid things, flat paint, added light), the height of the land, and the control map: a soft
// picture of which ground lies where and how shaded it is. It also holds the rule that protects the fighting floor: Stand refuses any place where a solid thing would be on
// the arena or could hide part of it from the camera's default view.
// Points on the map are in world metres. For a layout, "v" means metres from the middle line towards its own side, so one layout works on either side.
// Looks only. Depends on: TerrainMeshBuilder, TerrainNoise, UnityEngine.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Halobellum.Presentation
{
    /// <summary>
    /// Maps world metres along one axis to a row of the control map. Most of the texture goes to the arena and the land just around it; the rest stretches out to the far land.
    /// </summary>
    public sealed class TerrainAxis
    {
        public const float FarReach = 1500f;
        private const float NearShare = 0.8f;
        private readonly float _near;

        /// <summary>The world position of each texel.</summary>
        public readonly float[] World;

        public TerrainAxis(float near, int resolution)
        {
            _near = near;
            World = new float[resolution];
            for (int i = 0; i < resolution; i++) World[i] = ToWorld((i + 0.5f) / resolution);
        }

        public float ToWorld(float u)
        {
            float t = u * 2f - 1f;
            float a = Mathf.Abs(t);
            float world;
            if (a <= NearShare) world = a / NearShare * _near;
            else
            {
                float f = (a - NearShare) / (1f - NearShare);
                world = _near + f * f * (FarReach - _near);
            }
            return t < 0f ? -world : world;
        }

        public float ToU(float world)
        {
            float a = Mathf.Abs(world);
            float t;
            if (a <= _near) t = a / _near * NearShare;
            else t = NearShare + Mathf.Sqrt(Mathf.Clamp01((a - _near) / (FarReach - _near))) * (1f - NearShare);
            return (world < 0f ? -t : t) * 0.5f + 0.5f;
        }
    }

    public sealed class TerrainCanvas
    {
        public const float DecalHeight = -0.04f;
        public const float GroundHeight = -0.06f;
        public const float GlowHeight = -0.035f;
        /// <summary>A solid thing's whole footprint keeps this far from the arena's edge.</summary>
        public const float ArenaMargin = 1.2f;
        /// <summary>South of the arena, a thing this tall must stand back by this much per metre of height, so that it can't hide the field from the camera's default view.</summary>
        public const float SouthLean = 0.75f;

        public readonly float HalfWidth, HalfLength, ZoneDepth, Gap;
        public readonly TerrainMeshBuilder Solid = new TerrainMeshBuilder();
        public readonly TerrainMeshBuilder Decals = new TerrainMeshBuilder();
        public readonly TerrainMeshBuilder Glow = new TerrainMeshBuilder();

        // The control map.
        public readonly int Resolution;
        public readonly TerrainAxis AxisX, AxisZ;
        public readonly float[] Second, Third, Shade;

        /// <summary>The side the layout now drawing belongs to: -1 for Team A (south), +1 for Team B (north).</summary>
        public int Side { get; set; }
        /// <summary>Which way shadows fall on the ground, per metre of height.</summary>
        public Vector2 ShadowPerMetre { get; set; }

        private readonly Func<float, float, float> _height;
        private readonly List<Vector3> _occupied = new List<Vector3>(); // x, z, radius.

        public TerrainCanvas(float halfWidth, float zoneDepth, float gap, int resolution, Func<float, float, float> height)
        {
            HalfWidth = halfWidth;
            ZoneDepth = zoneDepth;
            Gap = gap;
            HalfLength = zoneDepth + gap * 0.5f;
            Resolution = resolution;
            _height = height;
            AxisX = new TerrainAxis(halfWidth + 30f, resolution);
            AxisZ = new TerrainAxis(HalfLength + 28f, resolution);
            Second = new float[resolution * resolution];
            Third = new float[resolution * resolution];
            Shade = new float[resolution * resolution];
            for (int i = 0; i < Shade.Length; i++) Shade[i] = 1f;
        }

        public float GroundAt(float x, float z) => _height(x, z);

        /// <summary>World z of a distance from the middle line on the side now drawing.</summary>
        public float Z(float v) => Side * v;

        /// <summary>The edge of the arena, measured from the middle line.</summary>
        public float EdgeV => HalfLength;

        // ------------------------------------------------------------------ where solid things may stand

        /// <summary>True if a thing of this footprint and height stands clear of the arena and can't hide any of it. Doesn't reserve the place.</summary>
        public bool IsClear(float x, float z, float radius, float height)
        {
            float reach = radius + ArenaMargin;
            bool overFloorX = Mathf.Abs(x) < HalfWidth + reach;
            if (overFloorX && Mathf.Abs(z) < HalfLength + reach) return false;
            // At its default view the camera stands inside the arena's width and south of what it looks at, so only things to the south of the floor, in line with it, can hide any of it.
            // From other sides the terrain's shaders leave out what hides the floor (KeepFloorInView), so nothing else need stand back.
            if (z < -HalfLength && overFloorX && -z - HalfLength - radius < height * SouthLean + 0.3f) return false;
            foreach (Vector3 other in _occupied)
            {
                float dx = other.x - x, dz = other.y - z, sum = other.z + radius;
                if (dx * dx + dz * dz < sum * sum) return false;
            }
            return true;
        }

        /// <summary>Reserves a place for a thing if it is clear, and darkens the ground around it and in the direction its shadow falls. False if it can't stand there.</summary>
        public bool Stand(float x, float z, float radius, float height, float shadow = 0.5f)
        {
            if (!IsClear(x, z, radius, height)) return false;
            _occupied.Add(new Vector3(x, z, radius));
            if (shadow > 0f) CastShadow(x, z, radius, height, shadow);
            return true;
        }

        /// <summary>Puts the solid mesh's next pieces on the ground here.</summary>
        public void PlaceSolid(float x, float z, float yaw = 0f, float scale = 1f, float sink = 0f) =>
            Solid.Place(new Vector3(x, GroundAt(x, z) - sink, z), yaw, scale);

        // ------------------------------------------------------------------ the control map

        /// <summary>Paints a layer (0, 1 or 2) over a soft disc.</summary>
        public void PaintDisc(float x, float z, float radius, int layer, float amount = 1f)
        {
            Each(x - radius, x + radius, z - radius, z + radius, (wx, wz, index) =>
            {
                float d = Mathf.Sqrt((wx - x) * (wx - x) + (wz - z) * (wz - z)) / radius;
                if (d < 1f) Paint(index, layer, amount * TerrainNoise.Smooth(1f, 0.55f, d));
            });
        }

        /// <summary>Paints a layer along a line with soft sides: a road, a track.</summary>
        public void PaintLine(float ax, float az, float bx, float bz, float width, int layer, float amount = 1f)
        {
            float half = width * 0.5f;
            float dx = bx - ax, dz = bz - az;
            float lengthSquared = Mathf.Max(0.0001f, dx * dx + dz * dz);
            Each(Mathf.Min(ax, bx) - half, Mathf.Max(ax, bx) + half, Mathf.Min(az, bz) - half, Mathf.Max(az, bz) + half, (wx, wz, index) =>
            {
                float t = Mathf.Clamp01(((wx - ax) * dx + (wz - az) * dz) / lengthSquared);
                float ox = ax + dx * t - wx, oz = az + dz * t - wz;
                float d = Mathf.Sqrt(ox * ox + oz * oz) / half;
                if (d < 1f) Paint(index, layer, amount * TerrainNoise.Smooth(1f, 0.5f, d));
            });
        }

        /// <summary>Darkens a soft disc of ground. amount 1 is black at its middle.</summary>
        public void Darken(float x, float z, float radius, float amount)
        {
            Each(x - radius, x + radius, z - radius, z + radius, (wx, wz, index) =>
            {
                float d = Mathf.Sqrt((wx - x) * (wx - x) + (wz - z) * (wz - z)) / radius;
                if (d < 1f) Shade[index] *= 1f - amount * TerrainNoise.Smooth(1f, 0f, d);
            });
        }

        /// <summary>A soft shadow from a thing's foot in the direction the sun throws it, as long as its height says. No real shadows are drawn: this is painted in once.</summary>
        public void CastShadow(float x, float z, float radius, float height, float amount)
        {
            float bx = x + ShadowPerMetre.x * height, bz = z + ShadowPerMetre.y * height;
            float reach = radius * 1.35f + 1f;
            float dx = bx - x, dz = bz - z;
            float lengthSquared = Mathf.Max(0.0001f, dx * dx + dz * dz);
            Each(Mathf.Min(x, bx) - reach, Mathf.Max(x, bx) + reach, Mathf.Min(z, bz) - reach, Mathf.Max(z, bz) + reach, (wx, wz, index) =>
            {
                float t = Mathf.Clamp01(((wx - x) * dx + (wz - z) * dz) / lengthSquared);
                float ox = x + dx * t - wx, oz = z + dz * t - wz;
                float d = Mathf.Sqrt(ox * ox + oz * oz) / (reach * (1f - 0.35f * t));
                if (d < 1f) Shade[index] *= 1f - amount * (1f - 0.45f * t) * TerrainNoise.Smooth(1f, 0.25f, d);
            });
        }

        private void Paint(int index, int layer, float amount)
        {
            if (layer == 1)
            {
                Second[index] += (1f - Second[index]) * amount;
                Third[index] -= Third[index] * amount;
            }
            else if (layer == 2) Third[index] += (1f - Third[index]) * amount;
            else
            {
                Second[index] -= Second[index] * amount;
                Third[index] -= Third[index] * amount;
            }
        }

        private void Each(float minX, float maxX, float minZ, float maxZ, Action<float, float, int> action)
        {
            int ix0 = Mathf.Max(0, Mathf.FloorToInt(AxisX.ToU(minX) * Resolution));
            int ix1 = Mathf.Min(Resolution - 1, Mathf.CeilToInt(AxisX.ToU(maxX) * Resolution));
            int iz0 = Mathf.Max(0, Mathf.FloorToInt(AxisZ.ToU(minZ) * Resolution));
            int iz1 = Mathf.Min(Resolution - 1, Mathf.CeilToInt(AxisZ.ToU(maxZ) * Resolution));
            for (int iz = iz0; iz <= iz1; iz++)
            {
                float wz = AxisZ.World[iz];
                int row = iz * Resolution;
                for (int ix = ix0; ix <= ix1; ix++) action(AxisX.World[ix], wz, row + ix);
            }
        }

        // ------------------------------------------------------------------ paths

        /// <summary>A wandering path of points from a start, heading a way, turning a bit at each step. Fills points.</summary>
        public static void Wander(List<Vector2> points, TerrainRandom random, Vector2 start, float heading, float step, int steps, float wobble, float drift = 0f)
        {
            points.Clear();
            Vector2 p = start;
            points.Add(p);
            for (int i = 0; i < steps; i++)
            {
                heading += random.Range(-wobble, wobble) + drift;
                p += new Vector2(Mathf.Sin(heading), Mathf.Cos(heading)) * step;
                points.Add(p);
            }
        }

        /// <summary>Cuts a path off where it would leave the arena's floor.</summary>
        public void KeepOnFloor(List<Vector2> points, float inset)
        {
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i];
                if (Mathf.Abs(p.x) > HalfWidth - inset || Mathf.Abs(p.y) > HalfLength - inset)
                {
                    points.RemoveRange(i, points.Count - i);
                    return;
                }
            }
        }
    }
}
