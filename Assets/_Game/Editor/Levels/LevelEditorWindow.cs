using UnityEditor;
using UnityEngine;
using BlockPuzzle.Levels;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Edits the campaign levels: pick a level, paint its prefilled 8x8 board with the mouse, set the goal and
    /// the move limit, regenerate it or check it with the bot. Left click paints, right click erases.
    /// </summary>
    public sealed class LevelEditorWindow : EditorWindow
    {
        private const float ListWidth = 210f;
        private const float CellSize = 42f;
        private const float CellGap = 2f;

        private static readonly string[] BrushLabels = { "Block", "Crystal", "Marked", "Eraser" };

        private LevelDatabase database;
        private SerializedObject levelObject;
        private LevelDefinition selected;
        private Vector2 listScroll;
        private Vector2 detailScroll;
        private int brush;
        private string botReport = string.Empty;

        [MenuItem("Tools/Block Puzzle/Level Editor")]
        private static void Open()
        {
            GetWindow<LevelEditorWindow>("Level Editor").minSize = new Vector2(720f, 560f);
        }

        private void OnEnable()
        {
            LoadDatabase();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (database == null || database.Count == 0)
            {
                EditorGUILayout.HelpBox("No level database yet. Use \"Generate 100 levels\" to create it.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawList();
            DrawDetails();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                LoadDatabase();
            }

            if (GUILayout.Button("Generate 100 levels", EditorStyles.toolbarButton, GUILayout.Width(130f)))
            {
                EditorApplication.delayCall += () =>
                {
                    if (EditorUtility.DisplayDialog("Generate 100 levels", "Rebuilds every level asset, hand-made edits are lost. Continue?", "Generate", "Cancel"))
                    {
                        LevelGenerator.GenerateAll();
                        LoadDatabase();
                        Repaint();
                    }
                };
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawList()
        {
            listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Width(ListWidth));
            Color background = GUI.backgroundColor;

            for (int i = 0; i < database.Count; i++)
            {
                LevelDefinition level = database.Levels[i];
                if (level == null)
                {
                    continue;
                }

                GUI.backgroundColor = level == selected ? new Color(0.5f, 0.75f, 1f) : background;
                string label = $"{level.Number:000}{(level.IsSpecial ? " *" : "  ")}  {level.GoalType}  {GoalSummary(level)}";
                if (GUILayout.Button(label, EditorStyles.miniButtonLeft) && level != selected)
                {
                    Select(level);
                }
            }

            GUI.backgroundColor = background;
            EditorGUILayout.EndScrollView();
        }

        private void DrawDetails()
        {
            EditorGUILayout.BeginVertical();

            if (selected == null || levelObject == null)
            {
                EditorGUILayout.HelpBox("Select a level on the left.", MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
            levelObject.Update();

            EditorGUILayout.LabelField($"Level {selected.Number}", EditorStyles.boldLabel);
            DrawFields();
            EditorGUILayout.Space();
            DrawBoard();
            DrawCounts();
            EditorGUILayout.Space();
            DrawActions();

            levelObject.ApplyModifiedProperties();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawFields()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(levelObject.FindProperty("number"));
            }

            EditorGUILayout.PropertyField(levelObject.FindProperty("isSpecial"));
            EditorGUILayout.PropertyField(levelObject.FindProperty("goalType"));

            if (selected.GoalType == LevelGoalType.ClearMarked)
            {
                EditorGUILayout.LabelField("Goal target", $"{selected.GoalTarget} marked cells (counted from the board)");
            }
            else
            {
                EditorGUILayout.PropertyField(levelObject.FindProperty("goalTarget"));
            }

            EditorGUILayout.PropertyField(levelObject.FindProperty("moveLimit"), new GUIContent("Move limit (0 = none)"));
            EditorGUILayout.PropertyField(levelObject.FindProperty("shapeSeed"));
            EditorGUILayout.PropertyField(levelObject.FindProperty("difficulty"));
            EditorGUILayout.PropertyField(levelObject.FindProperty("coinReward"));

            string unit = selected.StarsUseMovesLeft ? "moves left" : "points";
            EditorGUILayout.PropertyField(levelObject.FindProperty("twoStarThreshold"), new GUIContent("2 stars (" + unit + ")"));
            EditorGUILayout.PropertyField(levelObject.FindProperty("threeStarThreshold"), new GUIContent("3 stars (" + unit + ")"));
        }

        private void DrawBoard()
        {
            brush = GUILayout.Toolbar(brush, BrushLabels, GUILayout.Width(4 * 80f));

            float side = LevelDefinition.Size * (CellSize + CellGap) + CellGap;
            Rect area = GUILayoutUtility.GetRect(side, side, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(area, new Color(0.12f, 0.12f, 0.14f));

            SerializedProperty cells = levelObject.FindProperty("cells");
            Event current = Event.current;
            bool paint = (current.type == EventType.MouseDown || current.type == EventType.MouseDrag)
                && (current.button == 0 || current.button == 1);

            for (int row = 0; row < LevelDefinition.Size; row++)
            {
                for (int col = 0; col < LevelDefinition.Size; col++)
                {
                    Rect rect = new Rect(
                        area.x + CellGap + col * (CellSize + CellGap),
                        area.y + CellGap + row * (CellSize + CellGap),
                        CellSize,
                        CellSize);

                    SerializedProperty cell = cells.GetArrayElementAtIndex(row * LevelDefinition.Size + col);

                    if (paint && rect.Contains(current.mousePosition))
                    {
                        LevelCellType painted = current.button == 1 || brush == 3 ? LevelCellType.Empty : (LevelCellType)(brush + 1);
                        if (cell.enumValueIndex != (int)painted)
                        {
                            cell.enumValueIndex = (int)painted;
                            GUI.changed = true;
                        }

                        current.Use();
                    }

                    DrawCell(rect, (LevelCellType)cell.enumValueIndex);
                }
            }

            if (paint && area.Contains(current.mousePosition))
            {
                Repaint();
            }
        }

        private static void DrawCell(Rect rect, LevelCellType type)
        {
            switch (type)
            {
                case LevelCellType.Block:
                    EditorGUI.DrawRect(rect, new Color(0.35f, 0.5f, 0.85f));
                    break;

                case LevelCellType.Crystal:
                    EditorGUI.DrawRect(rect, new Color(0.35f, 0.5f, 0.85f));
                    EditorGUI.DrawRect(new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, rect.height - 20f), new Color(0.4f, 1f, 0.95f));
                    break;

                case LevelCellType.Marked:
                    EditorGUI.DrawRect(rect, new Color(0.95f, 0.6f, 0.2f));
                    EditorGUI.DrawRect(new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, rect.height - 24f), new Color(0.35f, 0.15f, 0.05f));
                    break;

                default:
                    EditorGUI.DrawRect(rect, new Color(0.22f, 0.22f, 0.26f));
                    break;
            }
        }

        private void DrawCounts()
        {
            EditorGUILayout.LabelField(
                $"Blocks {selected.CountCells(LevelCellType.Block)}   Crystals {selected.CountCells(LevelCellType.Crystal)}   Marked {selected.CountCells(LevelCellType.Marked)}");

            foreach (string problem in selected.Validate())
            {
                EditorGUILayout.HelpBox(problem, MessageType.Warning);
            }
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Regenerate this level", GUILayout.Height(26f)))
            {
                RunLater(() =>
                {
                    Undo.RecordObject(selected, "Regenerate level");
                    LevelGenerator.Result result = LevelGenerator.Regenerate(selected);
                    botReport = result.Line;
                });
            }

            if (GUILayout.Button("Check with bot", GUILayout.Height(26f)))
            {
                RunLater(() =>
                {
                    EditorUtility.DisplayProgressBar("Level bot", $"Playing level {selected.Number}", 0.5f);
                    try
                    {
                        BotReport report = LevelBotSimulator.Evaluate(selected);
                        botReport = $"Level {selected.Number}: {report}";
                        Debug.Log("[LevelGen] " + botReport);
                    }
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                });
            }

            if (GUILayout.Button("Save", GUILayout.Height(26f)))
            {
                levelObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(selected);
                AssetDatabase.SaveAssets();
            }

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(botReport))
            {
                EditorGUILayout.HelpBox(botReport, MessageType.None);
            }
        }

        /// <summary>Long jobs run outside the GUI pass, so the layout is not left half-built.</summary>
        private void RunLater(System.Action action)
        {
            levelObject.ApplyModifiedProperties();
            EditorApplication.delayCall += () =>
            {
                if (selected == null)
                {
                    return;
                }

                action();
                levelObject = new SerializedObject(selected);
                Repaint();
            };
        }

        private void Select(LevelDefinition level)
        {
            selected = level;
            levelObject = level != null ? new SerializedObject(level) : null;
            botReport = string.Empty;
            GUI.FocusControl(null);
        }

        private void LoadDatabase()
        {
            database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(LevelGenerator.DatabasePath);
            LevelDefinition keep = selected;
            if (keep != null && database != null)
            {
                bool exists = false;
                foreach (LevelDefinition level in database.Levels)
                {
                    exists |= level == keep;
                }

                keep = exists ? keep : null;
            }

            Select(keep != null ? keep : database != null && database.Count > 0 ? database.Levels[0] : null);
        }

        private static string GoalSummary(LevelDefinition level)
        {
            string moves = level.HasMoveLimit ? $"/{level.MoveLimit}" : string.Empty;
            return level.GoalType == LevelGoalType.ClearMarked
                ? $"x{level.GoalTarget}{moves}"
                : $"{level.GoalTarget}{moves}";
        }
    }
}
