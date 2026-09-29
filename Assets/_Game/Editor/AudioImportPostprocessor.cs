using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Keeps the import settings of the audio files fixed, so a reimport or a fresh checkout
    /// never resets them. Music is download-size friendly (Vorbis 30 %, decoded on load,
    /// loaded in the background, mono); effects are small, decoded and preloaded with the scene (Vorbis 70 %, mono).
    /// <para>
    /// Every clip is DecompressOnLoad on purpose. On WebGL Unity plays a CompressedInMemory clip larger than
    /// 128 KB through an HTML &lt;audio&gt; element, and the browser then shows its media player: in the desktop
    /// media controls and in the Android / iOS notification shade. Yandex Games rejects that (requirements
    /// 1.6.1.6 and 1.6.2.5). A decoded clip plays through the Web Audio API and shows no player.
    /// Music is mono to halve the memory the decoded samples take.
    /// </para>
    /// </summary>
    public sealed class AudioImportPostprocessor : AssetPostprocessor
    {
        private const string MusicFolder = "Assets/_Game/Audio/Music/";
        private const string SfxFolder = "Assets/_Game/Audio/Sfx/";

        /// <summary>Written to the importer once the settings are applied; a file without it is reimported.</summary>
        private const string Stamp = "BlockPuzzle.AudioImport.v3";

        private void OnPreprocessAudio()
        {
            var importer = (AudioImporter)assetImporter;
            bool music = assetPath.StartsWith(MusicFolder);
            if (!music && !assetPath.StartsWith(SfxFolder))
            {
                return;
            }

            var settings = new AudioImporterSampleSettings
            {
                // Never CompressedInMemory or Streaming: see the class summary (system media player on WebGL).
                loadType = AudioClipLoadType.DecompressOnLoad,
                compressionFormat = AudioCompressionFormat.Vorbis,
                quality = music ? 0.3f : 0.7f,
                sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
                // Effects are tiny: decode them with the scene, so the first play does not stall on a load.
                preloadAudioData = !music
            };

            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.loadInBackground = music;
            importer.userData = Stamp;

            // A platform override would silently win over the defaults above.
            importer.ClearSampleSettingOverride("WebGL");
        }

        /// <summary>Files imported before this script existed get the settings on the next editor load.</summary>
        [InitializeOnLoadMethod]
        private static void ReimportUnstamped()
        {
            EditorApplication.delayCall += () =>
            {
                var stale = new List<string>();
                foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { MusicFolder.TrimEnd('/'), SfxFolder.TrimEnd('/') }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is AudioImporter importer && importer.userData != Stamp)
                    {
                        stale.Add(path);
                    }
                }

                foreach (string path in stale)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            };
        }
    }
}
