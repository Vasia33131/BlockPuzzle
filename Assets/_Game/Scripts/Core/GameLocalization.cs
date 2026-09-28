using System;
using BlockPuzzle.Levels;

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

        /// <summary>Banner over the board for a multi-line clear; null for a single line.</summary>
        public static string Praise(int lines)
        {
            switch (lines)
            {
                case 0:
                case 1:
                    return null;
                case 2:
                    return Good;
                case 3:
                    return Great;
                case 4:
                    return Super;
                default:
                    return Incredible;
            }
        }

        public static string Good => Pick("Хорошо!", "Good!");
        public static string Great => Pick("Отлично!", "Great!");
        public static string Super => Pick("Супер!", "Super!");
        public static string Incredible => Pick("Невероятно!", "Incredible!");
        public static string BoardClearedTitle => Pick("Поле очищено!", "Board cleared!");
        public static string ComboBanner(int combo) => Pick($"Комбо x{combo}", $"Combo x{combo}");
        public static string Points(int points) => $"+{points}";

        public static string ScorePrefix => Pick("СЧЁТ: ", "SCORE: ");
        public static string BestPrefix => Pick("РЕКОРД: ", "BEST: ");

        public static string PauseTitle => Pick("ПАУЗА", "PAUSE");
        public static string SoundOn => Pick("ЗВУК: ВКЛ", "SOUND: ON");
        public static string SoundOff => Pick("ЗВУК: ВЫКЛ", "SOUND: OFF");
        public static string Resume => Pick("ПРОДОЛЖИТЬ", "RESUME");
        public static string Restart => Pick("НАЧАТЬ ЗАНОВО", "RESTART");

        public static string Home => Pick("ДОМОЙ", "HOME");

        public static string SettingsTitle => Pick("НАСТРОЙКИ", "SETTINGS");
        public static string HowToPlay => Pick("КАК ИГРАТЬ", "HOW TO PLAY");
        public static string OurGames => Pick("Наши игры", "Our games");
        public static string Close => Pick("ЗАКРЫТЬ", "CLOSE");
        public static string VersionLabel(string version) => Pick($"Версия {version}", $"Version {version}");
        public static string EndlessMode => Pick("БЕСКОНЕЧНЫЙ РЕЖИМ", "ENDLESS MODE");
        public static string Levels => Pick("УРОВНИ", "LEVELS");
        public static string MenuTitle => Pick("БЛОК-ПАЗЛ", "BLOCK PUZZLE");
        public static string MenuEndless => Pick("БЕСКОНЕЧНЫЙ", "ENDLESS");
        public static string ComingSoon => Pick("Скоро!", "Coming soon!");

        public static string ShopTitle =>Pick("МАГАЗИН", "SHOP");
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

        public static string GameOverTitle => Pick("Нет ходов!", "No moves left!");
        public static string PlayAgain => Pick("ИГРАТЬ СНОВА", "PLAY AGAIN");
        public static string ToRecord(int points) => Pick($"До рекорда: {points}", $"To beat your best: {points}");
        public static string RecordTied => Pick("Рекорд повторён!", "You matched your best!");
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
        public static string TutorialHint => Pick(
            "Перетащи фигуру, чтобы собрать линию",
            "Drag the shape to complete the line");
        public static string TutorialDone => Pick("Отлично!", "Great!");

        public static string ClearTitle => Pick("Очистка линии", "Clear a line");
        public static string ClearBody => Pick(
            "Уберёт самую заполненную строку или столбец.",
            "Removes the fullest row or column.");

        // ------------------------------------------------------------ meta layer

        public static string DailyRewardTitle => Pick("ЕЖЕДНЕВНАЯ НАГРАДА", "DAILY REWARD");
        public static string DailyRewardHint => Pick(
            "Заходи каждый день — награда растёт. Пропуск дня начинает серию заново.",
            "Come back every day for bigger rewards. Missing a day restarts the streak.");
        public static string DayLabel(int day) => Pick($"День {day}", $"Day {day}");
        public static string Claim => Pick("ЗАБРАТЬ", "CLAIM");
        public static string Claimed => Pick("Получено", "Claimed");
        public static string DailyClaimedToast => Pick("Награда получена!", "Reward claimed!");

        public static string QuestsTitle => Pick("ЗАДАНИЯ ДНЯ", "DAILY TASKS");
        public static string QuestsWaiting => Pick("Задания загружаются...", "Loading tasks...");
        public static string QuestDone => Pick("Готово", "Done");
        public static string QuestCompletedToast => Pick("Задание выполнено!", "Task complete!");

        public static string QuestText(QuestDef def)
        {
            if (def == null)
            {
                return string.Empty;
            }

            int n = def.Target;
            switch (def.Kind)
            {
                case QuestKind.ClearLines:
                    return Pick(
                        $"Очисти {n} {Plural(n, "линию", "линии", "линий")}",
                        $"Clear {n} {(n == 1 ? "line" : "lines")}");
                case QuestKind.ScoreInRun:
                    return Pick($"Набери {n} за партию", $"Score {n} in one game");
                case QuestKind.Combo:
                    return Pick($"Сделай комбо x{n}", $"Make a x{n} combo");
                case QuestKind.BoardClear:
                    return n <= 1
                        ? Pick("Очисти поле целиком", "Clear the whole board")
                        : Pick($"Очисти поле целиком {n} раза", $"Clear the whole board {n} times");
                default:
                    return string.Empty;
            }
        }

        public static string CoinsEarned(int coins) => Pick(
            $"+{coins} {Plural(coins, "монета", "монеты", "монет")}",
            $"+{coins} {(coins == 1 ? "coin" : "coins")}");
        public static string BuyForCoinsConfirm => Pick("Купить?", "Buy?");

        // ------------------------------------------------------------ player profile

        public static string PlayerFallbackName => Pick("Игрок", "Player");
        public static string SignInToSave => Pick("Войдите, чтобы сохранять прогресс", "Sign in to save your progress");
        public static string NewLevelTitle => Pick("Новый уровень!", "New level!");
        public static string PlayerLevelCaption(int level) => Pick($"Уровень {level}", $"Level {level}");

        // ------------------------------------------------------------ levels

        public static string LevelTitle(int level) => Pick($"Уровень {level}", $"Level {level}");
        public static string MovesCaption => Pick("ХОДЫ", "MOVES");
        public static string LevelGoalHeader => Pick("ЦЕЛЬ", "GOAL");

        /// <summary>Text of the goal card shown before a level: "Collect 10 crystals in 20 moves".</summary>
        public static string LevelGoalCard(LevelDefinition level) => LevelGoalText(level, true);

        /// <summary>The goal alone, without the move limit ("Collect 10 crystals"): the level map card lists the limit on its own line.</summary>
        public static string LevelGoalShort(LevelDefinition level) => LevelGoalText(level, false);

        private static string LevelGoalText(LevelDefinition level, bool withLimit)
        {
            if (level == null)
            {
                return string.Empty;
            }

            int n = level.GoalTarget;
            int moves = withLimit ? level.MoveLimit : 0;
            string ruLimit = moves > 0 ? $" за {moves} {Plural(moves, "ход", "хода", "ходов")}" : string.Empty;
            string enLimit = moves > 0 ? $" in {moves} {(moves == 1 ? "move" : "moves")}" : string.Empty;

            switch (level.GoalType)
            {
                case LevelGoalType.Gems:
                    return Pick(
                        $"Собери {n} {Plural(n, "кристалл", "кристалла", "кристаллов")}{ruLimit}",
                        $"Collect {n} {(n == 1 ? "crystal" : "crystals")}{enLimit}");
                case LevelGoalType.ClearMarked:
                    return Pick(
                        $"Очисти все отмеченные клетки{ruLimit}",
                        $"Clear every marked cell{enLimit}");
                case LevelGoalType.Lines:
                    return Pick(
                        $"Очисти {n} {Plural(n, "линию", "линии", "линий")}{ruLimit}",
                        $"Clear {n} {(n == 1 ? "line" : "lines")}{enLimit}");
                default:
                    return Pick(
                        $"Набери {n} {Plural(n, "очко", "очка", "очков")}{ruLimit}",
                        $"Score {n} {(n == 1 ? "point" : "points")}{enLimit}");
            }
        }

        /// <summary>HUD goal counter. Crystals get an icon instead of a caption, so theirs is just "3/10".</summary>
        public static string LevelGoalCounter(LevelGoalType goal, int progress, int target)
        {
            switch (goal)
            {
                case LevelGoalType.Gems:
                    return $"{progress}/{target}";
                case LevelGoalType.ClearMarked:
                    return $"{progress}/{target}";
                case LevelGoalType.Lines:
                    return Pick($"Линии {progress}/{target}", $"Lines {progress}/{target}");
                default:
                    return Pick($"Очки {progress}/{target}", $"Score {progress}/{target}");
            }
        }

        // Level map

        public static string ChapterTitle(int chapter)
        {
            string[] ru = { "Начало", "Кристальный лес", "Закатные дюны", "Глубина", "Звёздный путь" };
            string[] en = { "The Beginning", "Crystal Woods", "Sunset Dunes", "The Deep", "Star Trail" };
            int index = Math.Max(0, Math.Min(chapter - 1, ru.Length - 1));
            return Pick($"Глава {chapter} · {ru[index]}", $"Chapter {chapter} · {en[index]}");
        }

        public static string LevelsSoon => Pick("Скоро новые уровни!", "New levels coming soon!");
        public static string LevelsAllDone => Pick("Все пройдены", "All complete");
        public static string LevelLockedHint => Pick("Сначала пройди предыдущий уровень", "Finish the previous level first");
        public static string LevelPlay => Pick("ИГРАТЬ", "PLAY");
        public static string SpecialLevel => Pick("Особый уровень", "Special level");
        public static string BestResult => Pick("ЛУЧШИЙ РЕЗУЛЬТАТ", "BEST RESULT");
        public static string MoveLimitLine(int moves) => moves > 0
            ? Pick($"Ходов: {moves}", $"Moves: {moves}")
            : Pick("Без ограничения ходов", "No move limit");

        public static string LevelCompleteTitle => Pick("Уровень пройден!", "Level complete!");
        public static string LevelFailedTitle => Pick("Уровень не пройден", "Level failed");
        public static string FailOutOfMoves => Pick("Закончились ходы", "Out of moves");
        public static string FailNoSpace => Pick("Нет места для фигур", "No room for the shapes");
        public static string MovesBonus => Pick("Бонус за ходы", "Move bonus");
        public static string NextLevel => Pick("ДАЛЬШЕ", "NEXT");
        public static string Retry => Pick("ЗАНОВО", "RETRY");
        public static string DoubleCoinsAd => Pick("x2 монеты — реклама", "x2 coins — ad");
        public static string DoubleCoinsHint => Pick("Удвоим награду", "Doubles your reward");
        public static string MoreMovesAd => Pick("Ещё 5 ходов — реклама", "5 more moves — ad");
        public static string MoreMovesHint => Pick("Продолжим уровень", "Keep playing the level");
        public static string XpEarned(int xp) => Pick($"+{xp} опыта", $"+{xp} XP");
        public static string LeaveLevelTitle => Pick("Выйти из уровня?", "Leave the level?");
        public static string LeaveLevelBody => Pick(
            "Прогресс этой попытки будет потерян.",
            "The progress of this attempt will be lost.");
        public static string LeaveLevelYes => Pick("ВЫЙТИ", "LEAVE");
        public static string LeaveLevelNo => Pick("ОСТАТЬСЯ", "STAY");

        /// <summary>Russian plural: 1 линию, 2 линии, 5 линий.</summary>
        private static string Plural(int n, string one, string few, string many)
        {
            int mod100 = System.Math.Abs(n) % 100;
            int mod10 = mod100 % 10;
            if (mod100 >= 11 && mod100 <= 14)
            {
                return many;
            }

            if (mod10 == 1)
            {
                return one;
            }

            return mod10 >= 2 && mod10 <= 4 ? few : many;
        }
    }
}
