using System;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using UnityModManagerNet;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.View;
using UnityEngine;
using Kingmaker.EntitySystem;

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

            ModSettings.EnableLocalTransitions = UnityEngine.GUILayout.Toggle(
                ModSettings.EnableLocalTransitions, 
                Localization.GetString("setting.local_transitions.title")
            );

            ModSettings.EnableGlobalTransitions = UnityEngine.GUILayout.Toggle(
                ModSettings.EnableGlobalTransitions, 
                Localization.GetString("setting.global_transitions.title")
            );

            if (UnityEngine.GUI.changed)
            {
                ModSettings.Save(modEntry);
            }
        }

            public static bool IsPlayerMinion(UnitEntityData unit)
        {
            if (unit == null) return false;

            
            if (unit.Descriptor?.State?.IsDead == true) return false;

            
            if (!unit.IsPlayerFaction) return false;

            
            if (IsPartyMemberOrPet(unit))
                return false;

            var summonedPart = unit.Get<UnitPartSummonedMonster>();
            
            if (summonedPart != null && summonedPart.Summoner != null)
            {
                if (IsPartyMemberOrPet(summonedPart.Summoner))
                {
                    return true;
                }
            }

            if (unit.Buffs != null)
            {
                foreach (var buff in unit.Buffs)
                {
                    if (buff.Blueprint != null && !string.IsNullOrEmpty(buff.Blueprint.name))
                    {
                        if (buff.Blueprint.name.IndexOf("Repurpose", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
		
        public static UnitEntityData? GetMinionMaster(UnitEntityData unit)
        {
            if (unit == null) return null;

            var summonedPart = unit.Get<UnitPartSummonedMonster>();
            if (summonedPart != null && summonedPart.Summoner != null)
            {
                return summonedPart.Summoner;
            }

            return Game.Instance.Player?.MainCharacter.Value;
        }

        public static bool IsPartyMemberOrPet(UnitEntityData unit)
        {
            if (unit == null) return false;
            if (unit.IsMainCharacter) return true;

            var companion = unit.Get<UnitPartCompanion>();
            if (companion != null && companion.State != CompanionState.ExCompanion && companion.State != CompanionState.Remote)
            {
                return true;
            }

            var petPart = unit.Get<UnitPartPet>();
            if (petPart != null)
            {
                var master = petPart.Master;
                if (master != null)
                {
                    return IsPartyMemberOrPet(master);
                }
            }

            return false;
        }
    }

    
    [HarmonyPatch(typeof(EntityDataBase), nameof(EntityDataBase.MarkForDestroy))]
    public static class EntityDataBase_MarkForDestroy_Patch
    {
        public static bool Prefix(EntityDataBase __instance)
        {
            if (!Main.Enabled) return true;

            if (__instance is UnitEntityData unit && Main.IsPlayerMinion(unit))
            {
                if (unit.HoldingState != null && unit.HoldingState == Game.Instance.Player?.CrossSceneState)
                {
                    return false; 
                }
            }
            return true;
        }
    }

    
    [HarmonyPatch(typeof(EntityDataBase), nameof(EntityDataBase.IsInGame), MethodType.Setter)]
    public static class EntityDataBase_IsInGame_Patch
    {
        public static bool Prefix(EntityDataBase __instance, bool value)
        {
            if (!Main.Enabled) return true;

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
                var loadedAreaState = Game.Instance.LoadedAreaState;
                if (crossState == null || loadedAreaState == null) return;

                var mainState = loadedAreaState.MainState;
                if (mainState == null) return;

                var minions = mainState.AllEntityData.OfType<UnitEntityData>().Where(Main.IsPlayerMinion).ToList();
                foreach (var minion in minions)
                {
                    mainState.AllEntityData.Remove(minion);
                    crossState.AddEntityData(minion);
                    Main.Logger?.Log($"[SummonsTransitionFix] Promotion robuste de {minion.CharacterName} via HandleAreaBeginUnloading.");
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
            if (!Main.Enabled || Main.ModSettings == null) return;

            try
            {
                var crossState = Game.Instance.Player?.CrossSceneState;
                var mainState = Game.Instance.LoadedAreaState?.MainState;
                if (crossState != null && mainState != null)
                {
                    
                    var allCrossEntities = crossState.AllEntityData.OfType<UnitEntityData>().ToList();
                    
                    foreach (var unit in allCrossEntities)
                    {
                        
                        if (!Main.IsPartyMemberOrPet(unit))
                        {
                           
                            crossState.AllEntityData.Remove(unit);
                            mainState.AddEntityData(unit);
                            Main.Logger?.Log($"[SummonsTransitionFix] Descente du bus inter-scènes pour : {unit.CharacterName}.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Main.Logger?.Error($"[SummonsTransitionFix] Erreur lors de la ré-introduction locale : {ex}");
            }
        }

        
        public static void Postfix(AreaEnterPoint __instance)
        {
            if (!Main.Enabled || Main.ModSettings == null || !Main.ModSettings.EnableLocalTransitions) return;

            try
            {
                var mainState = Game.Instance.LoadedAreaState?.MainState;
                if (mainState == null) return;

                var minions = mainState.AllEntityData.OfType<UnitEntityData>().Where(Main.IsPlayerMinion).ToList();
                
                foreach (var unit in minions)
                {
                    var master = Main.GetMinionMaster(unit);
                    if (master != null)
                    {
                        
                        unit.Commands.InterruptAll(true);
                        if (unit.View != null) unit.View.StopMoving();

                        
                        unit.Position = master.Position;
                        unit.Orientation = master.Orientation;
                        
                        if (unit.View != null)
                        {
                            unit.View.transform.position = master.Position;
                            unit.View.transform.rotation = Quaternion.Euler(0f, master.Orientation, 0f);
                            unit.View.UpdateViewActive();
                        }
                        
                        unit.IsInGame = true; 
                        Main.Logger?.Log($"[SummonsTransitionFix] Repositionnement absolu de {unit.CharacterName} près de son maître.");
                    }
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
            if (!Main.Enabled) return true;

            if (Main.IsPlayerMinion(character))
            {
                __result = false;
                return false; 
            }
            return true;
        }
    }
}