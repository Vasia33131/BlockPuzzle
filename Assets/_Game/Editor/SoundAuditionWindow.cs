using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using BlockPuzzle.Core;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Lets the sound effects be chosen by ear: lists every <see cref="SoundBank"/> event with a play
    /// button for its current clip, and a drop-down of the candidates under
    /// <c>Audio/Candidates</c> to swap the clip in one click. Also makes sure the bank asset exists.
    /// </summary>
    public sealed class SoundAuditionWindow : EditorWindow
    {
        private const string BankPath = "Assets/_Game/Resources/SoundBank.asset";
        private const string SfxFolder = "Assets/_Game/Audio/Sfx";
        private const string CandidatesFolder = "Assets/_Game/Audio/Candidates";

        private SoundBank bank;
        private SerializedObject serialized;
        private string[] candidatePaths = new string[0];
        private string[] candidateLabels = new string[0];
        private Vector2 scroll;

        [MenuItem("Tools/Block Puzzle/Sound Audition")]
        private static void Open()
        {
            GetWindow<SoundAuditionWindow>("Sound Audition").minSize = new Vector2(560f, 320f);
        }

        /// <summary>Creates the bank on the first editor load and fills the empty slots from <c>Audio/Sfx</c> by name.</summary>
        [InitializeOnLoadMethod]
        private static void EnsureBankAsset()
        {
            EditorApplication.delayCall += () =>
            {
                SoundBank asset = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
                if (asset == null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(BankPath));
                    asset = CreateInstance<SoundBank>();
                    AssetDatabase.CreateAsset(asset, BankPath);
                }

                bool changed = asset.AddMissingEntries();
                foreach (SoundEntry entry in asset.Entries)
                {
                    if (entry.Clip != null || string.IsNullOrEmpty(entry.DefaultClipName))
                    {
                        continue;
                    }

                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxFolder}/{entry.DefaultClipName}.ogg");
                    if (clip != null)
                    {
                        entry.Clip = clip;
                        changed = true;
                    }
                }

                if (changed)
                {
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssets();
                }
            };
        }

        private void OnEnable() => Refresh();

        private void OnDisable() => StopPreview();

        private void OnFocus() => Refresh();

        private void Refresh()
        {
            bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
            serialized = bank != null ? new SerializedObject(bank) : null;

            var paths = new List<string>();
            if (AssetDatabase.IsValidFolder(CandidatesFolder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { CandidatesFolder }))
                {
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));
                }
            }

            paths.Sort(StringComparer.OrdinalIgnoreCase);
            candidatePaths = paths.ToArray();
            candidateLabels = new string[paths.Count];
            for (int i = 0; i < paths.Count; i++)
            {
                candidateLabels[i] = paths[i].Substring(CandidatesFolder.Length + 1);
            }
        }

        private void OnGUI()
        {
            if (serialized == null || bank == null)
            {
                EditorGUILayout.HelpBox($"No sound bank at {BankPath}.", MessageType.Warning);
                if (GUILayout.Button("Create / refresh"))
                {
                    EnsureBankAsset();
                    EditorApplication.delayCall += Refresh;
                }

                return;
            }

            serialized.Update();
            SerializedProperty entries = serialized.FindProperty("entries");

            EditorGUILayout.LabelField($"{entries.arraySize} events, {candidatePaths.Length} candidates", EditorStyles.miniLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < entries.arraySize; i++)
            {
                DrawEntry(entries.GetArrayElementAtIndex(i));
            }

            EditorGUILayout.EndScrollView();
            serialized.ApplyModifiedProperties();
        }

        private void DrawEntry(SerializedProperty entry)
        {
            SerializedProperty id = entry.FindPropertyRelative("Id");
            SerializedProperty clipProp = entry.FindPropertyRelative("Clip");
            SerializedProperty volume = entry.FindPropertyRelative("Volume");
            SerializedProperty spread = entry.FindPropertyRelative("PitchSpread");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(id.enumDisplayNames[id.enumValueIndex], EditorStyles.boldLabel, GUILayout.Width(130f));

            var clip = clipProp.objectReferenceValue as AudioClip;
            using (new EditorGUI.DisabledScope(clip == null))
            {
                if (GUILayout.Button("▶", GUILayout.Width(32f)))
                {
                    Play(clip);
                }
            }

            EditorGUILayout.PropertyField(clipProp, GUIContent.none);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Candidate", GUILayout.Width(130f));
            int current = clip != null ? Array.IndexOf(candidatePaths, AssetDatabase.GetAssetPath(clip)) : -1;
            int picked = EditorGUILayout.Popup(current, candidateLabels);
            if (picked != current && picked >= 0)
            {
                var candidate = AssetDatabase.LoadAssetAtPath<AudioClip>(candidatePaths[picked]);
                clipProp.objectReferenceValue = candidate;
                Play(candidate);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Volume", GUILayout.Width(130f));
            volume.floatValue = EditorGUILayout.Slider(volume.floatValue, 0f, 1f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Pitch spread ±", GUILayout.Width(130f));
            spread.floatValue = EditorGUILayout.Slider(spread.floatValue, 0f, 0.3f);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private static void Play(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            StopPreview();
            Type util = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            MethodInfo method = util?.GetMethod(
                "PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(AudioClip), typeof(int), typeof(bool) },
                null);
            method?.Invoke(null, new object[] { clip, 0, false });
        }

        private static void StopPreview()
        {
            Type util = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            util?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
        }
    }
}
