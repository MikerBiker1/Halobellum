// CardArtImport.cs
// Import settings for the card art (Resources/CardArt, drawn by Tools/UI/make_card_art.py) and the unit icons
// (Resources/UnitIcons, rendered by UnitIconRenderer): UI textures with mipmaps, so that they stay clean at any size on
// any screen. Frames and emblems are not compressed, for clean lines; unit icons are, since there are many of them.
// The layers of a fallback icon stay readable, because UnitIcons paints with them at run time.
// Depends on: UnityEditor.

using UnityEditor;
using UnityEngine;

namespace Halobellum.EditorTools
{
    public sealed class CardArtImport : AssetPostprocessor
    {
        public const string ArtFolder = "Assets/_Project/Resources/CardArt/";
        public const string IconFolder = "Assets/_Project/Resources/UnitIcons/";

        private void OnPreprocessTexture()
        {
            bool art = assetPath.StartsWith(ArtFolder), icon = assetPath.StartsWith(IconFolder);
            if (!art && !icon) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.GUI;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.isReadable = assetPath.Contains("/Layers/");
            importer.textureCompression = icon ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
        }
    }
}
