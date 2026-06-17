using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.View;
using UnityEngine;
using UnityModManagerNet;

namespace SummonsTransitionFix
{
    public static class Main
    {
        public static UnityModManager.ModEntry.ModLogger? Logger { get; private set; }
        public static bool Enabled { get; private set; }
        public static Settings? ModSettings { get; private set; }

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
                Logger?.Error($"[SummonsTransitionFix] Erreur critique lors de l'application des patchs Harmony : {ex}");
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

        public static bool IsPlayerMinion(UnitEntityData unit)
        {
            if (unit == null) return false;
            if (unit.Descriptor?.State?.IsDead == true) return false;
            if (!unit.IsPlayerFaction) return false;
            if (IsPartyMemberOrPet(unit)) return false;

            var summonedPart = unit.Get<UnitPartSummonedMonster>();
            if (summonedPart != null && HasActiveSummonBuff(unit))
            {
                var summoner = summonedPart.Summoner;
                if (summoner != null && IsPartyMemberOrPet(summoner))
                {
                    return true;
                }
            }

            return GetRepurposeCaster(unit) != null;
        }

        public static UnitEntityData? GetMinionMaster(UnitEntityData unit)
        {
            if (unit == null) return null;

            var summonedPart = unit.Get<UnitPartSummonedMonster>();
            if (summonedPart != null)
            {
                var summoner = summonedPart.Summoner;
                if (summoner != null && IsPartyMemberOrPet(summoner))
                {
                    return summoner;
                }
            }

            return GetRepurposeCaster(unit);
        }

        public static bool IsPartyMemberOrPet(UnitEntityData unit)
        {
            if (unit == null) return false;
            if (unit.IsMainCharacter) return true;

            var player = Game.Instance?.Player;
            if (player != null)
            {
                if (player.Party.Contains(unit)) return true;
                if (player.PartyAndPets.Contains(unit)) return true;
            }

            var petPart = unit.Get<UnitPartPet>();
            if (petPart?.Master != null && petPart.Master != unit)
            {
                return IsPartyMemberOrPet(petPart.Master);
            }

            var companion = unit.Get<UnitPartCompanion>();
            if (companion != null && companion.State != CompanionState.None)
            {
                return true;
            }

            return false;
        }

        public static void MoveEntityWithoutDispose(SceneEntitiesState from, SceneEntitiesState to, UnitEntityData unit)
        {
            if (from == null || to == null || unit == null || from == to) return;

            from.AllEntityData.Remove(unit);

            if (!to.AllEntityData.Any(e => e.UniqueId == unit.UniqueId))
            {
                to.AddEntityData(unit);
            }
        }

        private static bool HasActiveSummonBuff(UnitEntityData unit)
        {
            var summonedBuff = Game.Instance?.BlueprintRoot?.SystemMechanics?.SummonedUnitBuff;
            return summonedBuff != null && unit.Buffs != null && unit.Buffs.HasFact(summonedBuff);
        }

        private static UnitEntityData? GetRepurposeCaster(UnitEntityData unit)
        {
            if (unit?.Buffs == null) return null;

            foreach (var buff in unit.Buffs)
            {
                var name = buff.Blueprint?.name;
                if (string.IsNullOrEmpty(name)) continue;
                if (name.IndexOf("Repurpose", StringComparison.OrdinalIgnoreCase) < 0) continue;

                var caster = buff.Context?.MaybeCaster;
                if (caster != null && IsPartyMemberOrPet(caster))
                {
                    return caster;
                }
            }

            return null;
        }
    }

    [HarmonyPatch(typeof(EntityDataBase), nameof(EntityDataBase.IsInGame), MethodType.Setter)]
    public static class EntityDataBase_IsInGame_Patch
    {
        public static bool Prefix(EntityDataBase __instance, bool value)
        {
            if (!Main.Enabled || Main.ModSettings == null || !Main.ModSettings.EnableGlobalTransitions) return true;

            if (!value && __instance is UnitEntityData unit && Main.IsPlayerMinion(unit))
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
            if (!Main.Enabled || Main.ModSettings == null || !Main.ModSettings.EnableGlobalTransitions) return;
            if (forDispose) return;

            try
            {
                var crossState = Game.Instance.Player?.CrossSceneState;
                var mainState = Game.Instance.LoadedAreaState?.MainState;
                if (crossState == null || mainState == null) return;

                var minions = mainState.AllEntityData.OfType<UnitEntityData>().Where(Main.IsPlayerMinion).ToList();

                foreach (var minion in minions)
                {
                    Main.MoveEntityWithoutDispose(mainState, crossState, minion);
                    minion.ClearDestroyMark();
                    Main.Logger?.Log($"[SummonsTransitionFix] Promotion de {minion.CharacterName} vers CrossSceneState.");
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
            if (!Main.Enabled || Main.ModSettings == null || !Main.ModSettings.EnableGlobalTransitions) return;

            try
            {
                var crossState = Game.Instance.Player?.CrossSceneState;
                var mainState = Game.Instance.LoadedAreaState?.MainState;
                if (crossState == null || mainState == null) return;

                var minions = crossState.AllEntityData.OfType<UnitEntityData>().Where(Main.IsPlayerMinion).ToList();

                foreach (var minion in minions)
                {
                    Main.MoveEntityWithoutDispose(crossState, mainState, minion);
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
            if (!Main.Enabled || Main.ModSettings == null) return;
            if (!Main.ModSettings.EnableLocalTransitions && !Main.ModSettings.EnableGlobalTransitions) return;

            try
            {
                var mainState = Game.Instance.LoadedAreaState?.MainState;
                if (mainState == null) return;

                var minions = mainState.AllEntityData.OfType<UnitEntityData>().Where(Main.IsPlayerMinion).ToList();

                foreach (var unit in minions)
                {
                    var master = Main.GetMinionMaster(unit);
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
            if (!Main.Enabled || Main.ModSettings == null) return true;
            if (!Main.ModSettings.EnableLocalTransitions && !Main.ModSettings.EnableGlobalTransitions) return true;

            if (Main.IsPlayerMinion(character))
            {
                __result = false;
                return false;
            }

            return true;
        }
    }
}