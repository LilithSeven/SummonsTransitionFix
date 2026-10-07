#nullable disable
using System.Collections.Generic;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Formations;
using Kingmaker.Settings;
using Kingmaker.UnitLogic;
using UnityEngine;

namespace SummonsTransitionFix
{
    internal static class PartyAutoFormation
    {
        private const float TankMeleeThreshold = 0.12f;
        private const float MeleeLineThreshold = 0.0f;
        private const float ArcaneBackLineShare = 0.5f;
        private const int MaxOffTankAcGap = 6;
        private const int MinUnitsForOffTank = 4;
        private const int MaxPerRow = 4;
        private const float SubRowGap = 2.0f;

        private static readonly Dictionary<string, Vector2> CachedOffsets = new Dictionary<string, Vector2>();
        private static bool CacheValid;
        private static int CachedSignature;
        private static bool CachedInvalidTank;

        internal static void Setup(PartyFormationAuto formation)
        {
            formation.Clear();
            List<UnitEntityData> party = Game.Instance?.Player?.PartyAndPets;
            if (party == null || party.Count == 0)
            {
                return;
            }

            int signature = ComputeSignature(party);
            if (!CacheValid || signature != CachedSignature)
            {
                RebuildCache(party, signature);
            }

            ApplyCachedFormation(formation, party);
        }

        private static int ComputeSignature(List<UnitEntityData> party)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + party.Count;
                if (Game.Instance?.Player != null)
                {
                    hash = hash * 31 + Game.Instance.Player.PartyLevel;
                }

                hash = hash * 31 + SettingsRoot.Difficulty.EnemyDifficulty;
                Settings settings = Main.ModSettings;
                if (settings != null)
                {
                    hash = hash * 31 + settings.PartyFrontGap.GetHashCode();
                    hash = hash * 31 + settings.PartyMidGap.GetHashCode();
                    hash = hash * 31 + settings.PartyBackGap.GetHashCode();
                    hash = hash * 31 + settings.PartyLateralSpacing.GetHashCode();
                }

                for (int i = 0; i < party.Count; i++)
                {
                    UnitEntityData unit = party[i];
                    if (unit == null)
                    {
                        hash = hash * 31;
                        continue;
                    }

                    hash = hash * 31 + (unit.UniqueId != null ? unit.UniqueId.GetHashCode() : 0);
                    hash = hash * 31 + (unit.GetRider() != null ? 1 : 0);
                    if (unit.Descriptor != null)
                    {
                        if (unit.Descriptor.State != null)
                        {
                            hash = hash * 31 + (unit.Descriptor.State.IsConscious ? 1 : 0);
                        }

                        if (unit.Descriptor.Progression != null)
                        {
                            hash = hash * 31 + unit.Descriptor.Progression.CharacterLevel;
                        }
                    }

                    if (unit.Body != null)
                    {
                        var weapon = unit.Body.PrimaryHand?.MaybeWeapon;
                        hash = hash * 31 + (weapon?.Blueprint != null ? weapon.Blueprint.GetHashCode() : 0);
                        var armor = unit.Body.Armor?.MaybeArmor;
                        hash = hash * 31 + (armor?.Blueprint != null ? armor.Blueprint.GetHashCode() : 0);
                        hash = hash * 31 + (unit.Body.SecondaryHand?.MaybeShield != null ? 1 : 0);
                    }
                }

