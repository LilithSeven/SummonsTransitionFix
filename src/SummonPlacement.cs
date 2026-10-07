#nullable disable
using System.Collections.Generic;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.View;
using Pathfinding;
using UnityEngine;

namespace SummonsTransitionFix
{
    internal static class SummonPlacement
    {
        private const float FrontLineThreshold = 0.10f;
        private const int MaxPerRow = 4;
        private const float RowGap = 2.0f;
        private const double MissingRebuildCooldownSeconds = 0.25;
        private const double RosterValidationIntervalSeconds = 1.0;
        private const double LayoutCheckIntervalSeconds = 0.50;
        private const float LayoutEpsilon = 0.20f;
        private const float RetargetCooldownSeconds = 1.25f;
        private const float RetargetJitterMaximum = 0.35f;
        private const float ActivePathRetargetMultiplier = 2.5f;
        private const int MaxCachedSummoners = 32;
        private const int MaxCachedMovers = 128;
        private const float MaxEnvelopeDepth = 30f;

        private struct PartyEnvelope
        {
            internal float FrontDepth;
            internal float BackDepth;
            internal float LateralCenter;
            internal bool Valid;
        }

        private sealed class GroupPlan
        {
            internal double LastRosterRebuildTime = double.NegativeInfinity;
            internal double NextRosterValidationTime = double.NegativeInfinity;
            internal double NextLayoutCheckTime = double.NegativeInfinity;
            internal bool LayoutValid;
            internal PartyEnvelope LastEnvelope;
            internal float LastFrontMargin;
            internal float LastBackMargin;
            internal float LastLateralSpacing;
            internal float LastCorridorWidth;
            internal readonly Dictionary<string, Vector2> Offsets = new Dictionary<string, Vector2>();
            internal readonly List<UnitEntityData> FrontUnits = new List<UnitEntityData>();
            internal readonly List<UnitEntityData> BackUnits = new List<UnitEntityData>();
        }

        private sealed class MoveState
        {
            internal float NextRequestTime;
            internal float Jitter;
        }

        private static readonly Dictionary<string, GroupPlan> Plans = new Dictionary<string, GroupPlan>();
        private static readonly Dictionary<string, MoveState> MoveStates = new Dictionary<string, MoveState>();
        private static readonly List<UnitCombatProfile> Scratch = new List<UnitCombatProfile>();
        private static readonly List<UnitCombatProfile> FrontBuffer = new List<UnitCombatProfile>();
        private static readonly List<UnitCombatProfile> BackBuffer = new List<UnitCombatProfile>();

        internal static void Tick(UnitEntityData unit, UnitPartSummonedMonster part)
        {
            UnitEntityData summoner = part.Summoner;
            if (unit.Descriptor == null || unit.IsInCombat || !unit.Descriptor.State.CanMove || summoner == null || summoner.Group != unit.Group)
            {
                UnitEntityView view = unit.View;
                if (view != null && view.AgentASP != null)
                {
                    view.AgentASP.ForceRoaming = false;
                }

                if (part.MoveTo != null)
                {
                    part.MoveTo.Interrupt(true);
                    part.MoveTo = null;
                }

                MoveStates.Remove(unit.UniqueId);
                return;
            }

            if (part.MoveTo != null && (part.MoveTo.IsFinished || part.MoveTo.Result == UnitCommand.ResultType.Interrupt))
            {
                part.MoveTo = null;
            }

            Vector3 masterDestination = GetMasterDestination(summoner);
            float orientation = GetOrientation(summoner);
            Vector2 offset = GetOffset(unit, summoner, masterDestination, orientation);
            Quaternion rotation = Quaternion.Euler(0f, orientation, 0f);
            Vector3 desired = masterDestination + rotation * new Vector3(offset.x, 0f, offset.y);
            MoveState movement = GetMoveState(unit);
            float now = Time.unscaledTime;
            float repathDistance = Mathf.Max(1f, Main.ModSettings.SummonRepathDistance);
            float repathDistanceSquared = repathDistance * repathDistance;
            if (part.MoveTo == null)
            {
                if ((desired - unit.Position).sqrMagnitude > repathDistanceSquared && now >= movement.NextRequestTime)
                {
                    StartMove(unit, summoner, part, desired, orientation, movement, now, false);
                }
            }
            else
            {
                float activeThreshold = repathDistance * ActivePathRetargetMultiplier;
                float activeThresholdSquared = activeThreshold * activeThreshold;
                Vector3 currentRequestedTarget = part.MoveTo.Target;
                bool targetMovedFarEnough = (desired - currentRequestedTarget).sqrMagnitude > activeThresholdSquared;
                if (targetMovedFarEnough && now >= movement.NextRequestTime)
                {
                    StartMove(unit, summoner, part, desired, orientation, movement, now, true);
                }
            }

            if (unit.View != null && unit.View.AgentASP != null)
            {
                unit.View.AgentASP.ForceRoaming = true;
            }
        }

