using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using UnityEngine;
using UnityEngine.UI;
using UnityModManagerNet;

namespace SummonsTransitionFix
{
    public static class CreatureListWindow
    {
        private const int WindowId = 1398031942;
        private const float WindowWidth = 560f;
        private const float WindowHeight = 420f;
        private const float NameWidth = 290f;
        private const float ButtonWidth = 220f;
        private const float ShortcutButtonWidth = 420f;

        private static readonly List<CreatureRow> s_Rows = new List<CreatureRow>();
        private static bool s_Open;
        private static bool s_Positioned;
        private static bool s_WaitingForShortcut;
        private static Rect s_WindowRect = new Rect(0f, 0f, WindowWidth, WindowHeight);
        private static Vector2 s_Scroll;
        private static GameObject? s_ClickBlocker;

        public static void OnUpdate(UnityModManager.ModEntry modEntry, float deltaTime)
        {
            try
            {
                if (Main.Enabled && !s_WaitingForShortcut && IsShortcutPressed(Main.ModSettings))
                {
                    if (s_Open)
                    {
                        Close();
                    }
                    else if (IsGameLoaded())
                    {
                        Open();
                    }
                }

                if (s_Open && (!Main.Enabled || !IsGameLoaded()))
                {
                    Close();
                }
            }
            catch (Exception ex)
            {
                Close();
                Main.Logger?.Error($"[SummonsTransitionFix] The creature list shortcut failed: {ex}");
            }
        }

        public static void OnFixedGUI(UnityModManager.ModEntry modEntry)
        {
            if (!s_Open) return;

            try
            {
                float scale = Mathf.Max(1f, Screen.height / 1080f);
                Matrix4x4 previousMatrix = GUI.matrix;
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

                if (!s_Positioned)
                {
                    s_WindowRect.x = (Screen.width / scale - WindowWidth) / 2f;
                    s_WindowRect.y = (Screen.height / scale - WindowHeight) / 2f;
                    s_Positioned = true;
                }

                s_WindowRect = GUILayout.Window(WindowId, s_WindowRect, DrawWindow, Localization.GetString("creature_list.title"));
                s_WindowRect.x = Mathf.Clamp(s_WindowRect.x, 0f, Mathf.Max(0f, Screen.width / scale - WindowWidth));
                s_WindowRect.y = Mathf.Clamp(s_WindowRect.y, 0f, Mathf.Max(0f, Screen.height / scale - 60f));
                GUI.matrix = previousMatrix;
            }
            catch (Exception ex)
            {
                Close();
                Main.Logger?.Error($"[SummonsTransitionFix] The creature list window failed: {ex}");
            }
        }

        public static void DrawShortcutSetting(Settings settings)
        {
            string shortcut = ShortcutRules.Describe(
                settings.CreatureListKey.ToString(),
                settings.CreatureListKeyNeedsCtrl,
                settings.CreatureListKeyNeedsShift,
                settings.CreatureListKeyNeedsAlt);
            string waiting = Localization.GetString("setting.creature_list_shortcut.waiting");

            GUILayout.BeginHorizontal();
            GUILayout.Label(Localization.GetString("setting.creature_list_shortcut.title"), GUILayout.ExpandWidth(false));
            if (GUILayout.Button(s_WaitingForShortcut ? waiting : shortcut, GUILayout.Width(ShortcutButtonWidth)))
            {
                s_WaitingForShortcut = !s_WaitingForShortcut;
            }
            GUILayout.EndHorizontal();

            if (!s_WaitingForShortcut) return;

            Event pressed = Event.current;
            if (pressed.type != EventType.KeyDown || pressed.keyCode == KeyCode.None) return;

            if (pressed.keyCode == KeyCode.Escape)
            {
                s_WaitingForShortcut = false;
                return;
            }

            if (!ShortcutRules.CanBeAShortcut(pressed.keyCode.ToString())) return;

            settings.CreatureListKey = pressed.keyCode;
            settings.CreatureListKeyNeedsCtrl = pressed.control;
            settings.CreatureListKeyNeedsShift = pressed.shift;
            settings.CreatureListKeyNeedsAlt = pressed.alt;
            s_WaitingForShortcut = false;
            pressed.Use();
            Main.SaveSettings();
        }

        public static void StopWaitingForShortcut()
        {
            s_WaitingForShortcut = false;
        }

