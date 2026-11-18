using System;
using TBDA.Domain.Events;
using TBDA.Domain.Pets;

namespace TBDA.Domain.Care
{
    /// <summary>
    /// Pure C# Tamagotchi care rules: vital decay, feeding, stylus petting,campfire rest,
    /// and mood training multiplier (inspired by Nintendogs + Digimon World DS + Punch Club).
    /// </summary>
    public sealed class TamagotchiCareSystem
    {
        private readonly DomainEventBus _bus;
        private double _lastStylusPetTimeSeconds = -999.0;
        private const double PetCooldownSeconds = 1.6;

        public TamagotchiCareSystem(DomainEventBus bus)
        {
            _bus = bus;
        }

        public float ComputeTrainingMoodMultiplier(PetVitals vitals)
        {
            float moodFactor = 0.70f + (vitals.Affection / 100f) * 0.55f; // 0.70x .. 1.25x
            if (vitals.Hunger < 25f) moodFactor *= 0.65f;
            if (vitals.Energy < 20f) moodFactor *= 0.60f;
            return moodFactor;
        }

        public void TickCare(PetStateModel pet, float deltaSeconds)
        {
            var v = pet.Vitals;

            if (pet.Activity == PetActivityState.RestingAtCampfire)
            {
                v.Energy += 5.5f * deltaSeconds;
                v.Health += 2.0f * deltaSeconds;
                v.Hunger -= 0.35f * deltaSeconds;
                v.Affection -= 0.10f * deltaSeconds;
            }
            else if (pet.Activity == PetActivityState.Idle)
            {
                v.Hunger -= 0.42f * deltaSeconds;
                v.Energy += 0.85f * deltaSeconds;
                v.Affection -= 0.18f * deltaSeconds;
            }
            else
            {
                // Active training or exploring drains slightly faster (drill adds specific cost)
                v.Affection -= 0.12f * deltaSeconds;
            }

            if (v.Hunger <= 5f)
            {
                v.Health -= 0.6f * deltaSeconds;
            }
            else if (v.Hunger > 60f && v.Energy > 50f)
            {
                v.Health += 0.5f * deltaSeconds;
            }

            v.ClampAll();
        }

        public bool TryFeedItem(PetStateModel pet, CareItemConfig item)
        {
            if (item == null) return false;

            pet.Vitals.Hunger += item.HungerRestore;
            pet.Vitals.Energy += item.EnergyRestore;
            pet.Vitals.Affection += item.AffectionBoost;
            pet.Vitals.Health += item.HealthRestore;
            pet.Vitals.ClampAll();

            _bus.Publish(new FloatingFeedbackEvent
            {
                Message = $"+{ (int)item.HungerRestore } Fome ({item.DisplayName})",
                Tone = FeedbackTone.Positive
            });
            _bus.Publish(new PetStateUpdatedEvent { Reason = "Fed" });
            return true;
        }

        public bool TryStylusPet(PetStateModel pet, double elapsedSeconds)
        {
            if (elapsedSeconds - _lastStylusPetTimeSeconds < PetCooldownSeconds)
            {
                return false;
            }

            _lastStylusPetTimeSeconds = elapsedSeconds;
            pet.Vitals.Affection += 6.5f;
            pet.Vitals.ClampAll();
            pet.TotalClicksEncouraged++;

            _bus.Publish(new FloatingFeedbackEvent
            {
                Message = $"{pet.PetName} adorou o carinho! ♥",
                Tone = FeedbackTone.Love
            });
            _bus.Publish(new PetStateUpdatedEvent { Reason = "Petted" });
            return true;
        }
    }
}
