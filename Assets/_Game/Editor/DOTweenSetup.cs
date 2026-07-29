using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Watches the project for DOTween and keeps the <c>DOTWEEN</c> scripting define in
    /// sync with it. That define is what switches <see cref="BlockPuzzle.Core.GameTween"/>
    /// over to real tweens, so importing the free DOTween package is the only step the
    /// user has to take — nothing has to be wired up by hand afterwards.
    /// </summary>
    [InitializeOnLoad]
    public static class DOTweenSetup
    {
        private const string Define = "DOTWEEN";

        private static readonly BuildTargetGroup[] TargetGroups =
        {
            BuildTargetGroup.Standalone,
            BuildTargetGroup.Android,
            BuildTargetGroup.iOS
        };

        static DOTweenSetup()
        {
            EditorApplication.delayCall += Sync;
        }

        /// <summary>True when a DOTween assembly can be found in the project.</summary>
        public static bool IsInstalled => HasRuntimeType() || HasAssemblyFile();

        [MenuItem("Tools/Block Puzzle/Refresh DOTween Integration", priority = 60)]
        public static void SyncMenu()
        {
            Sync();
            Debug.Log(IsInstalled
                ? "[Block Puzzle] DOTween found — animations are driven by DOTween."
                : "[Block Puzzle] DOTween is not installed — animations use the built-in fallback. " +
                  "Import DOTween (free) from the Asset Store to switch over automatically.");
        }

        private static void Sync()
        {
            bool installed = IsInstalled;

            foreach (BuildTargetGroup group in TargetGroups)
            {
                ApplyDefine(group, installed);
            }
        }

        private static void ApplyDefine(BuildTargetGroup group, bool installed)
        {
            string current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            var symbols = new List<string>(current.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

            bool present = symbols.Contains(Define);
            if (present == installed)
            {
                return;
            }

            if (installed)
            {
                symbols.Add(Define);
            }
            else
            {
                symbols.Remove(Define);
            }

            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", symbols));
        }

        private static bool HasRuntimeType()
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.GetType("DG.Tweening.DOTween", false) != null)
                    {
                        return true;
                    }
                }
                catch (Exception)
                {
                    // Assemblies that refuse to be inspected simply do not contain DOTween.
                }
            }

            return false;
        }

        /// <summary>
        /// Catches the state right after the import, while the assembly has been written to
        /// disk but the domain has not been reloaded yet.
        /// </summary>
        private static bool HasAssemblyFile()
        {
            return Directory.GetFiles(Application.dataPath, "DOTween.dll", SearchOption.AllDirectories).Length > 0;
        }
    }
}