                return hash;
            }
        }

        private static void RebuildCache(List<UnitEntityData> party, int signature)
        {
            CachedOffsets.Clear();
            List<UnitCombatProfile> profiles = BuildProfiles(party);
            CachedInvalidTank = false;
            if (profiles.Count > 0)
            {
                UnitCombatProfile.ComputeDurability(profiles);
                bool invalidTank;
                UnitCombatProfile mainTank = SelectMainTank(profiles, out invalidTank);
                CachedInvalidTank = invalidTank;
                UnitCombatProfile offTank = invalidTank ? null : SelectOffTank(profiles, mainTank);
                AssignLines(profiles, mainTank, offTank);
                BuildCachedLayout(profiles, mainTank, offTank);
            }

            CachedSignature = signature;
            CacheValid = true;
        }

        private static List<UnitCombatProfile> BuildProfiles(List<UnitEntityData> party)
        {
            List<UnitCombatProfile> profiles = new List<UnitCombatProfile>();
            for (int i = 0; i < party.Count; i++)
            {
                UnitEntityData unit = party[i];
                if (unit == null || unit.GetRider() != null)
                {
                    continue;
                }

                profiles.Add(UnitCombatProfile.Build(unit));
            }

            return profiles;
        }

        private static void ApplyCachedFormation(PartyFormationAuto formation, List<UnitEntityData> party)
        {
            formation.InvalidTank = CachedInvalidTank;
            for (int i = 0; i < party.Count; i++)
            {
                UnitEntityData unit = party[i];
                if (unit == null || unit.GetRider() != null)
                {
                    continue;
                }

                Vector2 offset;
                if (CachedOffsets.TryGetValue(unit.UniqueId, out offset))
                {
                    formation.SetOffset(unit, offset);
                }
            }
        }

        private static UnitCombatProfile SelectMainTank(List<UnitCombatProfile> profiles, out bool invalidTank)
        {
            UnitCombatProfile best = null;
            foreach (UnitCombatProfile p in profiles)
            {
                if (p.MeleeAffinity < TankMeleeThreshold || p.Incapacitated)
                {
                    continue;
                }

                if (best == null || IsBetterTank(p, best))
                {
                    best = p;
                }
            }

            if (best != null)
            {
                invalidTank = best.ArmorClass < GetRecommendedTankArmorClass();
                return best;
            }

            foreach (UnitCombatProfile p in profiles)
            {
                if (best == null || IsBetterTank(p, best))
                {
                    best = p;
                }
            }

            invalidTank = true;
            return best;
        }

        private static bool IsBetterTank(UnitCombatProfile candidate, UnitCombatProfile current)
        {
            float a = candidate.Durability;
            float b = current.Durability;
            if (Mathf.Abs(a - b) > 0.0001f)
            {
                return a > b;
            }

            if (Mathf.Abs(candidate.MeleeAffinity - current.MeleeAffinity) > 0.0001f)
            {
                return candidate.MeleeAffinity > current.MeleeAffinity;
            }

            return string.CompareOrdinal(StableOrder(candidate), StableOrder(current)) < 0;
        }

        private static UnitCombatProfile SelectOffTank(List<UnitCombatProfile> profiles, UnitCombatProfile mainTank)
        {
            if (mainTank == null || profiles.Count < MinUnitsForOffTank)
            {
                return null;
            }

            UnitCombatProfile best = null;
            foreach (UnitCombatProfile p in profiles)
            {
                if (p == mainTank || p.MeleeAffinity < TankMeleeThreshold || p.Incapacitated)
                {
                    continue;
                }

                if (best == null || IsBetterTank(p, best))
                {
                    best = p;
                }
            }

            if (best == null || mainTank.ArmorClass - best.ArmorClass > MaxOffTankAcGap)
            {
                return null;
            }

            return best;
        }

        private static void AssignLines(List<UnitCombatProfile> profiles, UnitCombatProfile mainTank, UnitCombatProfile offTank)
        {
            foreach (UnitCombatProfile p in profiles)
            {
                if (p == mainTank || p == offTank)
                {
                    p.Line = FormationLine.Front;
                }
                else if (p.MeleeAffinity >= MeleeLineThreshold && !p.Incapacitated)
                {
                    p.Line = FormationLine.Melee;
                }
                else if (p.RangedPrimary || p.RayAutoAbility || p.ArcaneShare >= ArcaneBackLineShare)
                {
                    p.Line = FormationLine.Ranged;
                }
                else
                {
                    p.Line = FormationLine.Support;
                }
            }
        }

        private static void BuildCachedLayout(List<UnitCombatProfile> profiles, UnitCombatProfile mainTank, UnitCombatProfile offTank)
        {
            Settings settings = Main.ModSettings;
            List<UnitCombatProfile> front = new List<UnitCombatProfile>(2);
            if (mainTank != null)
            {
                front.Add(mainTank);
            }

            if (offTank != null)
            {
                front.Add(offTank);
            }

            List<UnitCombatProfile> melee = Collect(profiles, FormationLine.Melee);
            List<UnitCombatProfile> support = Collect(profiles, FormationLine.Support);
            List<UnitCombatProfile> ranged = Collect(profiles, FormationLine.Ranged);
            float y = 0f;
            CacheLine(front, ref y, settings.PartyLateralSpacing, 2);
            float pending = settings.PartyFrontGap;
            pending = CacheGradedLine(melee, ref y, pending, settings.PartyMidGap, settings);
            pending = CacheGradedLine(support, ref y, pending, settings.PartyBackGap, settings);
            CacheGradedLine(ranged, ref y, pending, settings.PartyBackGap, settings);
        }

        private static float CacheGradedLine(List<UnitCombatProfile> line, ref float y, float gapBefore, float nextGap, Settings settings)
        {
            if (line.Count == 0)
            {
                return Mathf.Max(gapBefore, nextGap);
            }

            y -= gapBefore;
            CacheLine(line, ref y, settings.PartyLateralSpacing, MaxPerRow);
            return nextGap;
        }

        private static List<UnitCombatProfile> Collect(List<UnitCombatProfile> profiles, FormationLine line)
        {
            List<UnitCombatProfile> result = new List<UnitCombatProfile>();
            foreach (UnitCombatProfile p in profiles)
            {
                if (p.Line == line)
                {
                    result.Add(p);
                }
            }

            result.Sort((a, b) =>
            {
                int byDurability = b.Durability.CompareTo(a.Durability);
                return byDurability != 0 ? byDurability : string.CompareOrdinal(StableOrder(a), StableOrder(b));
            });
            return result;
        }

        private static void CacheLine(List<UnitCombatProfile> line, ref float y, float spacing, int maxPerRow)
        {
            int index = 0;
            while (index < line.Count)
            {
                int count = Mathf.Min(maxPerRow, line.Count - index);
                float startX = -spacing * 0.5f * (count - 1);
                for (int i = 0; i < count; i++)
                {
                    UnitCombatProfile p = line[index + i];
                    CachedOffsets[p.Unit.UniqueId] = new Vector2(startX + i * spacing, y);
                }

                index += count;
                if (index < line.Count)
                {
                    y -= SubRowGap;
                }
            }
        }

        private static int GetRecommendedTankArmorClass()
        {
            int difficulty = SettingsRoot.Difficulty.EnemyDifficulty - 1;
            return 10 + Mathf.CeilToInt(Game.Instance.Player.PartyLevel * 1.5f) + 4 + difficulty;
        }

        private static string StableOrder(UnitCombatProfile p)
        {
            return p.Unit?.UniqueId ?? string.Empty;
        }
    }
}
