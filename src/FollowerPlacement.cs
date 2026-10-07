#nullable disable
using System.Collections.Generic;
using Kingmaker;
using Kingmaker.Blueprints.Root;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Formations;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.Parts;
using UnityEngine;

namespace SummonsTransitionFix
{
    internal static class FollowerPlacement
    {
        private const float FrontLineThreshold = 0.10f;
        private const int MaxPerRow = 4;
        private const float SubRowGap = 2.0f;
        private static readonly OffsetFormation Formation = new OffsetFormation();
        private static readonly List<UnitEntityData> OrderedFollowers = new List<UnitEntityData>(20);
        private static readonly List<UnitCombatProfile> Profiles = new List<UnitCombatProfile>(20);
        private static readonly List<UnitCombatProfile> FrontBuffer = new List<UnitCombatProfile>(20);
        private static readonly List<UnitCombatProfile> BackBuffer = new List<UnitCombatProfile>(20);

        internal static void Prepare(UnitPartFollowedByUnits leader, IList<UnitEntityData> followers, Vector3 position, Dictionary<UnitEntityData, FollowerActionType> desiredActions)
        {
            if (followers == null || followers.Count == 0 || leader?.Owner == null)
            {
                return;
            }

            BuildProfiles(followers);
            if (Profiles.Count == 0)
            {
                return;
            }

            UnitCombatProfile.ComputeDurability(Profiles);
            SplitLines();
            float leaderOrientation = GetOrientation(leader.Owner);
            Quaternion rotation = Quaternion.Euler(0f, leaderOrientation, 0f);
            Vector2[] offsets = BuildOffsets();
            Formation.SetOffsets(offsets, OrderedFollowers.Count);
            float maxY = float.MinValue;
            for (int i = 0; i < OrderedFollowers.Count; i++)
            {
                if (offsets[i].y > maxY)
                {
                    maxY = offsets[i].y;
                }
            }

            float anchorX = OrderedFollowers.Count == 1 ? offsets[0].x : 0f;
            Vector3 anchor = position + rotation * new Vector3(anchorX, 0f, maxY);
            PartyFormationHelper.FillFormationPositions(anchor, FormationAnchor.Front, rotation * Vector3.forward, OrderedFollowers, OrderedFollowers, Formation, 1f, true);
            float spread = GetLookAngleSpread() * 0.5f;
            for (int i = 0; i < OrderedFollowers.Count; i++)
            {
                UnitEntityData follower = OrderedFollowers[i];
                FollowerActionType actionType;
                if (!desiredActions.TryGetValue(follower, out actionType))
                {
                    continue;
                }

                leader.FollowerDesiredActions[follower] = new FollowerAction(PartyFormationHelper.ResultPositions[i], leaderOrientation + Random.Range(-spread, spread), actionType);
            }
        }

        private static void BuildProfiles(IList<UnitEntityData> followers)
        {
            Profiles.Clear();
            for (int i = 0; i < followers.Count; i++)
            {
                UnitEntityData follower = followers[i];
                if (follower != null)
                {
                    Profiles.Add(UnitCombatProfile.Build(follower));
                }
            }
        }

        private static void SplitLines()
        {
            FrontBuffer.Clear();
            BackBuffer.Clear();
            foreach (UnitCombatProfile p in Profiles)
            {
                if (p.MeleeAffinity >= FrontLineThreshold && !p.Incapacitated)
                {
                    p.Line = FormationLine.Front;
                    FrontBuffer.Add(p);
                }
                else
                {
                    p.Line = FormationLine.Ranged;
                    BackBuffer.Add(p);
                }
            }

            SortByStrength(FrontBuffer);
            SortByStrength(BackBuffer);
        }

        private static void SortByStrength(List<UnitCombatProfile> line)
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

        private static Vector2[] BuildOffsets()
        {
            Settings settings = Main.ModSettings;
            OrderedFollowers.Clear();
            List<Vector2> offsets = new List<Vector2>(Profiles.Count);
            PlaceFrontLine(FrontBuffer, offsets, settings);
            PlaceBackLine(BackBuffer, offsets, settings);
            return offsets.ToArray();
        }

        private static void PlaceFrontLine(List<UnitCombatProfile> line, List<Vector2> offsets, Settings settings)
        {
            float y = settings.RaisedFrontPush;
            float halfCorridor = settings.RaisedCorridorWidth * 0.5f;
            for (int i = 0; i < line.Count; i++)
            {
                int row = i / MaxPerRow;
                int indexInRow = i % MaxPerRow;
                int rank = indexInRow / 2;
                int side = (indexInRow % 2 == 0) ? -1 : 1;
                float x = side * (halfCorridor + settings.RaisedLateralSpacing * rank);
                OrderedFollowers.Add(line[i].Unit);
                offsets.Add(new Vector2(x, y - row * SubRowGap));
            }
        }

        private static void PlaceBackLine(List<UnitCombatProfile> line, List<Vector2> offsets, Settings settings)
        {
            float baseY = -settings.RaisedLineGap;
            int index = 0;
            while (index < line.Count)
            {
                int count = Mathf.Min(MaxPerRow, line.Count - index);
                float startX = -settings.RaisedLateralSpacing * 0.5f * (count - 1);
                float y = baseY - (index / MaxPerRow) * SubRowGap;
                for (int i = 0; i < count; i++)
                {
                    OrderedFollowers.Add(line[index + i].Unit);
                    offsets.Add(new Vector2(startX + i * settings.RaisedLateralSpacing, y));
                }

                index += count;
            }
        }

        private static float GetOrientation(UnitEntityData unit)
        {
            UnitMoveTo move = unit.Commands?.Move;
            float? orientation = move?.Orientation;
            return orientation ?? unit.Orientation;
        }

        internal static Vector3 GetUnitDestination(UnitEntityData unit)
        {
            UnitCommand standard = unit.Commands?.Standard;
            if (standard != null)
            {
                return standard.ApproachPoint;
            }

            UnitMoveTo move = unit.Commands?.Move;
            if (move == null)
            {
                return unit.Position;
            }

            return move.ApproachPoint;
        }

        private static float GetLookAngleSpread()
        {
            FollowersFormation formation = Game.Instance?.BlueprintRoot?.Formations?.FollowersFormation;
            return formation?.LookAngleRandomSpread ?? 0f;
        }
    }

    internal sealed class OffsetFormation : IImmutablePartyFormation
    {
        private Vector2[] m_Offsets = new Vector2[0];
        private int m_Count;

        public float Length => 1f;

        public UnitEntityData Tank => null;

        internal void SetOffsets(Vector2[] offsets, int count)
        {
            m_Offsets = offsets;
            m_Count = count;
        }

        public Vector2 GetOffset(int index, UnitEntityData unit)
        {
            if (index < 0 || index >= m_Count || index >= m_Offsets.Length)
            {
                return Vector2.zero;
            }

            return m_Offsets[index];
        }
    }
}
