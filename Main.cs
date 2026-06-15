using System;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using UnityModManagerNet;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.View;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.View.MapObjects;
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

    // PATCH 1 : Protéger les serviteurs de la destruction pure et simple
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
                    Main.Logger?.Log($"[SummonsTransitionFix] Destruction bloquée pour le serviteur en transit : {unit.CharacterName}.");
                    return false; 
                }
            }
            return true;
        }
    }

    // PATCH 1bis (NOUVEAU) : Le "Bouclier d'Intégrité" (Empêche l'extinction des buffs et la perte d'allégeance)
    [HarmonyPatch(typeof(EntityDataBase), nameof(EntityDataBase.IsInGame), MethodType.Setter)]
    public static class EntityDataBase_IsInGame_Patch
    {
        public static bool Prefix(EntityDataBase __instance, bool value)
        {
            if (!Main.Enabled) return true;

            // Si le jeu tente de désactiver l'entité (value == false)
            if (!value && __instance is UnitEntityData unit && Main.IsPlayerMinion(unit))
            {
                // Si l'unité a été promue dans le cache inter-scènes pour le voyage
                if (unit.HoldingState != null && unit.HoldingState == Game.Instance.Player?.CrossSceneState)
                {
                    // Bloquer le passage à false empêche le moteur d'Owlcat de déclencher Deactivate()
                    // sur les buffs de la Liche, préservant ainsi la faction alliée et le statut de Mort-Vivant.
                    Main.Logger?.Log($"[SummonsTransitionFix] Maintien de IsInGame=true pour {unit.CharacterName}. (Préserve l'intégrité de la faction et des buffs).");
                    return false; 
                }
            }
            return true;
        }
    }

    // PATCH 2 : Promotion globale lors du changement de zone
    [HarmonyPatch(typeof(AreaTransitionGroupCommand), nameof(AreaTransitionGroupCommand.ExecuteTransition))]
    public static class AreaTransitionGroupCommand_ExecuteTransition_Patch
    {
        public static void Prefix(AreaTransitionPart areaTransition)
        {
            if (!Main.Enabled || Main.ModSettings == null || !Main.ModSettings.EnableGlobalTransitions) return;

            try
            {
                var crossState = Game.Instance.Player?.CrossSceneState;
                if (crossState == null) return;

                var state = Game.Instance.State;
                if (state == null || state.Units == null) return;

                var units = state.Units.ToList();
                foreach (var unit in units)
                {
                    if (Main.IsPlayerMinion(unit))
                    {
                        var oldState = unit.HoldingState;
                        if (oldState != null && oldState != crossState)
                        {
                            oldState.AllEntityData.Remove(unit);
                            crossState.AddEntityData(unit);
                            Main.Logger?.Log($"[SummonsTransitionFix] Promotion de {unit.CharacterName} ({unit.UniqueId}) vers CrossSceneState.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Main.Logger?.Error($"[SummonsTransitionFix] Erreur lors de la promotion globale : {ex}");
            }
        }
    }

    // PATCH 3 : Gestion de la ré-introduction locale et du repositionnement après transition
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
                    var minions = crossState.AllEntityData.OfType<UnitEntityData>().Where(Main.IsPlayerMinion).ToList();
                    foreach (var minion in minions)
                    {
                        crossState.AllEntityData.Remove(minion);
                        mainState.AddEntityData(minion);
                        Main.Logger?.Log($"[SummonsTransitionFix] Ré-introduction de {minion.CharacterName} vers le MainState.");
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
            if (!Main.Enabled || Main.ModSettings == null) return;

            try
            {
                var mainState = Game.Instance.LoadedAreaState?.MainState;
                if (mainState == null) return;

                var allUnits = mainState.AllEntityData.OfType<UnitEntityData>().ToList();
                
                foreach (var unit in allUnits)
                {
                    if (Main.IsPlayerMinion(unit))
                    {
                        var master = Main.GetMinionMaster(unit);
                        if (master != null)
                        {
                            unit.Position = master.Position;
                            unit.Orientation = master.Orientation;
                            if (unit.View != null)
                            {
                                unit.View.transform.position = master.Position;
                                unit.View.transform.rotation = Quaternion.Euler(0f, master.Orientation, 0f);
                                unit.View.UpdateViewActive();
                            }
                            
                            unit.IsInGame = true; 
                            
                            Main.Logger?.Log($"[SummonsTransitionFix] Repositionnement réussi de {unit.CharacterName} près de son maître.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Main.Logger?.Error($"[SummonsTransitionFix] Erreur lors du repositionnement des serviteurs : {ex}");
            }
        }
    }
	
    // PATCH 4 : Exclure les serviteurs du calcul de formation de marche pour éviter les IndexOutOfRangeException
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