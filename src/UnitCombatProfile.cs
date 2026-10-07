#nullable disable
using System;
using System.Collections.Generic;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Items.Armors;
using Kingmaker.Blueprints.Items.Weapons;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.Formations.Facts;
using Kingmaker.Items;
using Kingmaker.Items.Slots;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Parts;
using UnityEngine;

namespace SummonsTransitionFix
{
    internal enum FormationLine
    {
        Front,
        Melee,
        Support,
        Ranged
    }

    internal sealed class UnitCombatProfile
    {
        private const float MeleeWeaponWeight = 0.30f;
        private const float ReachWeaponWeight = 0.16f;
        private const float NaturalWeaponWeight = 0.26f;
        private const float TwoHandedWeight = 0.06f;
        private const float RangedWeaponWeight = -0.50f;
        private const float RayAbilityWeight = -0.26f;
        private const float BabPivot = 0.625f;
        private const float BabWeight = 0.64f;
        private const float ArcaneWeight = -0.30f;
        private const float DivineWeight = -0.08f;
        private const float SpellbookWeight = -0.14f;
        private const float ShieldWeight = 0.14f;
        private const float HeavyArmorWeight = 0.20f;
        private const float MediumArmorWeight = 0.10f;
        private const float LightArmorWeight = -0.04f;
        private const float NoArmorWeight = -0.12f;
        private const float AnimalCompanionWeight = 0.12f;
        private const float IncapacitatedPenalty = -0.35f;

        internal UnitEntityData Unit;
        internal int ArmorClass;
        internal int MaxHitPoints;
        internal int Fortitude;
        internal int Will;
        internal float BabRatio = 0.75f;
        internal float Corpulence = 0.5f;
        internal bool HasShield;
        internal bool RangedPrimary;
        internal bool MeleePrimary;
        internal bool ReachWeapon;
        internal bool NaturalOrUnarmed;
        internal bool TwoHandedMelee;
        internal bool RayAutoAbility;
        internal bool Incapacitated;
        internal bool IsAnimalCompanion;
        internal ArmorProficiencyGroup ArmorGroup = ArmorProficiencyGroup.None;
        internal float ArcaneShare;
        internal float DivineShare;
        internal float SpellbookShare;
        internal float MeleeAffinity;
        internal float Durability;
        internal FormationLine Line = FormationLine.Melee;

        internal static UnitCombatProfile Build(UnitEntityData unit)
        {
            UnitCombatProfile p = new UnitCombatProfile
            {
                Unit = unit
            };
            UnitEntityData mount = unit.GetSaddledUnit();
            CharacterStats armorSource = (mount ?? unit).Descriptor?.Stats;
            if (armorSource != null)
            {
                p.ArmorClass = GetFormationArmorClass(mount ?? unit, armorSource);
            }

            CharacterStats stats = unit.Descriptor?.Stats;
            if (stats != null)
            {
                p.MaxHitPoints = stats.HitPoints.ModifiedValue;
                p.Fortitude = stats.SaveFortitude.ModifiedValue;
                p.Will = stats.SaveWill.ModifiedValue;
            }

            p.Corpulence = unit.Corpulence;
            p.Incapacitated = !IsConscious(unit);
            p.IsAnimalCompanion = IsAnimal(unit);
            p.ReadEquipment(unit);
            p.ReadProgression(unit);
            p.RayAutoAbility = HasRayAutoAbility(unit);
            p.MeleeAffinity = p.ComputeMeleeAffinity();
            return p;
        }

