using System;
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
            if (!Main.HandlesGlobalTransitions) return;
            if (forDispose) return;

            try
            {
                var crossState = Game.Instance.Player?.CrossSceneState;
                var area = Game.Instance.LoadedAreaState;
                var mainState = area?.MainState;
                if (crossState == null || area == null || mainState == null) return;

                foreach (var state in TransitionRules.SelectAreaStates(mainState, area.GetAdditionalSceneStates()))
                {
                    var minions = state.AllEntityData.OfType<UnitEntityData>().Where(Minions.IsPlayerMinion).ToList();

                    foreach (var minion in minions)
                    {
                        Minions.MoveEntityWithoutDispose(state, crossState, minion);
                        minion.ClearDestroyMark();
                        Main.Logger?.Log($"[SummonsTransitionFix] Promotion de {minion.CharacterName} vers CrossSceneState.");
                    }
                }
            }
            catch (Exception ex)
            {
                Main.Logger?.Error($"[SummonsTransitionFix] Erreur de promotion : {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(AreaEnterPoint), nameof(AreaEnterPoint.PositionCharacters))]
    public static class AreaEnterPoint_PositionCharacters_Patch
    {
        public static void Prefix(AreaEnterPoint __instance)
        {
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
                    Main.Logger?.Log($"[SummonsTransitionFix] Réintroduction de {minion.CharacterName} dans MainState.");
                }

                if (minions.Count > 0)
                {
                    Game.Instance.Player?.InvalidateCharacterLists();
                }
            }
            catch (Exception ex)
            {
                Main.Logger?.Error($"[SummonsTransitionFix] Erreur lors de la réintroduction : {ex}");
            }
        }

        public static void Postfix(AreaEnterPoint __instance)
        {
            if (!Main.HandlesAnyTransition) return;

            try
            {
                var area = Game.Instance.LoadedAreaState;
                var mainState = area?.MainState;
                if (area == null || mainState == null) return;

                var minions = TransitionRules.SelectAreaStates(mainState, area.GetAdditionalSceneStates())
                    .SelectMany(state => state.AllEntityData.OfType<UnitEntityData>())
                    .Where(Minions.IsPlayerMinion)
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

                    Main.Logger?.Log($"[SummonsTransitionFix] Repositionnement de {unit.CharacterName} près de son maître.");
                }
            }
            catch (Exception ex)
            {
                Main.Logger?.Error($"[SummonsTransitionFix] Erreur repositionnement : {ex}");
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
