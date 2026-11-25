// EffectFlipbookImport.cs
// Import settings for the battle effects' flipbooks in Resources/Effects/Flipbooks (the effects pass, before 1.0): smoke, fire, flames and an explosion
// rendered in Houdini by Unity Labs and given away under CC0 (SOURCE.txt in that folder). Each is a grid of frames, top left first.
// Clamped, so that one frame never bleeds into the frame on the far side; colour carried into the see-through texels, so that the edges never darken;
// high-quality compression, mipmaps for the camera far away.
// Depends on: UnityEditor.

using UnityEditor;
using UnityEngine;

namespace Halobellum.EditorTools
{
    public sealed class EffectFlipbookImport : AssetPostprocessor
    {
        public const string Folder = "Assets/_Project/Resources/Effects/Flipbooks/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
