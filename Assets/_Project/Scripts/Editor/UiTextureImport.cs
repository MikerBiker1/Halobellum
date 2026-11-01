// UiTextureImport.cs
// Import settings for the faction UI textures in Art/UI (decision D28; drawn by
// Tools/UiArt/make_ui_textures.py): legacy GUI textures, no mipmaps, no compression (crisp lines and
// clean glows), bilinear filtering. Textures whose name ends in _Tile repeat (hull plating tiled over
// panels, bar segments); the others are clamped (9-slice frames, buttons, ornaments).
// Depends on: UnityEditor.

using UnityEditor;
using UnityEngine;

namespace Halobellum.EditorTools
{
    public sealed class UiTextureImport : AssetPostprocessor
    {
        public const string Folder = "Assets/_Project/Art/UI/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.GUI;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            bool tiles = System.IO.Path.GetFileNameWithoutExtension(assetPath).EndsWith("_Tile");
            importer.wrapMode = tiles ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        }
    }
}
