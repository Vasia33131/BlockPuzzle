using System;
using UnityEngine;

namespace BlockPuzzle.Core
{
    /// <summary>Every sound effect event of the game; the bank maps each one to a clip.</summary>
    public enum SfxId
    {
        UiClick,
        UiOpen,
        UiClose,
        PiecePickup,
        PiecePlace,
        PieceInvalid,
        LineClear,
        LineClearMulti,
        Combo,
        BoardClear,
        BoosterUse,
        Coin,
        JingleNewRecord,
        JingleGameOver,
        JingleWin
    }

    /// <summary>One event of the <see cref="SoundBank"/>: which clip, how loud, how much the pitch wanders.</summary>
    [Serializable]
    public sealed class SoundEntry
    {
        public SfxId Id;
        public AudioClip Clip;

        [Range(0f, 1f)] public float Volume = 1f;

        [Tooltip("The pitch of every play is multiplied by a random value within 1 ± this.")]
        [Range(0f, 0.3f)] public float PitchSpread;

        public SoundEntry()
        {
        }

        public SoundEntry(SfxId id, string clipName, float volume, float pitchSpread)
        {
            Id = id;
            Volume = volume;
            PitchSpread = pitchSpread;
            DefaultClipName = clipName;
        }

        /// <summary>File name (without extension) in Audio/Sfx that the editor fills in when <see cref="Clip"/> is empty.</summary>
        [HideInInspector] public string DefaultClipName;
    }

    /// <summary>
    /// Sound effect settings kept in an asset (<c>Resources/SoundBank</c>), so a clip, its volume or its
    /// pitch spread can be changed in the inspector without touching code.
    /// </summary>
    [CreateAssetMenu(menuName = "Block Puzzle/Sound Bank", fileName = "SoundBank")]
    public sealed class SoundBank : ScriptableObject
    {
        public const string ResourceName = "SoundBank";

        [SerializeField] private SoundEntry[] entries = CreateDefaultEntries();

        private static SoundBank cached;

        public SoundEntry[] Entries => entries;

        /// <summary>The bank from Resources; a runtime copy with the default values when the asset is missing.</summary>
        public static SoundBank Load()
        {
            if (cached == null)
            {
                cached = Resources.Load<SoundBank>(ResourceName);
                if (cached == null)
                {
                    cached = CreateInstance<SoundBank>();
                }
            }

            return cached;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => cached = null;

        /// <summary>The entry of <paramref name="id"/>, or null when the bank lacks it.</summary>
        public SoundEntry Get(SfxId id)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null && entries[i].Id == id)
                {
                    return entries[i];
                }
            }

            return null;
        }

        /// <summary>Adds the events an older asset does not have yet, without touching the ones it has.</summary>
        public bool AddMissingEntries()
        {
            SoundEntry[] defaults = CreateDefaultEntries();
            var list = new System.Collections.Generic.List<SoundEntry>(entries ?? new SoundEntry[0]);
            bool changed = false;
            for (int i = 0; i < defaults.Length; i++)
            {
                SfxId id = defaults[i].Id;
                if (!list.Exists(e => e != null && e.Id == id))
                {
                    list.Add(defaults[i]);
                    changed = true;
                }
            }

            entries = list.ToArray();
            return changed;
        }

        private static SoundEntry[] CreateDefaultEntries()
        {
            return new[]
            {
                new SoundEntry(SfxId.UiClick, "Ui_Click", 1f, 0f),
                new SoundEntry(SfxId.UiOpen, "Ui_Open", 1f, 0f),
                new SoundEntry(SfxId.UiClose, "Ui_Close", 1f, 0f),
                new SoundEntry(SfxId.PiecePickup, "Piece_Pickup", 1f, 0f),
                new SoundEntry(SfxId.PiecePlace, "Piece_Place", 1f, 0.06f),
                new SoundEntry(SfxId.PieceInvalid, "Piece_Invalid", 0.5f, 0f),
                new SoundEntry(SfxId.LineClear, "Line_Clear", 1f, 0f),
                new SoundEntry(SfxId.LineClearMulti, "Line_Clear_Multi", 1f, 0f),
                new SoundEntry(SfxId.Combo, "Combo", 1f, 0f),
                new SoundEntry(SfxId.BoardClear, "Board_Clear", 1f, 0f),
                new SoundEntry(SfxId.BoosterUse, "Booster_Use", 1f, 0f),
                new SoundEntry(SfxId.Coin, "Coin", 1f, 0f),
                new SoundEntry(SfxId.JingleNewRecord, "Jingle_NewRecord", 1f, 0f),
                new SoundEntry(SfxId.JingleGameOver, "Jingle_GameOver", 1f, 0f),
                new SoundEntry(SfxId.JingleWin, "Jingle_Win", 1f, 0f)
            };
        }
    }
}
