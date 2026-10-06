using System;
using System.Linq;
using Kingmaker;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Parts;

namespace SummonsTransitionFix
{
    public static class Minions
    {
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

        private static readonly string[] ServitudeBuffKeywords =
        {
            "Repurpose",
            "FlayForPurpose",
            "DoomOfServitude",
        };

        private static UnitEntityData? GetRepurposeCaster(UnitEntityData unit)
        {
            if (unit?.Buffs == null) return null;

            var hasServitudeBuff = false;

            foreach (var buff in unit.Buffs)
            {
                var name = buff.Blueprint?.name;
                if (string.IsNullOrEmpty(name)) continue;

                var isServitudeBuff = false;
                foreach (var keyword in ServitudeBuffKeywords)
                {
                    if (name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isServitudeBuff = true;
                        break;
                    }
                }
                if (!isServitudeBuff) continue;

                hasServitudeBuff = true;

                var caster = buff.Context?.MaybeCaster;
                if (caster != null && IsPartyMemberOrPet(caster))
                {
                    return caster;
                }
            }

            if (hasServitudeBuff)
            {
                var mainCharacter = Game.Instance?.Player?.MainCharacter.Value;
                if (mainCharacter != null && IsPartyMemberOrPet(mainCharacter))
                {
                    return mainCharacter;
                }
            }

            return null;
        }
    }
}
