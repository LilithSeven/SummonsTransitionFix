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
        bool IsInAbilitySummonPool(TUnit unit);
        bool IsBeingDestroyed(TUnit unit);
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

    public static class AbilitySummonPools
    {
        public static readonly string[] Ids =
        {
            "3218615b6f823ff4886e68fea9f5e3ca",
            "dff3a1584dac107459064c2e48344df3",
            "3179d260b5ccdc74ba6a33d12b5eb888",
            "b833c7342783c3a4eaf116b830eb0129",
            "cfb65b88ebacea4478f7a36a2891f944",
            "3c177298b5d47f14b8f372bf4d76f7e7",
            "0dd5520857fb4481be3a1c51799c258f",
            "490248a826bbf904e852f5e3afa6d138",
            "7c2be513c3b883141ae5ed0e121a18e5",
            "87447d0b37f75be4f804aef5728384ac",
            "134107951d5e4ce79efd088a43030387",
            "6209440d54444594a7d92bebff1f7239",
        };
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

            if (facts.HasSummonedPart(unit!) && (facts.HasActiveSummonBuff(unit!) || OutlivedItsAbilityRecast(unit!)))
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

        public bool IsBondProtected(TUnit? servant, TUnit? caster)
        {
            if (!facts.Exists(servant)) return false;
            if (facts.IsDead(servant!)) return false;
            if (IsPartyMemberOrPet(servant)) return false;

            return facts.Exists(caster) && IsPartyMemberOrPet(caster);
        }

        public List<TUnit> SelectPlayerMinions(IEnumerable<TUnit?>? followers)
        {
            var minions = new List<TUnit>();
            if (followers == null) return minions;

            foreach (var follower in followers)
            {
                if (IsPlayerMinion(follower))
                {
                    minions.Add(follower!);
                }
            }

            return minions;
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

        private bool OutlivedItsAbilityRecast(TUnit unit)
        {
            return facts.IsInAbilitySummonPool(unit) && !facts.IsBeingDestroyed(unit);
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
