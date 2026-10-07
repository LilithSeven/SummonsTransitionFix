using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace SummonsTransitionFix
{
    public static class Main
    {
        public static UnityModManager.ModEntry.ModLogger? Logger { get; private set; }
        public static bool Enabled { get; private set; }
        public static Settings? ModSettings { get; private set; }
        private static UnityModManager.ModEntry? s_ModEntry;

        public static bool HandlesGlobalTransitions => TransitionRules.HandlesGlobalTransitions(Enabled, ModSettings?.EnableGlobalTransitions);

        public static bool HandlesAnyTransition => TransitionRules.HandlesAnyTransition(Enabled, ModSettings?.EnableLocalTransitions, ModSettings?.EnableGlobalTransitions);

        public static bool WritesDiagnosticReport => TransitionRules.WritesDiagnosticReport(Enabled, ModSettings?.EnableDiagnosticLog);

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Logger = modEntry.Logger;
            ModSettings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            Enabled = true;
            s_ModEntry = modEntry;

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnUpdate = CreatureListWindow.OnUpdate;
            modEntry.OnFixedGUI = CreatureListWindow.OnFixedGUI;
            modEntry.OnHideGUI = OnHideGUI;

            Localization.Init(modEntry.Path);

            try
            {
                var harmony = new Harmony(modEntry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception ex)
            {
                Logger?.Error($"[SummonsTransitionFix] Failed to apply Harmony patches: {ex}");
            }

            StayList.StartFollowingSaves(new Harmony(modEntry.Info.Id));

            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            Enabled = value;

            if (!value)
            {
                CreatureListWindow.Close();
            }

            return true;
        }

        private static void OnHideGUI(UnityModManager.ModEntry modEntry)
        {
            CreatureListWindow.StopWaitingForShortcut();
        }

        public static void SaveSettings()
        {
            try
            {
                if (s_ModEntry != null)
                {
                    ModSettings?.Save(s_ModEntry);
                }
            }
            catch (Exception ex)
            {
                Logger?.Error($"[SummonsTransitionFix] Failed to save the settings: {ex}");
            }
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            Localization.UpdateLocale();

            if (ModSettings == null) return;

            ModSettings.EnableLocalTransitions = GUILayout.Toggle(
                ModSettings.EnableLocalTransitions,
                Localization.GetString("setting.local_transitions.title")
            );

            ModSettings.EnableGlobalTransitions = GUILayout.Toggle(
                ModSettings.EnableGlobalTransitions,
                Localization.GetString("setting.global_transitions.title")
            );

            ModSettings.EnableDiagnosticLog = GUILayout.Toggle(
                ModSettings.EnableDiagnosticLog,
                Localization.GetString("setting.diagnostic_log.title")
            );

            if (GUI.changed)
            {
                ModSettings.Save(modEntry);
            }

            GUILayout.Space(10f);
            CreatureListWindow.DrawShortcutSetting(ModSettings);
            GUILayout.Space(10f);
            GUILayout.Label(Localization.GetString("creature_list.title"));
            CreatureListWindow.DrawCreatureRows();
        }
    }
}
