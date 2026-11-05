// BattlePicture.cs
// A picture of the whole field from straight above, for the Battle stats window (D69): MatchController takes one as each battle
// starts, with both armies in place, and without the deployment squares. North is the enemy's edge, as on the stats window's map: the picture's left to right is the
// battle's X from -width/2 to width/2, and bottom to top its Y from -length/2 (the player's edge) to length/2.
// The pictures are kept in memory only, and dropped with the match (a new game, or leaving the scene).
// Without a graphics device (a test run with -nographics) there is no picture, and the window draws the field plainly.
// Depends on: UnityEngine.

using UnityEngine;
using UnityEngine.Rendering;

namespace Halobellum.Presentation
{
    public static class BattlePicture
    {
        /// <summary>The picture's width in pixels; its height follows the field.</summary>
        public const int Width = 720;
        private const float Height = 150f;

        /// <summary>Takes the picture now, or returns null if this run can't render.</summary>
        public static Texture2D Take(float fieldWidth, float fieldLength)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || fieldWidth <= 0f || fieldLength <= 0f) return null;
            int width = Width, height = Mathf.RoundToInt(Width * fieldLength / fieldWidth);
            var holder = new GameObject("Battle picture camera");
            RenderTexture target = RenderTexture.GetTemporary(new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 24) { sRGB = true });
            RenderTexture previous = RenderTexture.active;
            bool fog = RenderSettings.fog;
            try
            {
                var camera = holder.AddComponent<Camera>();
                camera.enabled = false;
                Camera main = Camera.main;
                if (main != null) camera.CopyFrom(main); // Its layers and its background.
                holder.transform.SetPositionAndRotation(new Vector3(0f, Height, 0f), Quaternion.Euler(90f, 0f, 0f)); // Looking down, the enemy's edge at the top.
                camera.orthographic = true;
                camera.orthographicSize = fieldLength * 0.5f;
                camera.aspect = fieldWidth / fieldLength;
                camera.nearClipPlane = 1f;
                camera.farClipPlane = Height * 2f;
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                camera.targetTexture = target;
                RenderSettings.fog = false; // From this high up, fog would hide the field.
                BattlefieldView field = Object.FindAnyObjectByType<BattlefieldView>();
                if (field != null) field.WithoutSquares(camera.Render); // The squares are still fading as the battle starts.
                else camera.Render();

                RenderTexture.active = target;
                var picture = new Texture2D(width, height, TextureFormat.RGB24, false) { name = "Battle picture", wrapMode = TextureWrapMode.Clamp };
                picture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                picture.Apply(false, true); // Only the graphics card keeps it.
                return picture;
            }
            finally
            {
                RenderSettings.fog = fog;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(holder);
            }
        }
    }
}
