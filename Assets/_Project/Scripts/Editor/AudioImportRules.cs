// AudioImportRules.cs
// Sets how audio files are stored in the game, by what they are, so that nobody has to set them by hand and the owner's files get
// the same treatment by just being dropped in: music is streamed and compressed (Vorbis), stingers compressed but kept in memory, effects and UI
// sounds uncompressed in memory, ready to fire at once. Applies to Audio/Generated and Art/Imported/Audio. The slot is read from the file's name.
// Depends on: UnityEditor.

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Halobellum.EditorTools
{
    public sealed class AudioImportRules : AssetPostprocessor
    {
        private const string Generated = "Assets/_Project/Audio/Generated/";
        private const string Imported = "Assets/_Project/Art/Imported/Audio/";

        public override uint GetVersion() => 2;

        private void OnPreprocessAudio()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(Generated) && !path.StartsWith(Imported)) return;

            var importer = (AudioImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(path);
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            bool streamed = name.StartsWith("music_") || name.StartsWith("stinger_game_"); // The end of a game has a whole song.
            if (streamed)
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.6f;
                settings.preloadAudioData = false;
            }
            else if (name.StartsWith("stinger_"))
            {
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                settings.preloadAudioData = true;
            }
            else
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.preloadAudioData = true;
            }
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = streamed;
        }
    }
}
