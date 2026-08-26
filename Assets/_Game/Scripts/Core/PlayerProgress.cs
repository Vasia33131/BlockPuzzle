using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Persistent player flags stored in PlayerPrefs. Lives in Core so platform
    /// services can read them without Core taking a dependency on YG.
    ///
    /// PlayerPrefs is only the local cache. Purchases must survive a change of device,
    /// so the platform layer mirrors every change into the Yandex save through
    /// <see cref="Changed"/> and feeds the cloud copy back through <see cref="Restore"/>.
    /// </summary>
    public static class PlayerProgress
    {
        public const string AdsRemovedKey = "BlockPuzzle.AdsRemoved";
        public const string ThemeIdKey = "BlockPuzzle.ThemeId";
        public const string OwnedThemesKey = "BlockPuzzle.OwnedThemes";
        public const string OwnedPacksKey = "BlockPuzzle.OwnedPacks";
        public const string ShapesPack1Id = "shapes_pack_1";

        private static readonly HashSet<string> ownedThemes = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> ownedPacks = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>True when fullscreen ads should no longer be shown.</summary>
        public static bool AdsRemoved { get; private set; }

        /// <summary>Id of the selected <see cref="ThemeConfig"/>. Missing values resolve to the default theme.</summary>
        public static string ThemeId { get; private set; } = ThemeConfig.DefaultId;

        /// <summary>Raised after anything was written, so cloud storage can mirror it.</summary>
        public static event Action Changed;

        /// <summary>Owned paid theme ids, comma separated. Read by the cloud save.</summary>
        public static string OwnedThemesCsv => JoinOwnedIds(ownedThemes);

        /// <summary>Owned paid pack ids, comma separated. Read by the cloud save.</summary>
        public static string OwnedPacksCsv => JoinOwnedIds(ownedPacks);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadOnStartup()
        {
            // Static state outlives a play session when the domain is not reloaded.
            Changed = null;
            Load();
        }

        public static void Load()
        {
            AdsRemoved = PlayerPrefs.GetInt(AdsRemovedKey, 0) == 1;
            ThemeId = NormalizeThemeId(PlayerPrefs.GetString(ThemeIdKey, ThemeConfig.DefaultId));
            ownedThemes.Clear();
            ParseOwnedIds(PlayerPrefs.GetString(OwnedThemesKey, string.Empty), ownedThemes, ThemeConfig.DefaultId);
            ownedPacks.Clear();
            ParseOwnedIds(PlayerPrefs.GetString(OwnedPacksKey, string.Empty), ownedPacks, null);
            if (!OwnsTheme(ThemeId))
            {
                ThemeId = ThemeConfig.DefaultId;
            }
        }

        public static void Save()
        {
            PlayerPrefs.SetInt(AdsRemovedKey, AdsRemoved ? 1 : 0);
            PlayerPrefs.SetString(ThemeIdKey, NormalizeThemeId(ThemeId));
            PlayerPrefs.SetString(OwnedThemesKey, JoinOwnedIds(ownedThemes));
            PlayerPrefs.SetString(OwnedPacksKey, JoinOwnedIds(ownedPacks));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>
        /// Merges what came back from the platform save. Nothing is ever taken away:
        /// a product granted on any device stays granted, so signing in on another
        /// device restores no-ads, the palettes and the figure pack.
        /// </summary>
        public static void Restore(bool adsRemoved, string ownedThemesCsv, string ownedPacksCsv, string themeId)
        {
            bool changed = false;

            if (adsRemoved && !AdsRemoved)
            {
                AdsRemoved = true;
                changed = true;
            }

            changed |= MergeOwnedIds(ownedThemesCsv, ownedThemes, ThemeConfig.DefaultId);
            changed |= MergeOwnedIds(ownedPacksCsv, ownedPacks, null);

            if (!string.IsNullOrEmpty(themeId) && themeId != ThemeId && OwnsTheme(themeId))
            {
                ThemeId = themeId;
                changed = true;
            }

            if (!OwnsTheme(ThemeId))
            {
                ThemeId = ThemeConfig.DefaultId;
                changed = true;
            }

            if (changed)
            {
                Save();
            }
        }

        public static void SetAdsRemoved(bool removed)
        {
            if (AdsRemoved == removed)
            {
                return;
            }

            AdsRemoved = removed;
            Save();
        }

        /// <summary>True for the free default theme and for any theme already granted.</summary>
        public static bool OwnsTheme(string id)
        {
            id = NormalizeThemeId(id);
            return id == ThemeConfig.DefaultId || ownedThemes.Contains(id);
        }

        /// <summary>Marks a paid theme as owned and selects it.</summary>
        public static void GrantTheme(string id)
        {
            id = NormalizeThemeId(id);
            bool changed = id != ThemeConfig.DefaultId && ownedThemes.Add(id);
            if (ThemeId != id && OwnsTheme(id))
            {
                ThemeId = id;
                changed = true;
            }

            if (changed)
            {
                Save();
            }
        }

        public static void SetThemeId(string id)
        {
            id = NormalizeThemeId(id);
            if (!OwnsTheme(id))
            {
                id = ThemeConfig.DefaultId;
            }

            if (ThemeId == id)
            {
                return;
            }

            ThemeId = id;
            Save();
        }

        /// <summary>True when the player has already paid for <paramref name="id"/>.</summary>
        public static bool OwnsPack(string id)
        {
            return !string.IsNullOrEmpty(id) && ownedPacks.Contains(id);
        }

        /// <summary>Marks a paid figure pack as owned. Does not change the default tray until the next draw.</summary>
        public static void GrantPack(string id)
        {
            if (string.IsNullOrEmpty(id) || !ownedPacks.Add(id))
            {
                return;
            }

            Save();
        }

        private static string NormalizeThemeId(string id)
        {
            return string.IsNullOrEmpty(id) ? ThemeConfig.DefaultId : id;
        }

        private static void ParseOwnedIds(string raw, HashSet<string> target, string skipId)
        {
            MergeOwnedIds(raw, target, skipId);
        }

        /// <summary>Adds every id of the list to the set. True when something was new.</summary>
        private static bool MergeOwnedIds(string raw, HashSet<string> target, string skipId)
        {
            if (string.IsNullOrEmpty(raw) || target == null)
            {
                return false;
            }

            bool added = false;
            string[] parts = raw.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string id = parts[i].Trim();
                if (!string.IsNullOrEmpty(id) && id != skipId)
                {
                    added |= target.Add(id);
                }
            }

            return added;
        }

        private static string JoinOwnedIds(HashSet<string> source)
        {
            if (source == null || source.Count == 0)
            {
                return string.Empty;
            }

            var ids = new List<string>(source);
            ids.Sort(StringComparer.Ordinal);
            return string.Join(",", ids);
        }
    }
}
