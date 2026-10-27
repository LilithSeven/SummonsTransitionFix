using System;
using Kingmaker.Blueprints;
using Kingmaker.Designers.EventConditionActionSystem.ContextData;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Mechanics;
using UnityEngine;

namespace SummonsTransitionFix
{
    public static class LoyaltyGuard
    {
        private static readonly BlueprintGuid ServitudeBuffGuid = BlueprintGuid.Parse(LoyaltyRules.RepurposeBuffUndeadId);
        private static readonly BlueprintGuid EndOfServitudeBuffGuid = BlueprintGuid.Parse(LoyaltyRules.RepurposeEndBuffId);
        private static readonly BlueprintGuid ReleaseAbilityGuid = BlueprintGuid.Parse(LoyaltyRules.RepurposeForcedEndId);

        private static Buff? s_LastBlockedBuff;
        private static int s_LastBlockedFrame = -1;
        private static int s_BlocksOfLastBlockedBuff;
        private static bool s_FailureReported;

        public static BondBuffKind GetKind(Buff? buff)
        {
            var blueprint = buff?.Blueprint;
            if (blueprint == null) return BondBuffKind.None;
            if (blueprint.AssetGuid == ServitudeBuffGuid) return BondBuffKind.Servitude;
            if (blueprint.AssetGuid == EndOfServitudeBuffGuid) return BondBuffKind.EndOfServitude;
            return BondBuffKind.None;
        }

        public static bool AllowsRemoval(Buff buff)
        {
            var kind = GetKind(buff);
            if (kind == BondBuffKind.None) return true;

            try
            {
                bool bondIsProtected = IsProtected(buff);
                bool isEndOfServitude = kind == BondBuffKind.EndOfServitude;
                bool allowed = LoyaltyRules.AllowsRemoval(
                    kind,
                    bondIsProtected,
                    bondIsProtected && isEndOfServitude && IsReleaseAbilityRunning(),
                    bondIsProtected && isEndOfServitude && IsServitudeBeingActivated(buff),
                    bondIsProtected && !isEndOfServitude && IsInsideEndOfServitudeActions(buff));
                if (allowed || StopsBlocking(buff)) return true;

                if (!buff.IsPermanent)
                {
                    buff.MakePermanent();
                }

                Report(buff, $"blocked the removal of {buff.Blueprint.name}");
                return false;
            }
            catch (Exception ex)
            {
                ReportFailure(ex);
                return true;
            }
        }

        public static bool RunsEndOfServitudeActions()
        {
            if (!(ContextData<FactData>.Current?.Fact is Buff buff) || GetKind(buff) != BondBuffKind.EndOfServitude) return true;

            try
            {
                bool bondIsProtected = IsProtected(buff);
                bool runs = LoyaltyRules.RunsEndOfServitudeActions(
                    bondIsProtected,
                    bondIsProtected && !IsStillListed(buff),
                    bondIsProtected && IsServitudeBeingActivated(buff));
                if (!runs)
                {
                    Report(buff, "ignored the end of servitude while its hidden effect was switched off or replaced");
                }

                return runs;
            }
            catch (Exception ex)
            {
                ReportFailure(ex);
                return true;
            }
        }

        public static bool KeepsUnsuppressed(Buff buff)
        {
            if (GetKind(buff) == BondBuffKind.None) return false;

            try
            {
                if (!IsProtected(buff)) return false;

                Report(buff, $"kept {buff.Blueprint.name} from being suppressed");
                return true;
            }
            catch (Exception ex)
            {
                ReportFailure(ex);
                return false;
            }
        }

        public static bool IgnoresDispel(Buff buff)
        {
            if (GetKind(buff) == BondBuffKind.None) return false;

            try
            {
                if (!IsProtected(buff)) return false;

                Report(buff, $"kept {buff.Blueprint.name} out of a dispel");
                return true;
            }
            catch (Exception ex)
            {
                ReportFailure(ex);
                return false;
            }
        }

        private static bool IsProtected(Buff buff)
        {
            if (!Main.KeepsRaisedCreaturesLoyal) return false;

            var servant = buff.Owner?.Unit;
            if (servant == null || servant.IsDisposingNow || servant.IsDisposed) return false;

            return Minions.IsBondProtected(servant, buff.Context?.MaybeCaster);
        }

        private static bool IsReleaseAbilityRunning()
        {
            var ability = ContextData<MechanicsContext.Data>.Current?.Context?.SourceAbility;
            return ability != null && ability.AssetGuid == ReleaseAbilityGuid;
        }

        private static bool IsServitudeBeingActivated(Buff buff)
        {
            var buffs = buff.Owner?.Buffs;
            if (buffs == null) return false;

            foreach (var other in buffs)
            {
                if (GetKind(other) == BondBuffKind.Servitude && other.Activating) return true;
            }

            return false;
        }

        private static bool IsInsideEndOfServitudeActions(Buff buff)
        {
            var running = ContextData<Buff.Data>.Current?.Buff;
            return running != null && GetKind(running) == BondBuffKind.EndOfServitude && ReferenceEquals(running.Owner, buff.Owner);
        }

        private static bool IsStillListed(Buff buff)
        {
            var manager = buff.Manager;
            return manager != null && manager.List.Contains(buff);
        }

        private static bool StopsBlocking(Buff buff)
        {
            int frame = Time.frameCount;
            if (!ReferenceEquals(buff, s_LastBlockedBuff) || frame != s_LastBlockedFrame)
            {
                s_LastBlockedBuff = buff;
                s_LastBlockedFrame = frame;
                s_BlocksOfLastBlockedBuff = 0;
            }

            s_BlocksOfLastBlockedBuff++;
            if (!LoyaltyRules.StopsBlocking(s_BlocksOfLastBlockedBuff)) return false;

            if (s_BlocksOfLastBlockedBuff == LoyaltyRules.MaxBlocksOfOneBuffPerFrame + 1)
            {
                Main.Logger?.Error($"[SummonsTransitionFix] The game kept trying to remove {buff.Blueprint?.name} from {buff.Owner?.Unit?.CharacterName}: the removal is let through to avoid a freeze.");
            }

            return true;
        }

        private static void Report(Buff buff, string action)
        {
            var servant = buff.Owner?.Unit;
            var caster = buff.Context?.MaybeCaster;
            Main.Logger?.Log($"[SummonsTransitionFix] Kept {servant?.CharacterName ?? "?"} loyal to {caster?.CharacterName ?? "?"}: {action}.");
        }

        private static void ReportFailure(Exception ex)
        {
            if (s_FailureReported) return;

            s_FailureReported = true;
            Main.Logger?.Error($"[SummonsTransitionFix] The loyalty protection failed, the game handles raised creatures its own way: {ex}");
        }
    }
}
