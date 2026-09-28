namespace YG
{
    /// <summary>
    /// Block Puzzle part of the Yandex save: what has to survive a change of device
    /// (requirements 1.9, 1.11, 1.13.3) — the durable purchases, the palette the player
    /// picked, the record and the sound switch — plus the unfinished run, so a closed
    /// tab does not cost the game in progress.
    /// </summary>
    public partial class SavesYG
    {
        public bool adsRemoved;
        public string themeId;

        /// <summary>Owned paid palette ids, comma separated.</summary>
        public string ownedThemes;

        /// <summary>Owned paid figure pack ids, comma separated.</summary>
        public string ownedPacks;

        public int bestScore;
        public bool muted;

        /// <summary>
        /// Finished runs (Game Over or a restart from pause). Keeps new players free
        /// of interstitials for their first runs; mirrored in PlayerPrefs.
        /// </summary>
        public int gamesPlayed;

        /// <summary>First-run tutorial finished; mirrored in PlayerPrefs.</summary>
        public bool tutorialDone;

        /// <summary>
        /// Unfinished run in the compact text form of RunSnapshotCodec (board, tray,
        /// score, combo, boosters); empty when there is none. Written by
        /// YandexRunCloudStore at most every few seconds, and erased on Game Over.
        /// </summary>
        public string currentRun;

        /// <summary>Game launches with the save loaded (one per page load). Counted by YandexShortcutService.</summary>
        public int sessionsCount;

        /// <summary>UTC day number (days since 1970-01-01) of the first counted session; 0 = unknown.</summary>
        public int firstSessionDay;

        /// <summary>UTC day number of the last desktop shortcut offer; 0 = never offered.</summary>
        public int lastShortcutPromptDay;

        /// <summary>Runs that reached Game Over. Drives the review prompt (YandexReviewService).</summary>
        public int finishedGames;

        /// <summary>The review dialog was already offered. It is offered once per account.</summary>
        public bool reviewAsked;

        /// <summary>The <c>first_move</c> Metrica goal was sent, so it is sent once per player.</summary>
        public bool firstMoveSent;

        // ---------------------------------------------------------------- meta layer
        // Mirrored by YandexMetaProgressService from BlockPuzzle.Core.MetaProgress.

        /// <summary>Change counter of the meta state; the copy with the higher one wins a merge.</summary>
        public int metaRevision;

        /// <summary>In-game coins earned from runs, spent on palettes.</summary>
        public int coins;

        /// <summary>Booster stock kept between runs (daily reward, daily tasks).</summary>
        public int boosterUndo;
        public int boosterExtra;
        public int boosterClear;

        /// <summary>Days claimed in the current 7-day login chain (1…7); 0 = none.</summary>
        public int dailyStreak;

        /// <summary>Server day (Moscow midnight, days since 1970) of the last login reward; 0 = never.</summary>
        public int dailyLastClaimDay;

        /// <summary>Server day the stored daily tasks belong to.</summary>
        public int questsDay;

        /// <summary>Daily tasks as <c>id:progress:done</c> joined by <c>|</c>.</summary>
        public string quests;

        /// <summary>Score of the unfinished run already paid out in coins (continue after Game Over).</summary>
        public int runAwardedScore;

        // ---------------------------------------------------------------- player level
        // Mirrored by YandexCloudProgressService from BlockPuzzle.Core.PlayerLevel.

        /// <summary>Experience gathered towards the next level. The copy further along wins a merge.</summary>
        public int xp;

        /// <summary>Player level, 1-based; 0 in a save that never held one.</summary>
        public int playerLevel;

        // ---------------------------------------------------------------- campaign levels
        // Mirrored by YandexCloudProgressService from BlockPuzzle.Levels.LevelProgress.

        /// <summary>Highest campaign level the player may start; 0 in a save that never held one. The higher copy wins a merge.</summary>
        public int levelsUnlocked;

        /// <summary>Best stars of every level, one digit per level from level 1 ("32001"). Merged per level by maximum.</summary>
        public string levelStars;
    }
}
