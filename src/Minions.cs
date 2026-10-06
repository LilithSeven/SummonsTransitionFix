using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Parts;

namespace SummonsTransitionFix
{
    public static class Minions
    {
        private static readonly MinionRules<UnitEntityData, Buff> Rules = new MinionRules<UnitEntityData, Buff>(new GameFacts());

        public static bool IsPlayerMinion(UnitEntityData unit)
        {
            return Rules.IsPlayerMinion(unit);
        }

        public static UnitEntityData? GetMinionMaster(UnitEntityData unit)
        {
            return Rules.GetMinionMaster(unit);
        }

        public static bool IsPartyMemberOrPet(UnitEntityData unit)
        {
            return Rules.IsPartyMemberOrPet(unit);
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

        private sealed class GameFacts : IMinionFacts<UnitEntityData, Buff>
        {
            public bool Exists(UnitEntityData? unit)
            {
                return unit != null;
            }

            public bool AreSame(UnitEntityData first, UnitEntityData second)
            {
                return first == second;
            }

            public bool IsDead(UnitEntityData unit)
            {
                return unit.Descriptor?.State?.IsDead == true;
            }

            public bool IsPlayerFaction(UnitEntityData unit)
            {
                return unit.IsPlayerFaction;
            }

            public bool IsMainCharacter(UnitEntityData unit)
            {
                return unit.IsMainCharacter;
            }

            public bool IsInParty(UnitEntityData unit)
            {
                var player = Game.Instance?.Player;
                return player != null && player.Party.Contains(unit);
            }

            public bool IsInPartyAndPets(UnitEntityData unit)
            {
                var player = Game.Instance?.Player;
                return player != null && player.PartyAndPets.Contains(unit);
            }

            public UnitEntityData? GetPetMaster(UnitEntityData unit)
            {
                return unit.Get<UnitPartPet>()?.Master;
            }

            public string? GetCompanionState(UnitEntityData unit)
            {
                var companion = unit.Get<UnitPartCompanion>();
                return companion != null ? companion.State.ToString() : null;
            }

            public bool HasSummonedPart(UnitEntityData unit)
            {
                return unit.Get<UnitPartSummonedMonster>() != null;
            }

            public UnitEntityData? GetSummoner(UnitEntityData unit)
            {
                return unit.Get<UnitPartSummonedMonster>()?.Summoner;
            }

            public bool HasActiveSummonBuff(UnitEntityData unit)
            {
                var summonedBuff = Game.Instance?.BlueprintRoot?.SystemMechanics?.SummonedUnitBuff;
                return summonedBuff != null && unit.Buffs != null && unit.Buffs.HasFact(summonedBuff);
            }

            public IEnumerable<Buff>? GetBuffs(UnitEntityData unit)
            {
                var buffs = unit.Buffs;
                return buffs != null ? Enumerate(buffs) : null;
            }

            public string? GetBuffName(Buff buff)
            {
                return buff.Blueprint?.name;
            }

            public UnitEntityData? GetBuffCaster(Buff buff)
            {
                return buff.Context?.MaybeCaster;
            }

            public UnitEntityData? GetMainCharacter()
            {
                return Game.Instance?.Player?.MainCharacter.Value;
            }

            private static IEnumerable<Buff> Enumerate(BuffCollection buffs)
            {
                foreach (var buff in buffs)
                {
                    yield return buff;
                }
            }
        }
    }
}
