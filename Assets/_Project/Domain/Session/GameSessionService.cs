using System;
using System.Collections.Generic;
using TBDA.Domain.Care;
using TBDA.Domain.DungeonMap;
using TBDA.Domain.Events;
using TBDA.Domain.Future;
using TBDA.Domain.Pets;
using TBDA.Domain.Training;

namespace TBDA.Domain.Session
{
    [Serializable]
    public class GameSaveData
    {
        public int SchemaVersion = 1;
        public bool HasCompletedIntro = false;
        public long LastSavedUnixSeconds = 0;
        public int SoulShards = 5;
        public PetStateModel ActivePet = new PetStateModel();
        public List<InventoryEntry> Inventory = new List<InventoryEntry>();
        public DungeonMapState DungeonMap = new DungeonMapState();
        public FutureProgressionFlags FutureFlags = new FutureProgressionFlags();
    }

    /// <summary>
    /// Central pure-C# game session orchestrator.
    /// Zero UnityEngine references: 100% deterministic, testable, and portable.
    /// </summary>
    public sealed class GameSessionService
    {
        public DomainEventBus EventBus { get; }
        public GameSaveData State { get; private set; }
        public TamagotchiCareSystem Care { get; }
        public PunchClubTrainingSystem Training { get; }
        public DungeonMiniMapSystem MiniMap { get; }

        private readonly List<CareItemConfig> _careCatalog;
        private double _sessionElapsedSeconds;

        public GameSessionService(
            List<CareItemConfig> careItems,
            List<TrainingDrillConfig> trainingDrills,
            GameSaveData loadedSave = null)
        {
            EventBus = new DomainEventBus();
            _careCatalog = careItems ?? BuildFallbackCareCatalog();
            var drills = (trainingDrills != null && trainingDrills.Count > 0)
                ? trainingDrills
                : BuildFallbackDrills();

            Care = new TamagotchiCareSystem(EventBus);
            Training = new PunchClubTrainingSystem(EventBus, Care, drills);
            MiniMap = new DungeonMiniMapSystem(EventBus, Training);

            if (loadedSave != null)
            {
                State = loadedSave;
                if (State.DungeonMap.Nodes == null || State.DungeonMap.Nodes.Count == 0)
                    MiniMap.GenerateFloorMap(State.DungeonMap, 1, 1337);
            }
            else
            {
                State = CreateFreshSave();
            }
        }

        public GameSaveData CreateFreshSave()
        {
            var fresh = new GameSaveData
            {
                SchemaVersion = 1,
                HasCompletedIntro = false,
                SoulShards = 6
            };
            fresh.Inventory.Add(new InventoryEntry { ItemId = "food_moss", Quantity = 4 });
            fresh.Inventory.Add(new InventoryEntry { ItemId = "food_bread", Quantity = 2 });
            fresh.Inventory.Add(new InventoryEntry { ItemId = "food_stew", Quantity = 1 });
            MiniMap.GenerateFloorMap(fresh.DungeonMap, 1, Environment.TickCount & 0x7FFF);
            return fresh;
        }

        public void CompleteIntroAndAwakenPet(PetSpeciesId species, string rawPetName)
        {
            string cleanName = string.IsNullOrWhiteSpace(rawPetName) ? GetDefaultPetName(species) : rawPetName.Trim();
            if (cleanName.Length > 14) cleanName = cleanName.Substring(0, 14);

            State.ActivePet.Species = species;
            State.ActivePet.PetName = cleanName;
            State.ActivePet.Activity = PetActivityState.Idle;
            State.ActivePet.Vitals = new PetVitals { Hunger = 78f, Affection = 75f, Energy = 88f, Health = 100f };

            // Distinct starting stat personality per chosen species!
            switch (species)
            {
                case PetSpeciesId.Dog:
                    State.ActivePet.Stats = new PetCombatStats { Strength = 3, Agility = 2, Vitality = 3 };
                    break;
                case PetSpeciesId.Cat:
                    State.ActivePet.Stats = new PetCombatStats { Strength = 2, Agility = 4, Vitality = 2 };
                    break;
                case PetSpeciesId.SpiritGolem:
                    State.ActivePet.Stats = new PetCombatStats { Strength = 3, Agility = 1, Vitality = 4 };
                    break;
            }

            State.HasCompletedIntro = true;
            MiniMap.GenerateFloorMap(State.DungeonMap, 1, cleanName.GetHashCode() & 0x7FFF);

            EventBus.Publish(new IntroCompletedEvent { PetName = cleanName });
            EventBus.Publish(new PetStateUpdatedEvent { Reason = "IntroCompleted" });
        }

        public void TickSimulation(float deltaSeconds)
        {
            if (!State.HasCompletedIntro || deltaSeconds <= 0f) return;

            _sessionElapsedSeconds += deltaSeconds;
            Care.TickCare(State.ActivePet, deltaSeconds);
            Training.TickTraining(State.ActivePet, deltaSeconds);
            MiniMap.TickMap(State.DungeonMap, State.ActivePet, State.Inventory, ref State.SoulShards, deltaSeconds);
        }

        public void ApplyOfflineCatchup(long currentUnixSeconds)
        {
            if (!State.HasCompletedIntro || State.LastSavedUnixSeconds <= 0) return;

            long elapsed = currentUnixSeconds - State.LastSavedUnixSeconds;
            if (elapsed <= 5) return;

            // Cap offline simulation to 2 hours for gentle Tamagotchi fairness
            float simulatedSeconds = Math.Min(elapsed, 7200) * 0.25f;
            const float step = 2.0f;
            while (simulatedSeconds > 0f)
            {
                float dt = Math.Min(step, simulatedSeconds);
                TickSimulation(dt);
                simulatedSeconds -= dt;
            }
        }

