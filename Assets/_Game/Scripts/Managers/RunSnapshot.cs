using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using BlockPuzzle.Grid;

namespace BlockPuzzle.Managers
{
    /// <summary>
    /// Everything needed to continue an unfinished run after the tab was closed: the
    /// board, the tray (by figure name), the score with its combo and the run's booster
    /// state. Encoded by <see cref="RunSnapshotCodec"/> into one short string.
    /// </summary>
    public sealed class RunSnapshot
    {
        /// <summary>When the run was saved, UTC milliseconds. The newer of two copies wins.</summary>
        public long Stamp;

        public BoardSnapshot Board;

        /// <summary>One figure name per tray slot; null or empty for an empty slot.</summary>
        public string[] Tray;

        public int Score;
        public int Combo;
        public int DryMoves;
        public int RunStartRecord;
        public bool ContinueUsed;
        public FreeBoosterType? FreeCharge;
        public int FreeThreshold;
    }

    /// <summary>
    /// Text form of a <see cref="RunSnapshot"/>:
    /// <c>1;stamp;size;board;colors;tray;score;combo;dry;record;continue;free;threshold#crc</c>.
    ///
    /// The board is one character per cell, row by row: <c>.</c> for an empty cell, else a
    /// base-36 index into <c>colors</c> (comma separated RRGGBBAA). The tray is figure names
    /// joined by <c>|</c>. The checksum catches a truncated or hand-edited value. The string
    /// never holds quotes or backslashes, so it survives the JSON round trips of the
    /// platform save untouched.
    /// </summary>
    public static class RunSnapshotCodec
    {
        public const int FormatVersion = 1;

        private const char FieldSeparator = ';';
        private const char TraySeparator = '|';
        private const char ColorSeparator = ',';
        private const char ChecksumSeparator = '#';
        private const char EmptyCell = '.';
        private const int FieldCount = 13;
        private const int MaxColors = 36;
        private const int MaxBoardSize = 16;
        private const string Digits = "0123456789abcdefghijklmnopqrstuvwxyz";

        /// <summary>Text form of <paramref name="snapshot"/>, or null when it cannot be represented.</summary>
        public static string Encode(RunSnapshot snapshot)
        {
            if (snapshot?.Board == null || snapshot.Tray == null)
            {
                return null;
            }

            BoardSnapshot board = snapshot.Board;
            var palette = new List<string>();
            var cells = new StringBuilder(board.Size * board.Size);

            for (int row = 0; row < board.Size; row++)
            {
                for (int col = 0; col < board.Size; col++)
                {
                    if (!board.Occupied[row, col])
                    {
                        cells.Append(EmptyCell);
                        continue;
                    }

                    string hex = ColorUtility.ToHtmlStringRGBA(board.Colors[row, col]);
                    int index = palette.IndexOf(hex);
                    if (index < 0)
                    {
                        if (palette.Count >= MaxColors)
                        {
                            return null;
                        }

                        index = palette.Count;
                        palette.Add(hex);
                    }

                    cells.Append(Digits[index]);
                }
            }

            var tray = new string[snapshot.Tray.Length];
            for (int i = 0; i < tray.Length; i++)
            {
                string name = snapshot.Tray[i] ?? string.Empty;
                if (name.IndexOfAny(new[] { FieldSeparator, TraySeparator, ChecksumSeparator }) >= 0)
                {
                    return null;
                }

                tray[i] = name;
            }

            string body = string.Join(FieldSeparator.ToString(), new[]
            {
                FormatVersion.ToString(CultureInfo.InvariantCulture),
                snapshot.Stamp.ToString(CultureInfo.InvariantCulture),
                board.Size.ToString(CultureInfo.InvariantCulture),
                cells.ToString(),
                string.Join(ColorSeparator.ToString(), palette),
                string.Join(TraySeparator.ToString(), tray),
                Int(snapshot.Score),
                Int(snapshot.Combo),
                Int(snapshot.DryMoves),
                Int(snapshot.RunStartRecord),
                snapshot.ContinueUsed ? "1" : "0",
                Int(snapshot.FreeCharge.HasValue ? (int)snapshot.FreeCharge.Value : -1),
                Int(snapshot.FreeThreshold)
            });

            return body + ChecksumSeparator + Checksum(body);
        }

