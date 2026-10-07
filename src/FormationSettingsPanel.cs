using System.Globalization;
using UnityEngine;

namespace SummonsTransitionFix
{
    public static class FormationSettingsPanel
    {
        private const float IndentWidth = 24f;
        private const float LabelWidth = 330f;
        private const float SliderWidth = 220f;
        private const float ValueWidth = 50f;
        private const float ButtonWidth = 280f;
        private const string DistanceFormat = "0.0";
        private const string FactorFormat = "0.00";

        private static bool s_ShowsMinionTuning;
        private static bool s_DrawsMinionOptions;
        private static bool s_DrawsMinionTuning;
        private static bool s_DrawsCatchUp;
        private static bool s_HasUnsavedTuning;

        public static void Draw(Settings settings)
        {
            if (Event.current.type == EventType.Layout)
            {
                s_DrawsMinionOptions = settings.EnableMinionFormation;
                s_DrawsMinionTuning = s_ShowsMinionTuning;
                s_DrawsCatchUp = settings.EnableMinionSpeed;
            }

            bool togglesChanged = false;
            bool tuningChanged = false;

            GUILayout.Label("<b>" + Localization.GetString("formation.title") + "</b>");

            togglesChanged |= DrawToggle(ref settings.EnableMinionFormation, Localization.GetString("setting.minion_formation.title"));
            GUILayout.Label(Localization.GetString("setting.minion_formation.help"));

            if (s_DrawsMinionOptions)
            {
                BeginIndentedBlock();

                togglesChanged |= DrawToggle(ref settings.EnableMinionSpeed, Localization.GetString("setting.minion_speed.title"));
                GUILayout.Label(Localization.GetString("setting.minion_speed.help"));

                s_ShowsMinionTuning = DrawTuningButton(s_ShowsMinionTuning);
                if (s_DrawsMinionTuning)
                {
                    tuningChanged |= DrawMinionTuning(settings);
                }

                EndIndentedBlock();
            }

            if (togglesChanged)
            {
                s_HasUnsavedTuning = false;
                Main.SaveSettings();
            }
            else if (tuningChanged)
            {
                s_HasUnsavedTuning = true;
            }
        }

        public static void SaveUnsavedTuning()
        {
            if (!s_HasUnsavedTuning) return;

            s_HasUnsavedTuning = false;
            Main.SaveSettings();
        }

        private static bool DrawMinionTuning(Settings settings)
        {
            bool changed = false;

            GUILayout.Label(Localization.GetString("tuning.summons.title"));
            changed |= DrawSlider(ref settings.SummonFrontMargin, Localization.GetString("tuning.summons.front_margin"), 0f, 10f, DistanceFormat);
            changed |= DrawSlider(ref settings.SummonBackMargin, Localization.GetString("tuning.summons.back_margin"), 0f, 10f, DistanceFormat);
            changed |= DrawSlider(ref settings.SummonLateralSpacing, Localization.GetString("tuning.summons.lateral_spacing"), 1.5f, 5f, DistanceFormat);
            changed |= DrawSlider(ref settings.SummonCorridorWidth, Localization.GetString("tuning.summons.corridor_width"), 0f, 4f, DistanceFormat);
            changed |= DrawSlider(ref settings.SummonRepathDistance, Localization.GetString("tuning.summons.repath_distance"), 1f, 12f, DistanceFormat);

            if (s_DrawsCatchUp)
            {
                changed |= DrawSlider(ref settings.SummonCatchUpFactor, Localization.GetString("tuning.summons.catch_up"), 1f, 1.5f, FactorFormat);
            }

            GUILayout.Label(Localization.GetString("tuning.raised.title"));
            changed |= DrawSlider(ref settings.RaisedFrontPush, Localization.GetString("tuning.raised.front_push"), 0f, 10f, DistanceFormat);
            changed |= DrawSlider(ref settings.RaisedLineGap, Localization.GetString("tuning.raised.line_gap"), 1.5f, 6f, DistanceFormat);
            changed |= DrawSlider(ref settings.RaisedLateralSpacing, Localization.GetString("tuning.raised.lateral_spacing"), 1.5f, 5f, DistanceFormat);
            changed |= DrawSlider(ref settings.RaisedCorridorWidth, Localization.GetString("tuning.raised.corridor_width"), 0f, 4f, DistanceFormat);

            if (GUILayout.Button(Localization.GetString("formation.tuning.reset"), GUILayout.Width(ButtonWidth)))
            {
                var defaults = new Settings();
                settings.SummonFrontMargin = defaults.SummonFrontMargin;
                settings.SummonBackMargin = defaults.SummonBackMargin;
                settings.SummonLateralSpacing = defaults.SummonLateralSpacing;
                settings.SummonCorridorWidth = defaults.SummonCorridorWidth;
                settings.SummonRepathDistance = defaults.SummonRepathDistance;
                settings.SummonCatchUpFactor = defaults.SummonCatchUpFactor;
                settings.RaisedFrontPush = defaults.RaisedFrontPush;
                settings.RaisedLineGap = defaults.RaisedLineGap;
                settings.RaisedLateralSpacing = defaults.RaisedLateralSpacing;
                settings.RaisedCorridorWidth = defaults.RaisedCorridorWidth;
                changed = true;
            }

            return changed;
        }

        private static bool DrawToggle(ref bool value, string title)
        {
            bool drawn = GUILayout.Toggle(value, title);
            if (drawn == value) return false;

            value = drawn;
            return true;
        }

        private static bool DrawTuningButton(bool shown)
        {
            string title = shown ? Localization.GetString("formation.tuning.hide") : Localization.GetString("formation.tuning.show");

            return GUILayout.Button(title, GUILayout.Width(ButtonWidth)) ? !shown : shown;
        }

        private static bool DrawSlider(ref float value, string label, float minimum, float maximum, string format)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(LabelWidth));
            float drawn = GUILayout.HorizontalSlider(value, minimum, maximum, GUILayout.Width(SliderWidth));
            GUILayout.Label(drawn.ToString(format, CultureInfo.InvariantCulture), GUILayout.Width(ValueWidth));
            GUILayout.EndHorizontal();

            if (Mathf.Approximately(drawn, value)) return false;

            value = drawn;
            return true;
        }

        private static void BeginIndentedBlock()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(IndentWidth);
            GUILayout.BeginVertical();
        }

        private static void EndIndentedBlock()
        {
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
    }
}