        public bool FeedPetFromInventory(string itemId)
        {
            var cfg = GetCareItemConfig(itemId);
            if (cfg == null) return false;

            for (int i = 0; i < State.Inventory.Count; i++)
            {
                if (State.Inventory[i].ItemId == itemId && State.Inventory[i].Quantity > 0)
                {
                    State.Inventory[i].Quantity--;
                    Care.TryFeedItem(State.ActivePet, cfg);
                    return true;
                }
            }

            // Allow buying instant ration with Soul Shards if out of stock!
            if (State.SoulShards >= cfg.ShardPrice && cfg.ShardPrice > 0)
            {
                State.SoulShards -= cfg.ShardPrice;
                Care.TryFeedItem(State.ActivePet, cfg);
                return true;
            }

            EventBus.Publish(new FloatingFeedbackEvent
            {
                Message = "Sem estoque! Explore o Mini-Mapa para colher comida.",
                Tone = FeedbackTone.Warning
            });
            return false;
        }

        public int GetInventoryCount(string itemId)
        {
            for (int i = 0; i < State.Inventory.Count; i++)
            {
                if (State.Inventory[i].ItemId == itemId) return State.Inventory[i].Quantity;
            }
            return 0;
        }

        public CareItemConfig GetCareItemConfig(string itemId)
        {
            for (int i = 0; i < _careCatalog.Count; i++)
            {
                if (_careCatalog[i].ItemId == itemId) return _careCatalog[i];
            }
            return null;
        }

        public IReadOnlyList<CareItemConfig> GetAllCareItems() => _careCatalog;

        public void HandlePetStylusClicked()
        {
            if (!State.HasCompletedIntro) return;
            if (!Care.TryStylusPet(State.ActivePet, _sessionElapsedSeconds))
            {
                State.SoulShards += Training.ApplyManualEncourageClick(State.ActivePet);
            }
        }

        public void HandleTrainingManualClick()
        {
            if (!State.HasCompletedIntro) return;
            State.SoulShards += Training.ApplyManualEncourageClick(State.ActivePet);
            EventBus.Publish(new PetStateUpdatedEvent { Reason = "ManualClick" });
        }

        public void SetPetActivity(PetActivityState targetActivity)
        {
            Training.StartTraining(State.ActivePet, targetActivity);
        }

        public bool StartMapNodeExpedition(int nodeId)
        {
            return MiniMap.TryStartExpedition(State.DungeonMap, State.ActivePet, nodeId);
        }

        public void ResetNewDemoRun()
        {
            State = CreateFreshSave();
            EventBus.Publish(new PetStateUpdatedEvent { Reason = "Reset" });
        }

        private static string GetDefaultPetName(PetSpeciesId species)
        {
            switch (species)
            {
                case PetSpeciesId.Dog: return "Pipoca";
                case PetSpeciesId.Cat: return "Mimi";
                default: return "Rocco";
            }
        }

        public static List<CareItemConfig> BuildFallbackCareCatalog()
        {
            return new List<CareItemConfig>
            {
                new CareItemConfig
                {
                    ItemId = "food_moss",
                    DisplayName = "Musgo Doce",
                    Description = "Lanche leve das paredes úmidas (+22 Fome, +4 Afeto).",
                    HungerRestore = 22f,
                    EnergyRestore = 5f,
                    AffectionBoost = 4f,
                    HealthRestore = 3f,
                    ShardPrice = 3
                },
                new CareItemConfig
                {
                    ItemId = "food_bread",
                    DisplayName = "Pão Anão",
                    Description = "Sustenta treinos pesados (+40 Fome, +15 Energia).",
                    HungerRestore = 40f,
                    EnergyRestore = 15f,
                    AffectionBoost = 6f,
                    HealthRestore = 8f,
                    ShardPrice = 6
                },
                new CareItemConfig
                {
                    ItemId = "food_stew",
                    DisplayName = "Sopa Brilhante",
                    Description = "Refeição quentinha de fogueira (+60 Fome, +30 Energia, +15 Afeto).",
                    HungerRestore = 60f,
                    EnergyRestore = 30f,
                    AffectionBoost = 15f,
                    HealthRestore = 25f,
                    ShardPrice = 10
                }
            };
        }

        public static List<TrainingDrillConfig> BuildFallbackDrills()
        {
            return new List<TrainingDrillConfig>
            {
                new TrainingDrillConfig
                {
                    DrillId = "drill_str",
                    DisplayName = "Saco de Pedra [FOR]",
                    TargetStat = CombatStatType.Strength,
                    CorrespondingActivity = PetActivityState.TrainingStrength,
                    BaseProgressPerTick = 5.5f,
                    ClickBonusProgress = 4.2f,
                    HungerCostPerTick = 1.25f,
                    EnergyCostPerTick = 1.45f
                },
                new TrainingDrillConfig
                {
                    DrillId = "drill_agi",
                    DisplayName = "Alvos Ágeis [AGI]",
                    TargetStat = CombatStatType.Agility,
                    CorrespondingActivity = PetActivityState.TrainingAgility,
                    BaseProgressPerTick = 5.8f,
                    ClickBonusProgress = 4.5f,
                    HungerCostPerTick = 1.15f,
                    EnergyCostPerTick = 1.55f
                },
                new TrainingDrillConfig
                {
                    DrillId = "drill_vit",
                    DisplayName = "Runa Antiga [VIT]",
                    TargetStat = CombatStatType.Vitality,
                    CorrespondingActivity = PetActivityState.TrainingVitality,
                    BaseProgressPerTick = 5.2f,
                    ClickBonusProgress = 4.0f,
                    HungerCostPerTick = 1.35f,
                    EnergyCostPerTick = 1.10f
                }
            };
        }
    }
}