        /// <summary>
        /// Parses <paramref name="text"/>. False for an empty value and for anything
        /// malformed: wrong version, bad checksum, bad board, out-of-range numbers.
        /// </summary>
        public static bool TryDecode(string text, out RunSnapshot snapshot)
        {
            snapshot = null;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            try
            {
                return TryDecodeUnsafe(text, out snapshot);
            }
            catch (Exception)
            {
                snapshot = null;
                return false;
            }
        }

        private static bool TryDecodeUnsafe(string text, out RunSnapshot snapshot)
        {
            snapshot = null;

            int hash = text.LastIndexOf(ChecksumSeparator);
            if (hash <= 0)
            {
                return false;
            }

            string body = text.Substring(0, hash);
            if (!string.Equals(text.Substring(hash + 1), Checksum(body), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string[] fields = body.Split(FieldSeparator);
            if (fields.Length != FieldCount
                || !TryInt(fields[0], out int version) || version != FormatVersion
                || !long.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long stamp) || stamp <= 0
                || !TryInt(fields[2], out int size) || size < 1 || size > MaxBoardSize)
            {
                return false;
            }

            string cells = fields[3];
            if (cells.Length != size * size)
            {
                return false;
            }

            Color[] palette = ParsePalette(fields[4]);
            if (palette == null)
            {
                return false;
            }

            var occupied = new bool[size, size];
            var colors = new Color[size, size];
            for (int i = 0; i < cells.Length; i++)
            {
                char c = cells[i];
                if (c == EmptyCell)
                {
                    continue;
                }

                int index = Digits.IndexOf(c);
                if (index < 0 || index >= palette.Length)
                {
                    return false;
                }

                occupied[i / size, i % size] = true;
                colors[i / size, i % size] = palette[index];
            }

            string[] tray = fields[5].Split(TraySeparator);

            if (!TryInt(fields[6], out int score) || score < 0
                || !TryInt(fields[7], out int combo) || combo < 0
                || !TryInt(fields[8], out int dry) || dry < 0
                || !TryInt(fields[9], out int record) || record < 0
                || (fields[10] != "0" && fields[10] != "1")
                || !TryInt(fields[11], out int free) || free < -1 || free >= Enum.GetValues(typeof(FreeBoosterType)).Length
                || !TryInt(fields[12], out int threshold) || threshold < 0)
            {
                return false;
            }

            snapshot = new RunSnapshot
            {
                Stamp = stamp,
                Board = new BoardSnapshot(size, occupied, colors),
                Tray = tray,
                Score = score,
                Combo = combo,
                DryMoves = dry,
                RunStartRecord = record,
                ContinueUsed = fields[10] == "1",
                FreeCharge = free >= 0 ? (FreeBoosterType?)free : null,
                FreeThreshold = threshold
            };
            return true;
        }

        private static Color[] ParsePalette(string field)
        {
            if (field.Length == 0)
            {
                return Array.Empty<Color>();
            }

            string[] parts = field.Split(ColorSeparator);
            if (parts.Length > MaxColors)
            {
                return null;
            }

            var palette = new Color[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length != 8 || !ColorUtility.TryParseHtmlString("#" + parts[i], out palette[i]))
                {
                    return null;
                }
            }

            return palette;
        }

        private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static bool TryInt(string text, out int value) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        /// <summary>FNV-1a over the UTF-16 code units, as 8 hex digits.</summary>
        private static string Checksum(string body)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in body)
                {
                    hash = (hash ^ c) * 16777619;
                }

                return hash.ToString("x8", CultureInfo.InvariantCulture);
            }
        }
    }
}
