using System;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Persistence;

namespace SummonsTransitionFix
{
    public static class StayList
    {
        public static bool IsAvailable { get; private set; }

        public static void StartFollowingSaves(Harmony harmony)
        {
            try
            {
                var prepareSave = AccessTools.Method(typeof(SaveManager), nameof(SaveManager.PrepareSave), new[] { typeof(SaveInfo) });
                var loadRoutine = AccessTools.Method(typeof(SaveManager), nameof(SaveManager.LoadRoutine), new[] { typeof(SaveInfo), typeof(bool) });
                if (prepareSave == null || loadRoutine == null)
                {
                    Main.Logger?.Error("[SummonsTransitionFix] The game methods that write and read saves were not found: the raised creature list is turned off.");
                    return;
                }

                harmony.Patch(prepareSave, postfix: new HarmonyMethod(typeof(StayList), nameof(AfterSaveIsPrepared)));
                harmony.Patch(loadRoutine, prefix: new HarmonyMethod(typeof(StayList), nameof(BeforeSaveIsLoaded)));
                IsAvailable = true;
            }
            catch (Exception ex)
            {
                IsAvailable = false;
                Main.Logger?.Error($"[SummonsTransitionFix] Failed to follow saves, the raised creature list is turned off: {ex}");
            }
        }

        public static bool IsStaying(UnitEntityData unit)
        {
            if (!IsAvailable || unit == null) return false;

            return StayRules.IsStaying(Main.ModSettings?.StayingCreatures, Game.Instance?.Player?.GameId, unit.UniqueId);
        }

        public static void SetStaying(UnitEntityData unit, bool staying)
        {
            if (!IsAvailable || unit == null) return;

            if (!StayRules.SetStaying(Main.ModSettings?.StayingCreatures, Game.Instance?.Player?.GameId, unit.UniqueId, staying)) return;

            Main.SaveSettings();
            Main.Logger?.Log(staying
                ? $"[SummonsTransitionFix] {unit.CharacterName} now stays in this area."
                : $"[SummonsTransitionFix] {unit.CharacterName} now follows the party again.");
        }

        public static void AfterSaveIsPrepared(SaveInfo save)
        {
            try
            {
                var settings = Main.ModSettings;
                if (settings == null || save == null) return;

                bool changed = StayRules.RememberSave(
                    settings.StayingCreatures,
                    settings.StaySnapshots,
                    save.GameId,
                    save.GameTotalTime.Ticks,
                    save.SystemSaveTime.Ticks,
                    save.SystemSaveTime.ToUniversalTime().Ticks);

                if (changed)
                {
                    Main.SaveSettings();
                }
            }
            catch (Exception ex)
            {
                Main.Logger?.Error($"[SummonsTransitionFix] Failed to remember which creatures stay for this save: {ex}");
            }
        }

        public static void BeforeSaveIsLoaded(SaveInfo saveInfo)
        {
            var settings = Main.ModSettings;
            if (settings == null || saveInfo == null) return;

            try
            {
                bool changed = StayRules.RestoreSave(
                    settings.StayingCreatures,
                    settings.StaySnapshots,
                    saveInfo.GameId,
                    saveInfo.GameTotalTime.Ticks,
                    saveInfo.SystemSaveTime.Ticks,
                    saveInfo.SystemSaveTime.ToUniversalTime().Ticks);

                if (changed)
                {
                    Main.SaveSettings();
                    Main.Logger?.Log("[SummonsTransitionFix] Restored which raised creatures stay, as it was when this save was made.");
                }
            }
            catch (Exception ex)
            {
                if (StayRules.ForgetGame(settings.StayingCreatures, saveInfo.GameId))
                {
                    Main.SaveSettings();
                }

                Main.Logger?.Error($"[SummonsTransitionFix] Failed to restore which creatures stay for this save, every creature follows: {ex}");
            }
        }
    }
}
