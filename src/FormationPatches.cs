using System;
using System.Collections.Generic;
using System.Linq;
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
        public static bool Prefix(UnitPartFollowedByUnits leader, ref IList<UnitEntityData> followers, Vector3 position, Dictionary<UnitEntityData, FollowerActionType> desiredActions)
        {
            if (!Main.PlacesMinionsInFormation || leader?.Owner == null || followers == null) return true;

            try
            {
                var minions = Minions.SelectPlayerMinions(followers);
                FormationActivity.Count(minions.Count, followers.Count - minions.Count);
                if (minions.Count == 0) return true;

                var leaderUnit = leader.Owner;
                var anchor = position == leaderUnit.Position ? position : FollowerPlacement.GetUnitDestination(leaderUnit);
                FollowerPlacement.Prepare(leader, minions, anchor, desiredActions);

                if (minions.Count == followers.Count) return false;

                followers = followers.Where(follower => !minions.Contains(follower)).ToList();
                return true;
            }
            catch (Exception ex)
            {
                FormationFailures.Report("Placing raised creatures in the formation", ex);
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

    public static class FormationActivity
    {
        private static int s_FollowersPlacedByMod;
        private static int s_FollowersLeftToGame;

        public static void Count(int placedByMod, int leftToGame)
        {
            s_FollowersPlacedByMod += placedByMod;
            s_FollowersLeftToGame += leftToGame;
        }

        public static string DescribeAndReset()
        {
            var description = $"{s_FollowersPlacedByMod} placement(s) by the mod, {s_FollowersLeftToGame} left to the game";
            s_FollowersPlacedByMod = 0;
            s_FollowersLeftToGame = 0;
            return description;
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