        private static void StartMove(UnitEntityData unit, UnitEntityData summoner, UnitPartSummonedMonster part, Vector3 desired, float orientation, MoveState movement, float now, bool replaceExisting)
        {
            if (replaceExisting && part.MoveTo != null)
            {
                part.MoveTo.Interrupt(true);
                part.MoveTo = null;
            }

            UnitMoveTo move = new UnitMoveTo(desired)
            {
                Orientation = orientation
            };
            ApplyMasterSpeed(unit, summoner, move);
            part.MoveTo = move;
            unit.Commands.Run(move);
            movement.NextRequestTime = now + RetargetCooldownSeconds + movement.Jitter;
        }

        private static MoveState GetMoveState(UnitEntityData unit)
        {
            MoveState state;
            if (MoveStates.TryGetValue(unit.UniqueId, out state))
            {
                return state;
            }

            if (MoveStates.Count >= MaxCachedMovers)
            {
                MoveStates.Clear();
            }

            state = new MoveState
            {
                Jitter = GetStableJitter(unit.UniqueId)
            };
            MoveStates[unit.UniqueId] = state;
            return state;
        }

        private static float GetStableJitter(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return 0f;
            }

            unchecked
            {
                int hash = 17;
                for (int i = 0; i < id.Length; i++)
                {
                    hash = hash * 31 + id[i];
                }

                if (hash == int.MinValue)
                {
                    hash = 0;
                }
                else if (hash < 0)
                {
                    hash = -hash;
                }

                return (hash % 1000) / 1000f * RetargetJitterMaximum;
            }
        }

        internal static void ApplyMasterSpeed(UnitEntityData unit, UnitEntityData leader, UnitMoveTo move)
        {
            if (!Main.ModSettings.EnableMinionSpeed || move == null || leader == null)
            {
                return;
            }

            float reference = ReferenceSpeed(leader);
            if (reference <= 0.01f || unit.ModifiedSpeedMps >= reference)
            {
                return;
            }

            move.OverrideSpeed = reference * Mathf.Max(1f, Main.ModSettings.SummonCatchUpFactor);
        }

        private static float ReferenceSpeed(UnitEntityData leader)
        {
            return Mathf.Max(leader.ModifiedSpeedMps, leader.CurrentSpeedMps);
        }

        private static Vector2 GetOffset(UnitEntityData unit, UnitEntityData summoner, Vector3 masterDestination, float orientation)
        {
            GroupPlan plan = GetPlan(summoner, unit, masterDestination, orientation);
            Vector2 offset;
            return plan.Offsets.TryGetValue(unit.UniqueId, out offset) ? offset : new Vector2(0f, -Main.ModSettings.SummonBackMargin);
        }

        private static GroupPlan GetPlan(UnitEntityData summoner, UnitEntityData requester, Vector3 masterDestination, float orientation)
        {
            double now = Game.Instance.TimeController.GameTime.TotalSeconds;
            GroupPlan plan;
            if (!Plans.TryGetValue(summoner.UniqueId, out plan))
            {
                if (Plans.Count >= MaxCachedSummoners)
                {
                    Plans.Clear();
                }

                plan = new GroupPlan();
                Plans[summoner.UniqueId] = plan;
                RebuildRosterAndLayout(summoner, plan, masterDestination, orientation, now);
                return plan;
            }

            if (now < plan.LastRosterRebuildTime)
            {
                RebuildRosterAndLayout(summoner, plan, masterDestination, orientation, now);
                return plan;
            }

            bool requesterMissing = !plan.Offsets.ContainsKey(requester.UniqueId);
            bool rosterInvalid = false;
            if (now >= plan.NextRosterValidationTime)
            {
                plan.NextRosterValidationTime = now + RosterValidationIntervalSeconds;
                rosterInvalid = HasInvalidCachedUnit(plan, summoner);
            }

            if ((requesterMissing || rosterInvalid) && now - plan.LastRosterRebuildTime >= MissingRebuildCooldownSeconds)
            {
                RebuildRosterAndLayout(summoner, plan, masterDestination, orientation, now);
                return plan;
            }

            if (now >= plan.NextLayoutCheckTime)
            {
                plan.NextLayoutCheckTime = now + LayoutCheckIntervalSeconds;
                RefreshLayoutIfNeeded(plan, summoner, masterDestination, orientation);
            }

            return plan;
        }

