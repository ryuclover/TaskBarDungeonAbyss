using System;
using System.Collections.Generic;
using TBDA.Domain.Care;
using TBDA.Domain.Events;
using TBDA.Domain.Pets;

namespace TBDA.Domain.Training
{
    /// <summary>
    /// Punch Club-inspired stat training + slow Cookie Clicker manual focus impulse.
    /// Balances Hunger/Energy consumption against STR / AGI / VIT sub-bar gains.
    /// </summary>
    public sealed class PunchClubTrainingSystem
    {
        private readonly DomainEventBus _bus;
        private readonly TamagotchiCareSystem _careSystem;
        private readonly List<TrainingDrillConfig> _drills;
        private float _decayTimerSeconds;

        public PunchClubTrainingSystem(
            DomainEventBus bus,
            TamagotchiCareSystem careSystem,
            List<TrainingDrillConfig> drills)
        {
            _bus = bus;
            _careSystem = careSystem;
            _drills = drills ?? new List<TrainingDrillConfig>();
        }

        public TrainingDrillConfig GetDrillForActivity(PetActivityState state)
        {
            for (int i = 0; i < _drills.Count; i++)
            {
                if (_drills[i].CorrespondingActivity == state)
                    return _drills[i];
            }
            return null;
        }

        public bool StartTraining(PetStateModel pet, PetActivityState trainingActivity)
        {
            if (pet.Activity == PetActivityState.ExploringDungeonNode)
                return false;

            if (trainingActivity == PetActivityState.Idle ||
                trainingActivity == PetActivityState.RestingAtCampfire)
            {
                pet.Activity = trainingActivity;
                _bus.Publish(new PetStateUpdatedEvent { Reason = "ActivityChanged" });
                return true;
            }

            if (pet.Vitals.Energy < 12f || pet.Vitals.Hunger < 12f)
            {
                _bus.Publish(new FloatingFeedbackEvent
                {
                    Message = $"{pet.PetName} precisa comer/descansar antes de treinar!",
                    Tone = FeedbackTone.Warning
                });
                pet.Activity = PetActivityState.RestingAtCampfire;
                _bus.Publish(new PetStateUpdatedEvent { Reason = "TooTired" });
                return false;
            }

            pet.Activity = trainingActivity;
            _bus.Publish(new PetStateUpdatedEvent { Reason = "StartedTraining" });
            return true;
        }

        public void TickTraining(PetStateModel pet, float deltaSeconds)
        {
            // Gentle Punch Club-style unfocused stat decay every 45s on non-active sub-bars above Lv 3
            _decayTimerSeconds += deltaSeconds;
            if (_decayTimerSeconds >= 45f)
            {
                _decayTimerSeconds = 0f;
                ApplyGentlePunchClubDecay(pet);
            }

            var drill = GetDrillForActivity(pet.Activity);
            if (drill == null) return;

            // Check exhaustion safety: pet walks to campfire automatically instead of dying on taskbar
            if (pet.Vitals.Energy <= 6f || pet.Vitals.Hunger <= 6f)
            {
                pet.Activity = PetActivityState.RestingAtCampfire;
                _bus.Publish(new FloatingFeedbackEvent
                {
                    Message = $"{pet.PetName} cansou e foi descansar na fogueira.",
                    Tone = FeedbackTone.Warning
                });
                _bus.Publish(new PetStateUpdatedEvent { Reason = "AutoRest" });
                return;
            }

            pet.Vitals.Hunger -= drill.HungerCostPerTick * deltaSeconds;
            pet.Vitals.Energy -= drill.EnergyCostPerTick * deltaSeconds;
            pet.Vitals.ClampAll();

            float moodMult = _careSystem.ComputeTrainingMoodMultiplier(pet.Vitals);
            int currentLevel = pet.Stats.GetStatLevel(drill.TargetStat);
            // Slow scaling curve so higher levels feel rewarding
            float levelScale = 1f / (1f + (currentLevel - 1) * 0.18f);
            float gained = drill.BaseProgressPerTick * moodMult * levelScale * deltaSeconds;

            AddStatProgress(pet, drill.TargetStat, gained);
        }