        private void ReadEquipment(UnitEntityData unit)
        {
            UnitBody body = unit.Body;
            if (body == null)
            {
                return;
            }

            HandSlot hand = body.PrimaryHand;
            BlueprintItemWeapon bp = hand != null && hand.Active ? (hand.MaybeItem as ItemEntityWeapon)?.Blueprint : null;
            if (bp != null)
            {
                if (bp.IsRanged)
                {
                    RangedPrimary = true;
                }
                else
                {
                    MeleePrimary = true;
                    NaturalOrUnarmed = bp.IsNatural || bp.IsUnarmed;
                    TwoHandedMelee = bp.IsTwoHanded;
                    ReachWeapon = bp.AttackRange.Meters > 2.0f;
                }
            }
            else if (FightsWithBody(body))
            {
                MeleePrimary = true;
                NaturalOrUnarmed = true;
            }

            HasShield = body.SecondaryHand?.MaybeShield != null;
            ItemEntityArmor armor = body.Armor?.MaybeArmor;
            ArmorGroup = armor?.ArmorType() ?? ArmorProficiencyGroup.None;
        }

        private void ReadProgression(UnitEntityData unit)
        {
            UnitProgressionData progression = unit.Descriptor?.Progression;
            if (progression == null)
            {
                return;
            }

            int levels = 0;
            int bab = 0;
            int arcane = 0;
            int divine = 0;
            int spellbook = 0;
            foreach (ClassData data in progression.Classes)
            {
                BlueprintCharacterClass cls = data.CharacterClass;
                if (cls == null || cls.IsMythic || data.Level <= 0)
                {
                    continue;
                }

                levels += data.Level;
                BlueprintStatProgression babProgression = data.BaseAttackBonus;
                if (babProgression != null)
                {
                    bab += babProgression.GetBonus(data.Level);
                }

                if (cls.IsArcaneCaster)
                {
                    arcane += data.Level;
                }

                if (cls.IsDivineCaster)
                {
                    divine += data.Level;
                }

                if (data.Spellbook != null)
                {
                    spellbook += data.Level;
                }
            }

            if (levels <= 0)
            {
                int hitDice = progression.CharacterLevel;
                CharacterStats stats = unit.Descriptor?.Stats;
                if (hitDice > 0 && stats != null)
                {
                    BabRatio = Mathf.Clamp((float)stats.BaseAttackBonus.ModifiedValue / hitDice, 0f, 1f);
                }

                return;
            }

            BabRatio = (float)bab / levels;
            ArcaneShare = (float)arcane / levels;
            DivineShare = (float)divine / levels;
            SpellbookShare = (float)spellbook / levels;
        }

        private float ComputeMeleeAffinity()
        {
            float score = 0f;
            if (RangedPrimary)
            {
                score += RangedWeaponWeight;
            }
            else if (NaturalOrUnarmed)
            {
                score += NaturalWeaponWeight;
            }
            else if (ReachWeapon)
            {
                score += ReachWeaponWeight;
            }
            else if (MeleePrimary)
            {
                score += MeleeWeaponWeight;
            }

            if (TwoHandedMelee)
            {
                score += TwoHandedWeight;
            }

            if (RayAutoAbility)
            {
                score += RayAbilityWeight;
            }

            score += (BabRatio - BabPivot) * BabWeight;
            score += ArcaneShare * ArcaneWeight;
            score += DivineShare * DivineWeight;
            score += SpellbookShare * SpellbookWeight;
            if (HasShield)
            {
                score += ShieldWeight;
            }

            switch (ArmorGroup)
            {
                case ArmorProficiencyGroup.Heavy:
                case ArmorProficiencyGroup.HeavyBarding:
                    score += HeavyArmorWeight;
                    break;
                case ArmorProficiencyGroup.Medium:
                case ArmorProficiencyGroup.MediumBarding:
                    score += MediumArmorWeight;
                    break;
                case ArmorProficiencyGroup.Light:
                case ArmorProficiencyGroup.LightBarding:
                    score += LightArmorWeight;
                    break;
                default:
                    score += NoArmorWeight;
                    break;
            }

            if (IsAnimalCompanion)
            {
                score += AnimalCompanionWeight;
            }

            if (Incapacitated)
            {
                score += IncapacitatedPenalty;
            }

            return Mathf.Clamp(score, -1f, 1f);
        }

