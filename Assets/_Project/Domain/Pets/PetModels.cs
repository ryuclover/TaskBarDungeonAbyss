using System;
using System.Collections.Generic;

namespace TBDA.Domain.Pets
{
    public enum PetSpeciesId
    {
        Dog = 0,
        Cat = 1,
        SpiritGolem = 2
    }

    public enum PetActivityState
    {
        Idle = 0,
        RestingAtCampfire = 1,
        TrainingStrength = 2,
        TrainingAgility = 3,
        TrainingVitality = 4,
        ExploringDungeonNode = 5
    }

    public enum CombatStatType
    {
        Strength = 0,
        Agility = 1,
        Vitality = 2
    }

    [Serializable]
    public class PetVitals
    {
        // Fixed 0..100 scale (integer-friendly for DS portability)
        public float Hunger = 80f;     // 100 = full, 0 = starving
        public float Affection = 70f;  // 100 = loved, 0 = lonely
        public float Energy = 85f;     // 100 = rested, 0 = exhausted
        public float Health = 100f;    // 100 = healthy

        public void ClampAll()
        {
            Hunger = Clamp0100(Hunger);
            Affection = Clamp0100(Affection);
            Energy = Clamp0100(Energy);
            Health = Clamp0100(Health);
        }

        private static float Clamp0100(float v) => v < 0f ? 0f : (v > 100f ? 100f : v);
    }

    [Serializable]
    public class PetCombatStats
    {
        public int Strength = 2;
        public int Agility = 2;
        public int Vitality = 2;

        // Punch Club sub-bars (0..100 points to reach next stat integer)
        public float StrengthProgress = 0f;
        public float AgilityProgress = 0f;
        public float VitalityProgress = 0f;

        public int TotalPowerScore => Strength * 3 + Agility * 3 + Vitality * 3;
        public int MaxBattleHp => 35 + Vitality * 12;

        public int GetStatLevel(CombatStatType type)
        {
            switch (type)
            {
                case CombatStatType.Strength: return Strength;
                case CombatStatType.Agility: return Agility;
                default: return Vitality;
            }
        }

        public float GetStatProgress(CombatStatType type)
        {
            switch (type)
            {
                case CombatStatType.Strength: return StrengthProgress;
                case CombatStatType.Agility: return AgilityProgress;
                default: return VitalityProgress;
            }
        }
    }

    [Serializable]
    public class PetStateModel
    {
        public string PetName = "Bolinha";
        public PetSpeciesId Species = PetSpeciesId.Dog;
        public string CurrentFormId = "base_child"; // Ready for Digimon branching evolutions
        public PetActivityState Activity = PetActivityState.Idle;
        public PetVitals Vitals = new PetVitals();
        public PetCombatStats Stats = new PetCombatStats();
        public int TotalClicksEncouraged = 0;
        public int ExpeditionsCompleted = 0;
    }

    [Serializable]
    public class CareItemConfig
    {
        public string ItemId;
        public string DisplayName;
        public string Description;
        public float HungerRestore;
        public float EnergyRestore;
        public float AffectionBoost;
        public float HealthRestore;
        public int ShardPrice;
    }

    [Serializable]
    public class TrainingDrillConfig
    {
        public string DrillId;
        public string DisplayName;
        public CombatStatType TargetStat;
        public PetActivityState CorrespondingActivity;
        public float BaseProgressPerTick;
        public float ClickBonusProgress;
        public float HungerCostPerTick;
        public float EnergyCostPerTick;
    }

    [Serializable]
    public class InventoryEntry
    {
        public string ItemId;
        public int Quantity;
    }
}
