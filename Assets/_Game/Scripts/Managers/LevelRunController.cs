using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Grid;
using BlockPuzzle.Levels;
using BlockPuzzle.Pieces;

namespace BlockPuzzle.Managers
{
    /// <summary>Why a level attempt ended without reaching its goal.</summary>
    public enum LevelFailReason
    {
        OutOfMoves,
        NoSpace
    }

    /// <summary>What a passed level paid out. Filled once, when the goal is reached.</summary>
    public sealed class LevelResult
    {
        public LevelDefinition Level;
        public int Number;
        public int Stars;
        public bool IsNewBest;

        /// <summary>Moves the player did not need (0 for a level without a move limit).</summary>
        public int MovesLeft;
        public int MovesUsed;

        /// <summary>Score of the level when the goal was reached.</summary>
        public int Score;

        /// <summary>Points for the unused moves, shown on the result screen.</summary>
        public int BonusScore;

        public int Coins;
        public int Xp;

        /// <summary>True once the rewarded ad doubled <see cref="Coins"/>.</summary>
        public bool CoinsDoubled;
    }

    /// <summary>
    /// Plays one attempt of a campaign level on top of the regular board: counts the moves and the
    /// progress of the goal, decides the win (goal reached) and the loss (out of moves, or no room
    /// for the figures), pays the reward and offers the rewarded "more moves" continue.
    ///
    /// The game manager owns the state and the setup of the board; this class only keeps the level's
    /// own counters. It never touches the endless record, the run save or the leaderboard.
    /// </summary>
    public sealed class LevelRunController : MonoBehaviour
    {
        /// <summary>Moves the rewarded continue adds after "out of moves".</summary>
        public const int ContinueMoves = 5;

        /// <summary>Points the result screen credits for every unused move.</summary>
        public const int MoveBonusPoints = 100;

        /// <summary>Coins per star; a special level pays double.</summary>
        public const int CoinsPerStar = 10;

        /// <summary>How long the goal card holds the board before the first move.</summary>
        public const float IntroDuration = 1.5f;

        /// <summary>
        /// Set by the platform layer: while it returns true (a fullscreen ad is on) the goal card
        /// waits, so the card is not spent behind the ad.
        /// </summary>
        public static Func<bool> IntroHold { get; set; }

        private struct RunState
        {
            public int MovesUsed;
            public int Gems;
            public int Marked;
            public int Lines;
            public int Score;
            public int Combo;
            public int DryMoves;
        }

        private GameManager gameManager;
        private GridManager grid;
        private ShapeSpawner spawner;
        private ScoreManager score;
        private BoosterController boosters;
        private GameOverHandler gameOverHandler;
        private UndoBuffer undoBuffer;

        private LevelDefinition level;
        private int movesUsed;
        private int extraMoves;
        private int gems;
        private int markedCleared;
        private int linesCleared;

        private RunState committed;
        private RunState undoState;
        private bool hasUndoState;

        private bool finished = true;
        private bool failed;
        private bool continueUsed;
        private bool pendingEvaluate;
        private Coroutine introRoutine;

        public LevelDefinition Level => level;
        public int LevelNumber => level != null ? level.Number : 0;
        public LevelGoalType GoalType => level != null ? level.GoalType : LevelGoalType.Score;
        public int GoalTarget => level != null ? level.GoalTarget : 0;
        public bool HasMoveLimit => level != null && level.HasMoveLimit;
        public int MovesUsed => movesUsed;

        /// <summary>Moves the player still has; -1 for a level without a move limit.</summary>
        public int MovesLeft => HasMoveLimit ? Mathf.Max(0, level.MoveLimit + extraMoves - movesUsed) : -1;

        /// <summary>Progress towards the goal, never above the target.</summary>
        public int GoalProgress => Mathf.Min(RawProgress, GoalTarget);

        /// <summary>True while an attempt is on (from the goal card to the win or the loss).</summary>
        public bool IsRunning => level != null && !finished && gameManager != null && gameManager.Mode == GameMode.Level;

        /// <summary>True while the goal card is up and the board waits.</summary>
        public bool IsIntro { get; private set; }

        /// <summary>True once the player made at least one move of the running attempt; leaving then burns the attempt.</summary>
        public bool HasProgress => IsRunning && movesUsed > 0;

        public LevelResult Result { get; private set; }
        public LevelFailReason FailReason { get; private set; }

        /// <summary>The rewarded continue is offered once per attempt, after a loss.</summary>
        public bool CanContinue => failed && !continueUsed && gameManager != null && gameManager.State == GameState.LevelFailed;

        /// <summary>A new attempt started; the board and the tray are already dealt.</summary>
        public event Action<LevelDefinition> LevelStarted;

        /// <summary>The goal card appeared (true) or went away (false).</summary>
        public event Action<bool> IntroChanged;

        public event Action MovesChanged;
        public event Action GoalProgressChanged;

        /// <summary>Crystals were collected: the cells they sat in. Raised before <see cref="GoalProgressChanged"/>.</summary>
        public event Action<IReadOnlyList<Vector2Int>> CrystalsCollected;