        private static bool HasInvalidCachedUnit(GroupPlan plan, UnitEntityData summoner)
        {
            for (int i = 0; i < plan.FrontUnits.Count; i++)
            {
                if (!IsStillLinkedSummon(plan.FrontUnits[i], summoner))
                {
                    return true;
                }
            }

            for (int i = 0; i < plan.BackUnits.Count; i++)
            {
                if (!IsStillLinkedSummon(plan.BackUnits[i], summoner))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsStillLinkedSummon(UnitEntityData unit, UnitEntityData summoner)
        {
            if (unit == null || unit.Descriptor == null || unit.Descriptor.State.IsDead)
            {
                return false;
            }

            UnitPartSummonedMonster part = unit.Get<UnitPartSummonedMonster>();
            return part != null && part.IsLinkedToSummoner && part.Summoner == summoner;
        }

        private static PartyEnvelope MeasureParty(UnitEntityData summoner, Vector3 masterDestination, float orientation)
        {
            PartyEnvelope envelope = new PartyEnvelope
            {
                FrontDepth = float.MinValue,
                BackDepth = float.MaxValue
            };
            List<UnitEntityData> party = Game.Instance?.Player?.PartyAndPets;
            if (party == null)
            {
                return envelope;
            }

            Quaternion rotation = Quaternion.Euler(0f, orientation, 0f);
            Vector3 forward = rotation * Vector3.forward;
            Vector3 right = rotation * Vector3.right;
            float lateralSum = 0f;
            int counted = 0;
            foreach (UnitEntityData member in party)
            {
                if (member == null || member.Descriptor == null || !member.Descriptor.State.IsConscious)
                {
                    continue;
                }

                Vector3 destination = member.Commands?.Move?.ApproachPoint ?? member.Position;
                Vector3 delta = destination - masterDestination;
                float depth = Vector3.Dot(delta, forward);
                if (depth < -MaxEnvelopeDepth || depth > MaxEnvelopeDepth)
                {
                    continue;
                }

                envelope.Valid = true;
                counted++;
                lateralSum += Vector3.Dot(delta, right);
                if (depth > envelope.FrontDepth)
                {
                    envelope.FrontDepth = depth;
                }

                if (depth < envelope.BackDepth)
                {
                    envelope.BackDepth = depth;
                }
            }

            if (!envelope.Valid)
            {
                envelope.FrontDepth = 0f;
                envelope.BackDepth = 0f;
                envelope.LateralCenter = 0f;
                return envelope;
            }

            envelope.LateralCenter = counted > 0 ? lateralSum / counted : 0f;
            return envelope;
        }

        private static void RebuildRosterAndLayout(UnitEntityData summoner, GroupPlan plan, Vector3 masterDestination, float orientation, double now)
        {
            plan.Offsets.Clear();
            plan.FrontUnits.Clear();
            plan.BackUnits.Clear();
            Scratch.Clear();
            FrontBuffer.Clear();
            BackBuffer.Clear();
            foreach (UnitEntityData other in Game.Instance.State.Units.All)
            {
                UnitPartSummonedMonster otherPart = other?.Get<UnitPartSummonedMonster>();
                if (otherPart == null || !otherPart.IsLinkedToSummoner || otherPart.Summoner != summoner)
                {
                    continue;
                }

                if (other.Descriptor == null || other.Descriptor.State.IsDead)
                {
                    continue;
                }

                Scratch.Add(UnitCombatProfile.Build(other));
            }

            if (Scratch.Count > 0)
            {
                UnitCombatProfile.ComputeDurability(Scratch);
                foreach (UnitCombatProfile p in Scratch)
                {
                    if (p.MeleeAffinity >= FrontLineThreshold)
                    {
                        FrontBuffer.Add(p);
                    }
                    else
                    {
                        BackBuffer.Add(p);
                    }
                }

                SortStable(FrontBuffer);
                SortStable(BackBuffer);
                for (int i = 0; i < FrontBuffer.Count; i++)
                {
                    plan.FrontUnits.Add(FrontBuffer[i].Unit);
                }

                for (int i = 0; i < BackBuffer.Count; i++)
                {
                    plan.BackUnits.Add(BackBuffer[i].Unit);
                }
            }

            plan.LastRosterRebuildTime = now;
            plan.NextRosterValidationTime = now + RosterValidationIntervalSeconds;
            plan.NextLayoutCheckTime = now + LayoutCheckIntervalSeconds;
            PartyEnvelope envelope = MeasureParty(summoner, masterDestination, orientation);
            ApplyLayout(plan, envelope);
        }

        private static void RefreshLayoutIfNeeded(GroupPlan plan, UnitEntityData summoner, Vector3 masterDestination, float orientation)
        {
            PartyEnvelope envelope = MeasureParty(summoner, masterDestination, orientation);
            Settings settings = Main.ModSettings;
            bool changed = !plan.LayoutValid
                || envelope.Valid != plan.LastEnvelope.Valid
                || Mathf.Abs(envelope.FrontDepth - plan.LastEnvelope.FrontDepth) > LayoutEpsilon
                || Mathf.Abs(envelope.BackDepth - plan.LastEnvelope.BackDepth) > LayoutEpsilon
                || Mathf.Abs(envelope.LateralCenter - plan.LastEnvelope.LateralCenter) > LayoutEpsilon
                || !Mathf.Approximately(settings.SummonFrontMargin, plan.LastFrontMargin)
                || !Mathf.Approximately(settings.SummonBackMargin, plan.LastBackMargin)
                || !Mathf.Approximately(settings.SummonLateralSpacing, plan.LastLateralSpacing)
                || !Mathf.Approximately(settings.SummonCorridorWidth, plan.LastCorridorWidth);
            if (changed)
            {
                ApplyLayout(plan, envelope);
            }
        }

        private static void ApplyLayout(GroupPlan plan, PartyEnvelope envelope)
        {
            plan.Offsets.Clear();
            Settings settings = Main.ModSettings;
            float halfCorridor = settings.SummonCorridorWidth * 0.5f;
            float frontLine = envelope.FrontDepth + settings.SummonFrontMargin;
            float backLine = envelope.BackDepth - settings.SummonBackMargin;
            for (int i = 0; i < plan.FrontUnits.Count; i++)
            {
                int row = i / MaxPerRow;
                int indexInRow = i % MaxPerRow;
                int rank = indexInRow / 2;
                int side = indexInRow % 2 == 0 ? -1 : 1;
                float x = envelope.LateralCenter + side * (halfCorridor + settings.SummonLateralSpacing * rank);
                plan.Offsets[plan.FrontUnits[i].UniqueId] = new Vector2(x, frontLine - row * RowGap);
            }

            for (int i = 0; i < plan.BackUnits.Count; i++)
            {
                int row = i / MaxPerRow;
                int indexInRow = i % MaxPerRow;
                int countInRow = Mathf.Min(MaxPerRow, plan.BackUnits.Count - row * MaxPerRow);
                float startX = -settings.SummonLateralSpacing * 0.5f * (countInRow - 1);
                float x = envelope.LateralCenter + startX + indexInRow * settings.SummonLateralSpacing;
                plan.Offsets[plan.BackUnits[i].UniqueId] = new Vector2(x, backLine - row * RowGap);
            }

            plan.LastEnvelope = envelope;
            plan.LastFrontMargin = settings.SummonFrontMargin;
            plan.LastBackMargin = settings.SummonBackMargin;
            plan.LastLateralSpacing = settings.SummonLateralSpacing;
            plan.LastCorridorWidth = settings.SummonCorridorWidth;
            plan.LayoutValid = true;
        }

        private static void SortStable(List<UnitCombatProfile> line)
        {
            line.Sort((a, b) =>
            {
                int byDurability = b.Durability.CompareTo(a.Durability);
                if (byDurability != 0)
                {
                    return byDurability;
                }

                return string.CompareOrdinal(a.Unit?.UniqueId ?? string.Empty, b.Unit?.UniqueId ?? string.Empty);
            });
        }

        private static Vector3 GetMasterDestination(UnitEntityData summoner)
        {
            UnitEntityView view = summoner.View;
            Path path = view != null && view.AgentASP != null ? view.AgentASP.Path : null;
            if (path != null && path.CompleteState == PathCompleteState.Complete && path.vectorPath != null && path.vectorPath.Count > 0)
            {
                return path.vectorPath[path.vectorPath.Count - 1];
            }

            return summoner.Position;
        }

        private static float GetOrientation(UnitEntityData summoner)
        {
            UnitMoveTo move = summoner.Commands?.Move;
            float? orientation = move?.Orientation;
            return orientation ?? summoner.Orientation;
        }
    }
}
