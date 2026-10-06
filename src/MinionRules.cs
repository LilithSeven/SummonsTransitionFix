using System;
using System.Collections.Generic;

namespace SummonsTransitionFix
{
    public interface IMinionFacts<TUnit, TBuff> where TUnit : class
    {
        bool Exists(TUnit? unit);
        bool AreSame(TUnit first, TUnit second);
        bool IsDead(TUnit unit);
        bool IsPlayerFaction(TUnit unit);
        bool IsMainCharacter(TUnit unit);
        bool IsInParty(TUnit unit);
        bool IsInPartyAndPets(TUnit unit);
        TUnit? GetPetMaster(TUnit unit);
        string? GetCompanionState(TUnit unit);
        bool HasSummonedPart(TUnit unit);
        TUnit? GetSummoner(TUnit unit);
        bool HasActiveSummonBuff(TUnit unit);
        IEnumerable<TBuff>? GetBuffs(TUnit unit);
        string? GetBuffName(TBuff buff);
        TUnit? GetBuffCaster(TBuff buff);
        TUnit? GetMainCharacter();
    }

    public sealed class MinionRules<TUnit, TBuff> where TUnit : class
    {
        public const string NoCompanionState = "None";

        public static readonly string[] ServitudeBuffKeywords =
        {
            "Repurpose",
            "FlayForPurpose",
            "DoomOfServitude",
        };

        private readonly IMinionFacts<TUnit, TBuff> facts;

        public MinionRules(IMinionFacts<TUnit, TBuff> facts)
        {
            this.facts = facts;
        }

        public bool IsPlayerMinion(TUnit? unit)
        {
            if (!facts.Exists(unit)) return false;
            if (facts.IsDead(unit!)) return false;
            if (!facts.IsPlayerFaction(unit!)) return false;
            if (IsPartyMemberOrPet(unit)) return false;

            if (facts.HasSummonedPart(unit!) && facts.HasActiveSummonBuff(unit!))
            {
                var summoner = facts.GetSummoner(unit!);
                if (facts.Exists(summoner) && IsPartyMemberOrPet(summoner))
                {
                    return true;
                }
            }

            return facts.Exists(GetServitudeMaster(unit!));
        }

        public TUnit? GetMinionMaster(TUnit? unit)
        {
            if (!facts.Exists(unit)) return null;

            if (facts.HasSummonedPart(unit!))
            {
                var summoner = facts.GetSummoner(unit!);
                if (facts.Exists(summoner) && IsPartyMemberOrPet(summoner))
                {
                    return summoner;
                }
            }

            return GetServitudeMaster(unit!);
        }

        public bool IsPartyMemberOrPet(TUnit? unit)
        {
            if (!facts.Exists(unit)) return false;
            if (facts.IsMainCharacter(unit!)) return true;
            if (facts.IsInParty(unit!)) return true;
            if (facts.IsInPartyAndPets(unit!)) return true;

            var petMaster = facts.GetPetMaster(unit!);
            if (facts.Exists(petMaster) && !facts.AreSame(petMaster!, unit!))
            {
                return IsPartyMemberOrPet(petMaster);
            }

            var companionState = facts.GetCompanionState(unit!);
            if (companionState != null && companionState != NoCompanionState)
            {
                return true;
            }

            return false;
        }

        public static bool IsServitudeBuffName(string? name)
        {
            if (name == null || name.Length == 0) return false;

            foreach (var keyword in ServitudeBuffKeywords)
            {
                if (name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private TUnit? GetServitudeMaster(TUnit unit)
        {
            var buffs = facts.GetBuffs(unit);
            if (buffs == null) return null;

            var hasServitudeBuff = false;

            foreach (var buff in buffs)
            {
                if (!IsServitudeBuffName(facts.GetBuffName(buff))) continue;

                hasServitudeBuff = true;

                var caster = facts.GetBuffCaster(buff);
                if (facts.Exists(caster) && IsPartyMemberOrPet(caster))
                {
                    return caster;
                }
            }

            if (hasServitudeBuff)
            {
                var mainCharacter = facts.GetMainCharacter();
                if (facts.Exists(mainCharacter) && IsPartyMemberOrPet(mainCharacter))
                {
                    return mainCharacter;
                }
            }

            return null;
        }
    }
}
