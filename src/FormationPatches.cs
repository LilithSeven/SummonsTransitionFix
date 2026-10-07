using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker.Controllers.Units;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Formations;
using Kingmaker.UnitLogic.Parts;
using UnityEngine;

namespace SummonsTransitionFix
{
    [HarmonyPatch(typeof(SummonedUnitsController), "TickUnitFollowMaster")]
    public static class SummonedUnitsController_TickUnitFollowMaster_Patch
    {
        public static bool Prefix(UnitEntityData unit, UnitPartSummonedMonster part)
        {
            if (!Main.PlacesMinionsInFormation || unit == null || part == null) return true;

            try
            {
                if (!part.IsLinkedToSummoner || part.Summoner == null || !Minions.IsPartyMemberOrPet(part.Summoner)) return true;

                SummonPlacement.Tick(unit, part);
                return false;
            }
            catch (Exception ex)
            {
                FormationFailures.Report("Placing a summon in the formation", ex);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(FollowersFormationController), "PrepareFormation")]
    public static class FollowersFormationController_PrepareFormation_Patch
    {
        public static bool Prefix(UnitPartFollowedByUnits leader, IList<UnitEntityData> followers, Vector3 position, Dictionary<UnitEntityData, FollowerActionType> desiredActions)
        {
            if (!Main.PlacesMinionsInFormation) return true;

            try
            {
                if (!Minions.LeadsOnlyPlayerMinions(leader)) return true;

                FollowerPlacement.Prepare(leader, followers, position, desiredActions);
                return false;
            }
            catch (Exception ex)
            {
                FormationFailures.Report("Placing raised creatures in the formation", ex);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(FollowersFormationController), "GetFollowersFrontPosition")]
    public static class FollowersFormationController_GetFollowersFrontPosition_Patch
    {
        public static bool Prefix(UnitEntityData leader, ref Vector3 __result)
        {
            if (!Main.PlacesMinionsInFormation || leader == null) return true;

            try
            {
                if (!Minions.LeadsOnlyPlayerMinions(leader.Get<UnitPartFollowedByUnits>())) return true;

                __result = FollowerPlacement.GetUnitDestination(leader);
                return false;
            }
            catch (Exception ex)
            {
                FormationFailures.Report("Finding where raised creatures gather", ex);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(PartyAutoFormationHelper), nameof(PartyAutoFormationHelper.Setup))]
    public static class PartyAutoFormationHelper_Setup_Patch
    {
        public static bool Prefix(PartyFormationAuto formation)
        {
            if (!Main.ArrangesPartyFormation || formation == null) return true;

            try
            {
                PartyAutoFormation.Setup(formation);
                return false;
            }
            catch (Exception ex)
            {
                FormationFailures.Report("Arranging the automatic party formation", ex);
                return true;
            }
        }
    }

    public static class FormationFailures
    {
        private static readonly HashSet<string> s_Reported = new HashSet<string>();

        public static void Report(string action, Exception exception)
        {
            if (!s_Reported.Add(action)) return;

            Main.Logger?.Error($"[SummonsTransitionFix] {action} failed, the game does it its own way instead: {exception}");
        }
    }
}