        public static void DrawCreatureRows()
        {
            if (Event.current.type == EventType.Layout)
            {
                RefreshRows();
            }

            if (!StayList.IsAvailable)
            {
                GUILayout.Label(Localization.GetString("creature_list.unavailable"));
                return;
            }

            GUILayout.Label(Localization.GetString("creature_list.help"));

            if (s_Rows.Count == 0)
            {
                GUILayout.Label(Localization.GetString("creature_list.empty"));
                return;
            }

            foreach (var row in s_Rows)
            {
                bool staying = StayList.IsStaying(row.Creature);
                string label = staying ? Localization.GetString("creature_list.stays") : Localization.GetString("creature_list.follows");

                GUILayout.BeginHorizontal();
                GUILayout.Label(row.Text, GUILayout.Width(NameWidth));
                bool follows = GUILayout.Toggle(!staying, label, GUILayout.Width(ButtonWidth));
                GUILayout.EndHorizontal();

                if (follows == staying)
                {
                    StayList.SetStaying(row.Creature, !follows);
                }
            }
        }

        public static void Close()
        {
            s_Open = false;

            if (s_ClickBlocker != null)
            {
                UnityEngine.Object.Destroy(s_ClickBlocker);
                s_ClickBlocker = null;
            }
        }

        private static void Open()
        {
            Localization.UpdateLocale();
            s_Rows.Clear();
            s_Open = true;
            s_Positioned = false;
            s_Scroll = Vector2.zero;
            CreateClickBlocker();
        }

        private static void DrawWindow(int windowId)
        {
            s_Scroll = GUILayout.BeginScrollView(s_Scroll);
            DrawCreatureRows();
            GUILayout.EndScrollView();

            if (GUILayout.Button(Localization.GetString("creature_list.close")))
            {
                Close();
            }

            GUI.DragWindow();
        }

        private static bool IsShortcutPressed(Settings? settings)
        {
            if (settings == null) return false;
            if (!ShortcutRules.CanBeAShortcut(settings.CreatureListKey.ToString())) return false;
            if (!Input.GetKeyDown(settings.CreatureListKey)) return false;

            return ShortcutRules.ModifiersMatch(
                settings.CreatureListKeyNeedsCtrl,
                settings.CreatureListKeyNeedsShift,
                settings.CreatureListKeyNeedsAlt,
                Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl),
                Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
                Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));
        }

        public static bool IsGameLoaded()
        {
            var game = Game.Instance;
            return game?.Player != null && game.LoadedAreaState?.MainState != null && !string.IsNullOrEmpty(game.Player.GameId);
        }

        private static void RefreshRows()
        {
            s_Rows.Clear();

            try
            {
                var game = Game.Instance;
                var area = game?.LoadedAreaState;
                var mainState = area?.MainState;
                if (game?.Player == null || area == null || mainState == null) return;

                var states = TransitionRules.SelectAreaStates(mainState, area.GetAdditionalSceneStates());
                var crossState = game.Player.CrossSceneState;
                if (crossState != null && !states.Contains(crossState))
                {
                    states.Add(crossState);
                }

                var creatures = states
                    .SelectMany(state => state.AllEntityData.OfType<UnitEntityData>())
                    .Where(unit => unit.IsInGame && Minions.CanBeToldToStay(unit))
                    .Distinct();

                foreach (var creature in creatures)
                {
                    s_Rows.Add(new CreatureRow(creature, $"{creature.CharacterName}  ({creature.HPLeft}/{creature.MaxHP})"));
                }
            }
            catch (Exception ex)
            {
                s_Rows.Clear();
                Main.Logger?.Error($"[SummonsTransitionFix] Failed to list the raised creatures: {ex}");
            }
        }

        private static void CreateClickBlocker()
        {
            if (s_ClickBlocker != null) return;

            s_ClickBlocker = new GameObject("SummonsTransitionFix click blocker", typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = s_ClickBlocker.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            UnityEngine.Object.DontDestroyOnLoad(s_ClickBlocker);

            var panel = new GameObject("Panel", typeof(Image));
            panel.transform.SetParent(s_ClickBlocker.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        }

        private sealed class CreatureRow
        {
            public CreatureRow(UnitEntityData creature, string text)
            {
                Creature = creature;
                Text = text;
            }

            public UnitEntityData Creature { get; }

            public string Text { get; }
        }
    }
}