        /// <summary>
        /// Slow Cookie-Clicker style click encouragement during training or idle.
        /// Returns Soul Shards earned (0 or 1).
        /// </summary>
        public int ApplyManualEncourageClick(PetStateModel pet)
        {
            pet.TotalClicksEncouraged++;
            var drill = GetDrillForActivity(pet.Activity);

            if (drill != null && pet.Vitals.Energy > 4f)
            {
                int currentLevel = pet.Stats.GetStatLevel(drill.TargetStat);
                float levelScale = 1f / (1f + (currentLevel - 1) * 0.18f);
                float clickGain = drill.ClickBonusProgress * levelScale;
                pet.Vitals.Energy -= 0.6f;
                pet.Vitals.ClampAll();

                AddStatProgress(pet, drill.TargetStat, clickGain);
                _bus.Publish(new FloatingFeedbackEvent
                {
                    Message = $"+{clickGain:0.0}% {GetShortStatName(drill.TargetStat)}!",
                    Tone = FeedbackTone.Positive
                });
            }
            else
            {
                // Slow clicker shard drip every few clicks even when resting
                _bus.Publish(new FloatingFeedbackEvent
                {
                    Message = "+1 Pulso de Alma",
                    Tone = FeedbackTone.Neutral
                });
            }

            return (pet.TotalClicksEncouraged % 3 == 0) ? 1 : 0;
        }

        public void AddStatProgress(PetStateModel pet, CombatStatType stat, float amount)
        {
            switch (stat)
            {
                case CombatStatType.Strength:
                    pet.Stats.StrengthProgress += amount;
                    while (pet.Stats.StrengthProgress >= 100f)
                    {
                        pet.Stats.StrengthProgress -= 100f;
                        pet.Stats.Strength++;
                        PublishLevelUp("FOR", pet.Stats.Strength);
                    }
                    break;

                case CombatStatType.Agility:
                    pet.Stats.AgilityProgress += amount;
                    while (pet.Stats.AgilityProgress >= 100f)
                    {
                        pet.Stats.AgilityProgress -= 100f;
                        pet.Stats.Agility++;
                        PublishLevelUp("AGI", pet.Stats.Agility);
                    }
                    break;

                case CombatStatType.Vitality:
                    pet.Stats.VitalityProgress += amount;
                    while (pet.Stats.VitalityProgress >= 100f)
                    {
                        pet.Stats.VitalityProgress -= 100f;
                        pet.Stats.Vitality++;
                        PublishLevelUp("VIT", pet.Stats.Vitality);
                    }
                    break;
            }
        }

        private void ApplyGentlePunchClubDecay(PetStateModel pet)
        {
            // Only trims sub-progress bar (never drops already-unlocked integer stat tiers in Demo!)
            if (pet.Activity != PetActivityState.TrainingStrength && pet.Stats.Strength >= 4)
                pet.Stats.StrengthProgress = Math.Max(0f, pet.Stats.StrengthProgress - 2.5f);

            if (pet.Activity != PetActivityState.TrainingAgility && pet.Stats.Agility >= 4)
                pet.Stats.AgilityProgress = Math.Max(0f, pet.Stats.AgilityProgress - 2.5f);

            if (pet.Activity != PetActivityState.TrainingVitality && pet.Stats.Vitality >= 4)
                pet.Stats.VitalityProgress = Math.Max(0f, pet.Stats.VitalityProgress - 2.5f);
        }

        private void PublishLevelUp(string statCode, int newLevel)
        {
            _bus.Publish(new StatLevelUpEvent { StatCode = statCode, NewLevel = newLevel });
            _bus.Publish(new FloatingFeedbackEvent
            {
                Message = $"SUBIU! {statCode} Nv.{newLevel} ★",
                Tone = FeedbackTone.StatLevelUp
            });
        }

        public static string GetShortStatName(CombatStatType stat)
        {
            switch (stat)
            {
                case CombatStatType.Strength: return "FOR";
                case CombatStatType.Agility: return "AGI";
                default: return "VIT";
            }
        }
    }
}
