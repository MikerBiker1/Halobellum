// EffectAssets.cs
// The textures, meshes and materials of the battle effects (M10), all made in code, so no art files are needed and nothing is
// borrowed from anywhere. Made once on first use and kept for the whole run of the game, so changing scenes costs nothing.
// Shaders live in Resources/Effects, so that they are included in builds.
// Depends on: UnityEngine.

using UnityEngine;

namespace Halobellum.Presentation
{
    public static class EffectAssets
    {
        private static Texture2D _soft, _streak, _smoke, _flame, _ring, _scorch, _slash, _heat, _beam;
        private static Mesh[] _chunks;
        private static Mesh _flatQuad;

        /// <summary>A soft round glow.</summary>
        public static Texture2D Soft => _soft != null ? _soft : (_soft = MakeSoft(64));
        /// <summary>A long thin glow with a hot middle, for streaks and sparks.</summary>
        public static Texture2D Streak => _streak != null ? _streak : (_streak = MakeStreak(32, 96));
        /// <summary>Two by two soft puffs of smoke.</summary>
        public static Texture2D Smoke => _smoke != null ? _smoke : (_smoke = MakeSheet(256, 2, 0.55f, 3.2f, "Effect Smoke"));
        /// <summary>Two by two torn licks of flame.</summary>
        public static Texture2D Flame => _flame != null ? _flame : (_flame = MakeSheet(256, 2, 1.0f, 5f, "Effect Flame"));
        /// <summary>A thin bright ring with a soft inside, for shock waves.</summary>
        public static Texture2D Ring => _ring != null ? _ring : (_ring = MakeRing(256));
        /// <summary>A blotchy dark burn mark.</summary>
        public static Texture2D Scorch => _scorch != null ? _scorch : (_scorch = MakeScorch(128));
        /// <summary>The crescent of a quick slash.</summary>
        public static Texture2D Slash => _slash != null ? _slash : (_slash = MakeSlash(128));
        /// <summary>Noise for heat shimmer, in a soft round mask.</summary>
        public static Texture2D Heat => _heat != null ? _heat : (_heat = MakeHeat(64));
        /// <summary>Across a beam: soft at the edges, white in the middle.</summary>
        public static Texture2D Beam => _beam != null ? _beam : (_beam = MakeBeam(8, 64));

        /// <summary>A few rough lumps, for debris and gore.</summary>
        public static Mesh[] Chunks
        {
            get
            {
                if (_chunks != null && _chunks.Length > 0 && _chunks[0] != null) return _chunks;
                _chunks = new[] { MakeChunk(11, 0.5f, 1f, 0.4f), MakeChunk(23, 0.9f, 0.3f, 0.5f), MakeChunk(37, 0.3f, 0.35f, 1f), MakeChunk(53, 0.7f, 0.7f, 0.7f) };
                return _chunks;
            }
        }

