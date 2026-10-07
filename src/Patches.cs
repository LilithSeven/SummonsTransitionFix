using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.View;

namespace SummonsTransitionFix
{
    [HarmonyPatch(typeof(EntityDataBase), nameof(EntityDataBase.IsInGame), MethodType.Setter)]
    public static class EntityDataBase_IsInGame_Patch
    {
        public static bool Prefix(EntityDataBase __instance, bool value)
        {
            if (!Main.HandlesGlobalTransitions) return true;

            if (!value && __instance is UnitEntityData unit && Minions.IsPlayerMinion(unit))
            {
                if (unit.HoldingState != null && unit.HoldingState == Game.Instance.Player?.CrossSceneState)
                {
                    return false;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.HandleAreaBeginUnloading))]
    public static class Game_HandleAreaBeginUnloading_Patch
    {
        public static void Prefix(bool forDispose)
        {
            if (forDispose) return;

            Diagnostics.ReportUnits("Leaving area");

            if (!Main.HandlesGlobalTransitions) return;

            StayList.ReviewLoadedArea();

            try
            {
                var crossState = Game.Instance.Player?.CrossSceneState;
                var area = Game.Instance.LoadedAreaState;
                var mainState = area?.MainState;
                if (crossState == null || area == null || mainState == null) return;

                foreach (var state in TransitionRules.SelectAreaStates(mainState, area.GetAdditionalSceneStates()))
                {
                    var minions = state.AllEntityData.OfType<UnitEntityData>().Where(Minions.IsTakenAlong).ToList();

                    foreach (var minion in minions)
                    {
                        Minions.MoveEntityWithoutDispose(state, crossState, minion);
                        minion.ClearDestroyMark();
                        Main.Logger?.Log($"[SummonsTransitionFix] Moved {minion.CharacterName} to CrossSceneState.");
                    }
                }
            }
            catch (Exception ex)
            {
                Main.Logger?.Error($"[SummonsTransitionFix] Failed to move minions to CrossSceneState: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(AreaEnterPoint), nameof(AreaEnterPoint.PositionCharacters))]
    public static class AreaEnterPoint_PositionCharacters_Patch
    {
        private static readonly List<UnitEntityData> s_BroughtBack = new List<UnitEntityData>();

        public static void Prefix(AreaEnterPoint __instance)
        {
            s_BroughtBack.Clear();

            if (!Main.HandlesGlobalTransitions) return;

            try
            {
                var crossState = Game.Instance.Player?.CrossSceneState;
                var mainState = Game.Instance.LoadedAreaState?.MainState;
                if (crossState == null || mainState == null) return;

                var minions = crossState.AllEntityData.OfType<UnitEntityData>().Where(Minions.IsPlayerMinion).ToList();

                foreach (var minion in minions)
                {
                    Minions.MoveEntityWithoutDispose(crossState, mainState, minion);
                    minion.ClearDestroyMark();
                    Main.Logger?.Log($"[SummonsTransitionFix] Moved {minion.CharacterName} back to MainState.");
                }

                s_BroughtBack.AddRange(minions);

                if (minions.Count > 0)
                {
                    Game.Instance.Player?.InvalidateCharacterLists();
                }
            }
            catch (Exception ex)
            {
                Main.Logger?.Error($"[SummonsTransitionFix] Failed to move minions back to MainState: {ex}");
            }
        }

        public static void Postfix(AreaEnterPoint __instance)
        {
            PlaceMinionsNextToTheirMaster();
            s_BroughtBack.Clear();
            Diagnostics.ReportUnits("Arrived");
        }

        private static void PlaceMinionsNextToTheirMaster()
        {
            if (!Main.HandlesAnyTransition) return;

            try
            {
                var area = Game.Instance.LoadedAreaState;
                var mainState = area?.MainState;
                if (area == null || mainState == null) return;

                var minions = TransitionRules.SelectAreaStates(mainState, area.GetAdditionalSceneStates())
                    .SelectMany(state => state.AllEntityData.OfType<UnitEntityData>())
                    .Where(unit => Minions.IsTakenAlong(unit) || s_BroughtBack.Contains(unit))
                    .ToList();

                foreach (var unit in minions)
                {
                    var master = Minions.GetMinionMaster(unit);
                    if (master == null) continue;

                    unit.ClearDestroyMark();
                    unit.Commands.InterruptAll(true);

                    if (unit.View != null)
                    {
                        unit.View.StopMoving();
                    }

                    unit.Translocate(master.Position, master.Orientation);
                    unit.IsInGame = true;

                    if (unit.View != null)
                    {
                        unit.View.UpdateViewActive();
                    }

                    Main.Logger?.Log($"[SummonsTransitionFix] Placed {unit.CharacterName} next to its master.");
                }
            }
            catch (Exception ex)
            {
                Main.Logger?.Error($"[SummonsTransitionFix] Failed to place minions next to their master: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(AreaEnterPoint), nameof(AreaEnterPoint.ShouldMoveCharacterOnAreaEnterPoint))]
    public static class AreaEnterPoint_ShouldMoveCharacterOnAreaEnterPoint_Patch
    {
        public static bool Prefix(UnitEntityData character, ref bool __result)
        {
            if (!Main.HandlesAnyTransition) return true;

            if (Minions.IsPlayerMinion(character))
            {
                __result = false;
                return false;
            }

            return true;
        }
    }
}
