using HarmonyLib;
using Kingmaker.EntitySystem;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.UnitLogic.Parts;

namespace SummonsTransitionFix
{
    [HarmonyPatch(typeof(EntityFactsManager), nameof(EntityFactsManager.Remove))]
    public static class EntityFactsManager_Remove_Patch
    {
        public static bool Prefix(EntityFact fact)
        {
            return !(fact is Buff buff) || LoyaltyGuard.AllowsRemoval(buff);
        }
    }

    [HarmonyPatch(typeof(AddFactContextActions), "OnDeactivate")]
    public static class AddFactContextActions_OnDeactivate_Patch
    {
        public static bool Prefix()
        {
            return LoyaltyGuard.RunsEndOfServitudeActions();
        }
    }

    [HarmonyPatch(typeof(UnitPartBuffSuppress), nameof(UnitPartBuffSuppress.IsSuppressed))]
    public static class UnitPartBuffSuppress_IsSuppressed_Patch
    {
        public static void Postfix(Buff buff, ref bool __result)
        {
            if (__result && LoyaltyGuard.KeepsUnsuppressed(buff))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(ContextActionDispelMagic), "TryDispelBuff")]
    public static class ContextActionDispelMagic_TryDispelBuff_Patch
    {
        public static bool Prefix(Buff buff, ref bool __result)
        {
            if (!LoyaltyGuard.IgnoresDispel(buff)) return true;

            __result = false;
            return false;
        }
    }
}