        /// <summary>A square of size 1 lying flat on the ground, facing up.</summary>
        public static Mesh FlatQuad
        {
            get
            {
                if (_flatQuad != null) return _flatQuad;
                _flatQuad = new Mesh { name = "Effect Flat Quad", hideFlags = HideFlags.DontSave };
                _flatQuad.SetVertices(new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f) });
                _flatQuad.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
                _flatQuad.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
                _flatQuad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
                _flatQuad.RecalculateBounds();
                return _flatQuad;
            }
        }

        /// <summary>A new material for one layer of particles. renderQueue decides what is drawn over what.</summary>
        public static Material ParticleMaterial(string name, Texture texture, float boost, float occlude, float softFade, int renderQueue)
        {
            Material material = new Material(FindShader("Halobellum/EffectParticle")) { name = name, renderQueue = renderQueue };
            material.mainTexture = texture;
            material.SetFloat("_Boost", boost);
            material.SetFloat("_Occlude", occlude);
            material.SetFloat("_SoftFade", softFade);
            return material;
        }

        public static Material ChunkMaterial() => new Material(FindShader("Halobellum/EffectChunk")) { name = "Effect Chunks" };

        public static Material HeatMaterial(int renderQueue)
        {
            Material material = new Material(FindShader("Halobellum/EffectHeat")) { name = "Effect Heat", renderQueue = renderQueue };
            material.mainTexture = Heat;
            return material;
        }

        private static Shader FindShader(string name)
        {
            Shader shader = Shader.Find(name);
            if (shader != null) return shader;
            Debug.LogWarning($"Effect shader {name} is missing; effects will look plain.");
            return Shader.Find("Sprites/Default");
        }

        // ------------------------------------------------------------------ textures

        private static Texture2D NewTexture(string name, int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
        }

        private static Texture2D MakeSoft(int size)
        {
            Texture2D texture = NewTexture("Effect Soft", size, size);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = Radius(x, y, size);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a * a * 0.5f + a * 0.5f);
                }
            }
            return Finish(texture, pixels);
        }

        private static Texture2D MakeStreak(int width, int height)
        {
            Texture2D texture = NewTexture("Effect Streak", width, height);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float along = (y + 0.5f) / height * 2f - 1f;
                for (int x = 0; x < width; x++)
                {
                    float across = (x + 0.5f) / width * 2f - 1f;
                    float a = Mathf.Clamp01(1f - across * across) * Mathf.Clamp01(1f - along * along * along * along);
                    float core = Mathf.Clamp01(1f - Mathf.Abs(across) * 2.6f);
                    pixels[y * width + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a * a * 0.6f + core * Mathf.Clamp01(1f - Mathf.Abs(along))));
                }
            }
            return Finish(texture, pixels);
        }

        /// <summary>A sheet of tiles x tiles noisy puffs. erosion above 0.7 tears the edges into licks of flame.</summary>
        private static Texture2D MakeSheet(int size, int tiles, float erosion, float frequency, string name)
        {
            Texture2D texture = NewTexture(name, size, size);
            var pixels = new Color[size * size];
            int tile = size / tiles;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int tx = x / tile, ty = y / tile;
                    float u = (x % tile + 0.5f) / tile, v = (y % tile + 0.5f) / tile;
                    float dx = u * 2f - 1f, dy = v * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float seed = (tx + ty * tiles) * 17.3f + 3.7f;
                    float noise = Fbm(u * frequency + seed, v * frequency + seed * 1.7f, 4);
                    float edge = Mathf.Clamp01(1f - r);
                    float shape = Mathf.Clamp01(edge * (1f + erosion) - (1f - noise) * erosion);
                    shape = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(shape * 2.2f));
                    float shade = Mathf.Lerp(0.72f, 1f, noise);
                    pixels[y * size + x] = new Color(shade, shade, shade, shape);
                }
            }
            return Finish(texture, pixels);
        }

        private static Texture2D MakeRing(int size)
        {
            Texture2D texture = NewTexture("Effect Ring", size, size);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = Radius(x, y, size);
                    float band = Mathf.Clamp01(1f - Mathf.Abs(r - 0.93f) / 0.07f);
                    float inside = r < 0.93f ? Mathf.Pow(r / 0.93f, 3f) * 0.3f : 0f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(band * band + inside));
                }
            }
            return Finish(texture, pixels);
        }

        private static Texture2D MakeScorch(int size)
        {
            Texture2D texture = NewTexture("Effect Scorch", size, size);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = Radius(x, y, size);
                    float u = (float)x / size, v = (float)y / size;
                    float noise = Fbm(u * 6f + 9.1f, v * 6f + 2.3f, 4);
                    float a = Mathf.Clamp01((1f - r) * 1.7f + (noise - 0.55f) * 1.1f);
                    a = Mathf.SmoothStep(0f, 1f, a);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a * Mathf.Lerp(0.8f, 1f, noise));
                }
            }
            return Finish(texture, pixels);
        }

        private static Texture2D MakeSlash(int size)
        {
            Texture2D texture = NewTexture("Effect Slash", size, size);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx); // Sweeps from -100 to +100 degrees, thickest in the middle.
                    float sweep = Mathf.Clamp01(1f - Mathf.Abs(angle) / 1.75f);
                    float thickness = 0.03f + 0.1f * sweep;
                    float band = Mathf.Clamp01(1f - Mathf.Abs(r - 0.72f) / thickness);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(band * band * sweep * 2f));
                }
            }
            return Finish(texture, pixels);
        }

        private static Texture2D MakeHeat(int size)
        {
            Texture2D texture = NewTexture("Effect Heat", size, size);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size * 5f, v = (float)y / size * 5f;
                    float mask = Mathf.Clamp01(1f - Radius(x, y, size));
                    pixels[y * size + x] = new Color(Fbm(u, v, 2), Fbm(u + 31.7f, v + 11.3f, 2), 0.5f, mask * mask);
                }
            }
            return Finish(texture, pixels);
        }

        private static Texture2D MakeBeam(int width, int height)
        {
            Texture2D texture = NewTexture("Effect Beam", width, height);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float across = Mathf.Abs((y + 0.5f) / height * 2f - 1f);
                float a = Mathf.Clamp01(1f - across);
                a = a * a * 0.55f + Mathf.Clamp01(1f - across * 3f) * 0.45f;
                for (int x = 0; x < width; x++) pixels[y * width + x] = new Color(1f, 1f, 1f, a);
            }
            return Finish(texture, pixels);
        }

        private static float Radius(int x, int y, int size)
        {
            float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, weight = 0.5f, total = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x, y) * weight;
                total += weight;
                weight *= 0.5f;
                x = x * 2.03f + 13.1f;
                y = y * 2.03f + 7.7f;
            }
            return sum / total;
        }

        private static Texture2D Finish(Texture2D texture, Color[] pixels)
        {
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        // ------------------------------------------------------------------ meshes

        /// <summary>A rough lump: a box with its corners pushed about, with hard edges. About 1 unit across, stretched by x, y and z.</summary>
        private static Mesh MakeChunk(int seed, float x, float y, float z)
        {
            var random = new System.Random(seed);
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                float px = ((i & 1) == 0 ? -0.5f : 0.5f) * x * (0.6f + (float)random.NextDouble() * 0.8f);
                float py = ((i & 2) == 0 ? -0.5f : 0.5f) * y * (0.6f + (float)random.NextDouble() * 0.8f);
                float pz = ((i & 4) == 0 ? -0.5f : 0.5f) * z * (0.6f + (float)random.NextDouble() * 0.8f);
                corners[i] = new Vector3(px, py, pz);
            }
            int[,] faces = { { 0, 2, 3, 1 }, { 4, 5, 7, 6 }, { 0, 1, 5, 4 }, { 2, 6, 7, 3 }, { 0, 4, 6, 2 }, { 1, 3, 7, 5 } };
            var vertices = new Vector3[24];
            var triangles = new int[36];
            for (int f = 0; f < 6; f++)
            {
                for (int c = 0; c < 4; c++) vertices[f * 4 + c] = corners[faces[f, c]];
                int t = f * 6, v = f * 4;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = "Effect Chunk", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
