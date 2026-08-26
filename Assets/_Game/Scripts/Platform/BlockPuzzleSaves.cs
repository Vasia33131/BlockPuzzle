namespace YG
{
    /// <summary>
    /// Block Puzzle part of the Yandex save. Only what has to survive a change of
    /// device (requirements 1.9, 1.11, 1.13.3): the durable purchases, the palette the
    /// player picked, the record and the sound switch. The running board and the score
    /// of the current run are deliberately not here.
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
    }
}
