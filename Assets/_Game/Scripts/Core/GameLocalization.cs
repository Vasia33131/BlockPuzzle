using System;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Hand-written Russian and English UI copy. Language follows
    /// <c>ysdk.environment.i18n.lang</c> from the Yandex Games SDK (requirement 2.14).
    /// Unsupported portal languages use the official fallback: Russian for
    /// <c>ru/be/kk/uk/uz</c>, English for everything else. Do not list extra languages
    /// in the draft — moderation opens the game in every declared language, and
    /// machine-translated UI is a common rejection.
    /// </summary>
    public static class GameLocalization
    {
        public static event Action LanguageChanged;

        public static bool IsEnglish { get; private set; }

        /// <summary>
        /// Applies the ISO 639-1 code from the platform SDK. Empty/unknown-until-ready
        /// stays Russian so the first frame is not English for CIS players.
        /// </summary>
        public static void ApplyPlatformLanguage(string lang)
        {
            bool english = UseEnglish(lang);
            if (english == IsEnglish)
            {
                return;
            }

            IsEnglish = english;
            LanguageChanged?.Invoke();
        }

        /// <summary>
        /// Official Yandex fallback set: Russian for CIS codes, English for the rest.
        /// </summary>
        private static bool UseEnglish(string lang)
        {
            string code = NormalizeLang(lang);
            if (string.IsNullOrEmpty(code))
            {
                return false;
            }

            switch (code)
            {
                case "ru":
                case "be":
                case "kk":
                case "uk":
                case "uz":
                    return false;
                default:
                    return true;
            }
        }

        private static string NormalizeLang(string lang)
        {
            if (string.IsNullOrEmpty(lang))
            {
                return string.Empty;
            }

            string code = lang.Trim().ToLowerInvariant();
            if (code == "us" || code == "as" || code == "ai")
            {
                return "en";
            }

            int separator = code.IndexOfAny(new[] { '-', '_' });
            if (separator > 0)
            {
                code = code.Substring(0, separator);
            }

            return code;
        }

        private static string Pick(string russian, string english)
        {
            return IsEnglish ? english : russian;
        }

        public static string ThemeName(string themeId)
        {
            if (themeId == ThemeConfig.OceanId)
            {
                return OceanTheme;
            }

            if (themeId == ThemeConfig.CandyId)
            {
                return CandyTheme;
            }

            return ClassicTheme;
        }

        public static string Combo(int combo, int points)
        {
            if (combo > 1)
            {
                return Pick($"КОМБО x{combo}  +{points}", $"COMBO x{combo}  +{points}");
            }

            return $"+{points}";
        }

        public static string ScorePrefix => Pick("СЧЁТ: ", "SCORE: ");
        public static string BestPrefix => Pick("РЕКОРД: ", "BEST: ");

        public static string PauseTitle => Pick("ПАУЗА", "PAUSE");
        public static string SoundOn => Pick("ЗВУК: ВКЛ", "SOUND: ON");
        public static string SoundOff => Pick("ЗВУК: ВЫКЛ", "SOUND: OFF");
        public static string Resume => Pick("ПРОДОЛЖИТЬ", "RESUME");
        public static string Restart => Pick("НАЧАТЬ ЗАНОВО", "RESTART");

        public static string ShopTitle => Pick("МАГАЗИН", "SHOP");
        public static string Back => Pick("НАЗАД", "BACK");
        public static string NoAds => Pick("Без рекламы", "No ads");
        public static string ShapePack => Pick("Набор фигурок", "Shape pack");
        public static string Buy => Pick("Купить", "Buy");
        public static string Purchased => Pick("Куплено", "Purchased");
        public static string Select => Pick("Выбрать", "Select");
        public static string Selected => Pick("Выбрано", "Selected");
        public static string PackPreviewTitle => Pick("Дополнительные фигуры", "Extra shapes");
        public static string PackPreviewBody => Pick(
            "Эти фигуры будут доступны в наборе",
            "These shapes will be added to your set");
        public static string Cancel => Pick("Отмена", "Cancel");
        public static string ClassicTheme => Pick("Классика", "Classic");
        public static string OceanTheme => Pick("Океан", "Ocean");
        public static string CandyTheme => Pick("Конфеты", "Candy");

        public static string GameOverTitle => Pick("ПОРАЖЕНИЕ", "GAME OVER");
        public static string NewBest => Pick("НОВЫЙ РЕКОРД!", "NEW BEST!");
        public static string ScoreCaption => Pick("СЧЁТ", "SCORE");
        public static string BestCaption => Pick("РЕКОРД", "BEST");
        public static string AuthHint => Pick(
            "Авторизуйтесь, чтобы сохранить результат в таблице лидеров",
            "Sign in to save your score on the leaderboard");
        public static string SignIn => Pick("АВТОРИЗОВАТЬСЯ", "SIGN IN");
        public static string ContinueAd => Pick("Продолжить — реклама", "Continue — ad");
        public static string ContinueHint => Pick("Уберём 1–2 линии", "We'll clear 1–2 lines");

        public static string WatchAd => Pick("Смотреть", "Watch ad");
        public static string AdBonusWarning => Pick(
            "Бонус за просмотр рекламы",
            "Bonus for watching an ad");
        public static string UndoTitle => Pick("Отмена хода", "Undo move");
        public static string UndoBody => Pick(
            "Вернёт последнюю поставленную фигуру на панель.",
            "Returns the last placed shape to the tray.");
        public static string ExtraTitle => Pick("Лишняя фигура", "Extra shape");
        public static string ExtraBody => Pick(
            "Добавит ещё одну фигуру на панель, если есть свободный слот.",
            "Adds one more shape to the tray if a slot is free.");
        public static string ClearTitle => Pick("Очистка линии", "Clear a line");
        public static string ClearBody => Pick(
            "Уберёт самую заполненную строку или столбец.",
            "Removes the fullest row or column.");
    }
}