        public event Action<LevelResult> LevelWon;
        public event Action<LevelFailReason> LevelFailed;

        /// <summary>The rewarded continue was granted.</summary>
        public event Action Continued;

        /// <summary>The player left an attempt that had moves in it: (level number, moves made).</summary>
        public event Action<int, int> LevelAbandoned;

        /// <summary>The rewarded ad doubled the coins of <see cref="Result"/>.</summary>
        public event Action CoinsDoubled;

        private int RawProgress
        {
            get
            {
                switch (GoalType)
                {
                    case LevelGoalType.Gems:
                        return gems;
                    case LevelGoalType.ClearMarked:
                        return markedCleared;
                    case LevelGoalType.Lines:
                        return linesCleared;
                    default:
                        return score != null ? score.Score : 0;
                }
            }
        }

        public void Configure(
            GameManager manager,
            GridManager gridManager,
            ShapeSpawner shapeSpawner,
            ScoreManager scoreManager,
            BoosterController boosterController,
            GameOverHandler gameOver,
            UndoBuffer undo)
        {
            Unsubscribe();
            gameManager = manager;
            grid = gridManager;
            spawner = shapeSpawner;
            score = scoreManager;
            boosters = boosterController;
            gameOverHandler = gameOver;
            undoBuffer = undo;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            Unsubscribe();

            if (grid != null)
            {
                grid.GoalCellsCleared += HandleGoalCellsCleared;
            }

            if (undoBuffer != null)
            {
                undoBuffer.Undone += HandleUndone;
            }

            if (boosters != null)
            {
                boosters.RunChanged += HandleBoosterChanged;
            }
        }

        private void Unsubscribe()
        {
            if (grid != null)
            {
                grid.GoalCellsCleared -= HandleGoalCellsCleared;
            }

            if (undoBuffer != null)
            {
                undoBuffer.Undone -= HandleUndone;
            }

            if (boosters != null)
            {
                boosters.RunChanged -= HandleBoosterChanged;
            }
        }

        /// <summary>
        /// Starts counting a fresh attempt. The board, the score and the tray are set up by the
        /// game manager before this call; the goal card runs for <see cref="IntroDuration"/>.
        /// </summary>
        public void BeginLevel(LevelDefinition definition)
        {
            StopIntro();

            level = definition;
            movesUsed = 0;
            extraMoves = 0;
            gems = 0;
            markedCleared = 0;
            linesCleared = 0;
            finished = false;
            failed = false;
            continueUsed = false;
            pendingEvaluate = false;
            hasUndoState = false;
            Result = null;

            committed = CaptureState();
            undoState = committed;

            LevelStarted?.Invoke(level);
            MovesChanged?.Invoke();
            GoalProgressChanged?.Invoke();

            introRoutine = StartCoroutine(IntroRoutine());
        }

        /// <summary>
        /// Called by the game manager for every drop, after the score has been updated. Counts the
        /// move and decides the win or the loss.
        /// </summary>
        public void HandlePlacement(PlacementResult result)
        {
            if (!IsRunning || !result.Success)
            {
                return;
            }

            pendingEvaluate = false;

            // The state before this drop is what an undo goes back to.
            undoState = committed;
            hasUndoState = true;

            movesUsed++;
            linesCleared += result.LinesCleared;
            committed = CaptureState();

            MovesChanged?.Invoke();
            GoalProgressChanged?.Invoke();
            Evaluate();
        }

        /// <summary>Called by the game manager when no offered figure fits anywhere.</summary>
        public void HandleNoSpace()
        {
            if (!IsRunning)
            {
                return;
            }

            // A goal reached by the very last move wins even when that move left the board jammed.
            if (!TryFinishByGoal())
            {
                Fail(LevelFailReason.NoSpace);
            }
        }

        /// <summary>
        /// The rewarded continue. "Out of moves" gets <see cref="ContinueMoves"/> more moves; "no
        /// room" clears the two fullest lines, as the endless continue does. Once per attempt.
        /// </summary>
        public bool TryContinue()
        {
            if (!CanContinue || level == null)
            {
                return false;
            }

            continueUsed = true;
            failed = false;
            finished = false;
            Continued?.Invoke();

            if (FailReason == LevelFailReason.OutOfMoves)
            {
                extraMoves += ContinueMoves;
                gameManager.ResumePlaying();
                gameOverHandler?.Arm();
                spawner?.RefreshPlayability();
                MovesChanged?.Invoke();
                gameOverHandler?.Evaluate();
                return true;
            }

            if (boosters != null && boosters.TryContinue())
            {
                return true;
            }

            // Nothing was done (the booster refused): the loss stands.
            finished = true;
            failed = true;
            continueUsed = false;
            return false;
        }

        /// <summary>The rewarded ad doubles the coins of the passed level, once.</summary>
        public bool TryDoubleCoins()
        {
            if (Result == null || Result.CoinsDoubled || gameManager == null || gameManager.State != GameState.LevelWon)
            {
                return false;
            }

            Result.CoinsDoubled = true;
            MetaProgress.AddCoins(Result.Coins);
            CoinsDoubled?.Invoke();
            return true;
        }