        internal static void ComputeDurability(List<UnitCombatProfile> profiles)
        {
            if (profiles.Count == 0)
            {
                return;
            }

            Range ac = Range.Of(profiles, p => p.ArmorClass);
            Range hp = Range.Of(profiles, p => p.MaxHitPoints);
            Range fort = Range.Of(profiles, p => p.Fortitude);
            Range will = Range.Of(profiles, p => p.Will);
            foreach (UnitCombatProfile p in profiles)
            {
                float protection = 0f;
                if (p.ArmorGroup == ArmorProficiencyGroup.Heavy || p.ArmorGroup == ArmorProficiencyGroup.HeavyBarding)
                {
                    protection += 0.6f;
                }
                else if (p.ArmorGroup == ArmorProficiencyGroup.Medium || p.ArmorGroup == ArmorProficiencyGroup.MediumBarding)
                {
                    protection += 0.3f;
                }

                if (p.HasShield)
                {
                    protection += 0.4f;
                }

                p.Durability = ac.Normalize(p.ArmorClass) * 0.50f + hp.Normalize(p.MaxHitPoints) * 0.24f + fort.Normalize(p.Fortitude) * 0.08f + will.Normalize(p.Will) * 0.08f + Mathf.Clamp01(protection) * 0.10f;
                if (p.Incapacitated)
                {
                    p.Durability *= 0.25f;
                }
            }
        }

        private static int GetFormationArmorClass(UnitEntityData unit, CharacterStats stats)
        {
            int value = stats.AC.ModifiedValue;
            EntityFactsManager facts = unit.Facts;
            if (facts == null)
            {
                return value;
            }

            foreach (EntityFact fact in facts.List)
            {
                BlueprintComponent[] components = fact.Blueprint?.ComponentsArray;
                if (components == null)
                {
                    continue;
                }

                for (int i = 0; i < components.Length; i++)
                {
                    FormationACBonus bonus = components[i] as FormationACBonus;
                    if (bonus != null)
                    {
                        value += bonus.GetBonus(unit);
                    }
                }
            }

            return value;
        }

        private static bool FightsWithBody(UnitBody body)
        {
            ItemEntityWeapon empty = body.EmptyHandWeapon;
            if (empty != null && (empty.IsMonkUnarmedStrike || (empty.Blueprint != null && empty.Blueprint.IsNatural)))
            {
                return true;
            }

            List<WeaponSlot> limbs = body.AdditionalLimbs;
            if (limbs == null)
            {
                return false;
            }

            for (int i = 0; i < limbs.Count; i++)
            {
                WeaponSlot limb = limbs[i];
                BlueprintItemWeapon bp = limb != null && limb.Active ? (limb.MaybeItem as ItemEntityWeapon)?.Blueprint : null;
                if (bp != null && !bp.IsRanged)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasRayAutoAbility(UnitEntityData unit)
        {
            AbilityData ability = unit.Brain?.GetAvailableAutoUseAbility();
            return ability != null && ability.IsRay;
        }

        private static bool IsAnimal(UnitEntityData unit)
        {
            UnitPartPet pet = unit.Get<UnitPartPet>();
            return pet != null && pet.Type == PetType.AnimalCompanion;
        }

        private static bool IsConscious(UnitEntityData unit)
        {
            UnitState state = unit.Descriptor?.State;
            return state == null || state.IsConscious;
        }

        internal struct Range
        {
            private float m_Min;
            private float m_Max;
            internal static Range Of(List<UnitCombatProfile> profiles, System.Func<UnitCombatProfile, float> selector)
            {
                Range r = new Range
                {
                    m_Min = float.MaxValue,
                    m_Max = float.MinValue
                };
                foreach (UnitCombatProfile p in profiles)
                {
                    float v = selector(p);
                    if (v < r.m_Min)
                    {
                        r.m_Min = v;
                    }

                    if (v > r.m_Max)
                    {
                        r.m_Max = v;
                    }
                }

                return r;
            }

            internal float Normalize(float value)
            {
                float span = m_Max - m_Min;
                if (span <= 0.0001f)
                {
                    return 0.5f;
                }

                return Mathf.Clamp01((value - m_Min) / span);
            }
        }
    }
}
