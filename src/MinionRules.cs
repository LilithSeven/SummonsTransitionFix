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

    public enum MinionVerdict
    {
        Missing,
        Dead,
        NotPlayerFaction,
        PartyMemberOrPet,
        NoLinkToParty,
        SummonedByParty,
        BoundByServitude,
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

        public static readonly string[] BuffNamesThatNeverMarkAMinion =
        {
            "NPC_Immortality_RepurposeBuff",
            "RepurposeCasterBuff",
        };

        private readonly IMinionFacts<TUnit, TBuff> facts;

        public MinionRules(IMinionFacts<TUnit, TBuff> facts)
        {
            this.facts = facts;
        }

        public bool IsPlayerMinion(TUnit? unit)
        {
            var verdict = Classify(unit);
            return verdict == MinionVerdict.SummonedByParty || verdict == MinionVerdict.BoundByServitude;
        }

        public MinionVerdict Classify(TUnit? unit)
        {
            if (!facts.Exists(unit)) return MinionVerdict.Missing;
            if (facts.IsDead(unit!)) return MinionVerdict.Dead;
            if (!facts.IsPlayerFaction(unit!)) return MinionVerdict.NotPlayerFaction;
            if (IsPartyMemberOrPet(unit)) return MinionVerdict.PartyMemberOrPet;

            if (facts.HasSummonedPart(unit!) && facts.HasActiveSummonBuff(unit!))
            {
                var summoner = facts.GetSummoner(unit!);
                if (facts.Exists(summoner) && IsPartyMemberOrPet(summoner))
                {
                    return MinionVerdict.SummonedByParty;
                }
            }

            return facts.Exists(GetServitudeMaster(unit!)) ? MinionVerdict.BoundByServitude : MinionVerdict.NoLinkToParty;
        }

        public bool HasServitudeBuff(TUnit? unit)
        {
            if (!facts.Exists(unit)) return false;

            var buffs = facts.GetBuffs(unit!);
            if (buffs == null) return false;

            foreach (var buff in buffs)
            {
                if (IsServitudeBuffName(facts.GetBuffName(buff))) return true;
            }

            return false;
        }

        public bool IsWorthReporting(TUnit? unit)
        {
            var verdict = Classify(unit);
            if (verdict == MinionVerdict.Missing || verdict == MinionVerdict.PartyMemberOrPet) return false;
            if (verdict == MinionVerdict.Dead || verdict == MinionVerdict.NotPlayerFaction) return HasServitudeBuff(unit);
            return true;
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

            foreach (var excluded in BuffNamesThatNeverMarkAMinion)
            {
                if (string.Equals(name, excluded, StringComparison.OrdinalIgnoreCase)) return false;
            }

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
