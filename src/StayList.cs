using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Persistence;

namespace SummonsTransitionFix
{
    public static class StayList
    {
        private static bool s_ReviewFailureReported;
        private static bool s_AreaNameFailureReported;

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

            if (!StayRules.SetStaying(Main.ModSettings?.StayingCreatures, Game.Instance?.Player?.GameId, unit.UniqueId, staying, unit.CharacterName, GetLoadedAreaName())) return;

            Main.SaveSettings();
            Main.Logger?.Log(staying
                ? $"[SummonsTransitionFix] {unit.CharacterName} now stays in this area."
                : $"[SummonsTransitionFix] {unit.CharacterName} now follows the party again.");
        }

        public static void Forget(StayEntry creature)
        {
            if (!IsAvailable || creature == null) return;

            if (!StayRules.SetStaying(Main.ModSettings?.StayingCreatures, Game.Instance?.Player?.GameId, creature.UnitId, false)) return;

            Main.SaveSettings();
            Main.Logger?.Log($"[SummonsTransitionFix] Forgot that {creature.CreatureName} was told to stay ({creature.AreaName}).");
        }

        public static List<UnitEntityData>? ListLoadedUnits()
        {
            var game = Game.Instance;
            var area = game?.LoadedAreaState;
            var mainState = area?.MainState;
            if (game?.Player == null || area == null || mainState == null) return null;

            var states = TransitionRules.SelectAreaStates(mainState, area.GetAdditionalSceneStates());
            var crossState = game.Player.CrossSceneState;
            if (crossState != null && !states.Contains(crossState))
            {
                states.Add(crossState);
            }

            return states.SelectMany(state => state.AllEntityData.OfType<UnitEntityData>()).Distinct().ToList();
        }

        public static List<StayEntry> ReviewLoadedArea(ICollection<UnitEntityData>? units = null)
        {
            var elsewhere = new List<StayEntry>();
            if (!IsAvailable) return elsewhere;

            try
            {
                var entries = Main.ModSettings?.StayingCreatures;
                string? gameId = Game.Instance?.Player?.GameId;
                if (entries == null || entries.Count == 0 || string.IsNullOrEmpty(gameId)) return elsewhere;

                units ??= ListLoadedUnits();
                if (units == null) return elsewhere;

                string areaName = GetLoadedAreaName();
                var presentUnitIds = new HashSet<string>();
                bool changed = false;

                foreach (var unit in units)
                {
                    if (unit == null || string.IsNullOrEmpty(unit.UniqueId)) continue;

                    presentUnitIds.Add(unit.UniqueId);
                    if (!StayRules.IsStaying(entries, gameId, unit.UniqueId)) continue;

                    var verdict = Minions.Classify(unit);
                    if (TransitionRules.CanBeToldToStay(verdict))
                    {
                        changed |= StayRules.Describe(entries, gameId, unit.UniqueId, unit.CharacterName, areaName);
                    }
                    else if (TransitionRules.EndsStayOrder(verdict) && StayRules.SetStaying(entries, gameId, unit.UniqueId, false))
                    {
                        changed = true;
                        Main.Logger?.Log($"[SummonsTransitionFix] {unit.CharacterName} is dead and is no longer in the list of creatures told to stay.");
                    }
                }

                if (changed)
                {
                    Main.SaveSettings();
                }

                return StayRules.ListAbsent(entries, gameId, presentUnitIds);
            }
            catch (Exception ex)
            {
                if (!s_ReviewFailureReported)
                {
                    s_ReviewFailureReported = true;
                    Main.Logger?.Error($"[SummonsTransitionFix] Failed to review the creatures told to stay, their orders are left as they are: {ex}");
                }

                return new List<StayEntry>();
            }
        }

        private static string GetLoadedAreaName()
        {
            try
            {
                return Game.Instance?.CurrentlyLoadedArea?.AreaDisplayName ?? string.Empty;
            }
            catch (Exception ex)
            {
                if (!s_AreaNameFailureReported)
                {
                    s_AreaNameFailureReported = true;
                    Main.Logger?.Error($"[SummonsTransitionFix] Failed to read the name of this area, the list shows it as unknown: {ex}");
                }

                return string.Empty;
            }
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