        /// <summary>
        /// The player leaves a running attempt (menu, restart): it burns. Reported only when at
        /// least one move was made.
        /// </summary>
        public void Abandon()
        {
            StopIntro();

            if (level != null && !finished && movesUsed > 0)
            {
                LevelAbandoned?.Invoke(level.Number, movesUsed);
            }

            finished = true;
            failed = false;
        }

        private void LateUpdate()
        {
            // A booster (Clear) can finish the goal without a drop; it is settled once per frame.
            if (pendingEvaluate && IsRunning)
            {
                pendingEvaluate = false;
                Evaluate();
            }
        }

        private void Evaluate()
        {
            if (!IsRunning || TryFinishByGoal())
            {
                return;
            }

            if (HasMoveLimit && MovesLeft <= 0)
            {
                Fail(LevelFailReason.OutOfMoves);
            }
        }

        private bool TryFinishByGoal()
        {
            if (GoalTarget <= 0 || RawProgress < GoalTarget)
            {
                return false;
            }

            Win();
            return true;
        }

        private void Win()
        {
            finished = true;
            failed = false;
            gameOverHandler?.Disarm();

            int movesLeft = HasMoveLimit ? MovesLeft : 0;
            int finalScore = score != null ? score.Score : 0;
            int stars = level.GetStars(movesLeft, finalScore);
            int coins = CoinsPerStar * stars * (level.IsSpecial ? 2 : 1);
            int xp = PlayerLevel.XpForLevelComplete(stars);

            Result = new LevelResult
            {
                Level = level,
                Number = level.Number,
                Stars = stars,
                IsNewBest = stars > LevelProgress.GetStars(level.Number),
                MovesLeft = movesLeft,
                MovesUsed = movesUsed,
                Score = finalScore,
                BonusScore = movesLeft * MoveBonusPoints,
                Coins = coins,
                Xp = xp
            };

            LevelProgress.RecordWin(level.Number, stars);
            MetaProgress.AddCoins(coins);
            PlayerLevel.AddXp(xp, XpReason.LevelComplete);

            LevelWon?.Invoke(Result);
        }

        private void Fail(LevelFailReason reason)
        {
            finished = true;
            failed = true;
            FailReason = reason;
            gameOverHandler?.Disarm();
            LevelFailed?.Invoke(reason);
        }

        private void HandleGoalCellsCleared(IReadOnlyList<Vector2Int> crystals, IReadOnlyList<Vector2Int> marked)
        {
            if (!IsRunning)
            {
                return;
            }

            pendingEvaluate = true;

            if (crystals.Count > 0)
            {
                gems += crystals.Count;
                CrystalsCollected?.Invoke(crystals);
            }

            markedCleared += marked.Count;
            GoalProgressChanged?.Invoke();
        }

        /// <summary>A booster changed the board or the tray; a wiped line may have finished the goal.</summary>
        private void HandleBoosterChanged()
        {
            if (IsRunning)
            {
                pendingEvaluate = true;
            }
        }

        /// <summary>The last drop was taken back: the counters return to the state before it.</summary>
        private void HandleUndone()
        {
            if (!IsRunning || !hasUndoState)
            {
                return;
            }

            RunState state = undoState;
            hasUndoState = false;

            movesUsed = state.MovesUsed;
            gems = state.Gems;
            markedCleared = state.Marked;
            linesCleared = state.Lines;
            if (score != null)
            {
                score.RestoreLevelState(state.Score, state.Combo, state.DryMoves);
            }

            committed = state;

            MovesChanged?.Invoke();
            GoalProgressChanged?.Invoke();
        }

        private RunState CaptureState()
        {
            return new RunState
            {
                MovesUsed = movesUsed,
                Gems = gems,
                Marked = markedCleared,
                Lines = linesCleared,
                Score = score != null ? score.Score : 0,
                Combo = score != null ? score.ComboStreak : 0,
                DryMoves = score != null ? score.MovesWithoutClear : 0
            };
        }

        private IEnumerator IntroRoutine()
        {
            IsIntro = true;
            spawner?.SetInteractable(false);
            IntroChanged?.Invoke(true);

            float shown = 0f;
            while (shown < IntroDuration)
            {
                if (IntroHold == null || !IntroHold())
                {
                    shown += Time.unscaledDeltaTime;
                }

                yield return null;
            }

            introRoutine = null;
            IsIntro = false;
            if (gameManager != null && gameManager.State == GameState.Playing)
            {
                spawner?.SetInteractable(true);
            }

            IntroChanged?.Invoke(false);
        }

        private void StopIntro()
        {
            if (introRoutine != null)
            {
                StopCoroutine(introRoutine);
                introRoutine = null;
            }

            if (IsIntro)
            {
                IsIntro = false;
                IntroChanged?.Invoke(false);
            }
        }
    }
}
