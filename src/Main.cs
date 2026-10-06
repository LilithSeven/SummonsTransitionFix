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

        public static bool HandlesGlobalTransitions => TransitionRules.HandlesGlobalTransitions(Enabled, ModSettings?.EnableGlobalTransitions);

        public static bool HandlesAnyTransition => TransitionRules.HandlesAnyTransition(Enabled, ModSettings?.EnableLocalTransitions, ModSettings?.EnableGlobalTransitions);

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Logger = modEntry.Logger;
            ModSettings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            Enabled = true;

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;

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

            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            Enabled = value;
            return true;
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

            if (GUI.changed)
            {
                ModSettings.Save(modEntry);
            }
        }
    }
}
