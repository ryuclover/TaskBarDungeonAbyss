using System;
using System.Collections.Generic;
using TBDA.Domain.Events;
using TBDA.Domain.Pets;
using TBDA.Domain.Training;

namespace TBDA.Domain.DungeonMap
{
    public enum DungeonNodeType
    {
        CampEntrance = 0,
        CombatSparring = 1,   // Tests STR/AGI/VIT, rewards shards + training XP
        ForageMoss = 2,       // Gathers Tamagotchi dungeon rations
        AncientTrainingTotem = 3, // Heavy burst of combat stat XP
        UnstableMystery = 4,  // Mutates over time (Dungeon Odyssey style!)
        BossGateTrial = 5,    // Advances Dungeon Depth
        LockedMerchant = 6    // Teaser hook for full game unlockable Merchant!
    }

    /// <summary>
    /// Flat serializable node state (avoids circular object refs per Slay-the-Spire Unity best practices).
    /// </summary>
    [Serializable]
    public class DungeonNodeState
    {
        public int NodeId;
        public int Column;          // 0..4 (fits 256x192 DS Top Screen cleanly!)
        public int Row;             // 0..2
        public DungeonNodeType NodeType;
        public bool IsUnstable;     // Mutates periodically
        public bool Cleared;
        public int RecommendedPower;
        public List<int> NextNodeIds = new List<int>();
    }

    [Serializable]
    public class DungeonMapState
    {
        public int ActNumber = 1;           // 1 = Dungeon, 2 = Abyss (future)
        public int DepthFloor = 1;
        public int Seed = 1337;
        public int CurrentNodeId = 0;
        public int ActiveExpeditionNodeId = -1;
        public float ExpeditionElapsed = 0f;
        public float ExpeditionDuration = 6.5f;
        public float MutationTimer = 0f;
        public string LastExpeditionLog = "Escolha uma sala conectada no Mini-Mapa para treinar em campo!";
        public List<DungeonNodeState> Nodes = new List<DungeonNodeState>();
    }

    /// <summary>
    /// Generates and mutates the Nintendo DS-styled 5x3 node grid for Act 1: Dungeon.
    /// Integrates directly into the training loop (collects food + tests Punch Club stats).
    /// </summary>
    public sealed class DungeonMiniMapSystem
    {
        private readonly DomainEventBus _bus;
        private readonly PunchClubTrainingSystem _training;
        private const float MutationIntervalSeconds = 28f;

        public DungeonMiniMapSystem(DomainEventBus bus, PunchClubTrainingSystem training)
        {
            _bus = bus;
            _training = training;
        }

        public void GenerateFloorMap(DungeonMapState map, int depthFloor, int seed)
        {
            map.DepthFloor = depthFloor;
            map.Seed = seed;
            map.Nodes.Clear();
            map.ActiveExpeditionNodeId = -1;
            map.ExpeditionElapsed = 0f;

            var rng = new Random(seed + depthFloor * 7919);
            int basePower = 5 + (depthFloor - 1) * 4;
            int nextId = 0;

            // Column 0: Dungeon Camp Entrance (Row 1 center)
            var startNode = new DungeonNodeState
            {
                NodeId = nextId++,
                Column = 0,
                Row = 1,
                NodeType = DungeonNodeType.CampEntrance,
                IsUnstable = false,
                Cleared = true,
                RecommendedPower = basePower
            };
            map.Nodes.Add(startNode);
            map.CurrentNodeId = startNode.NodeId;

            // Columns 1..3: 3 rows each (9 rooms)
            int[,] gridIds = new int[5, 3];
            gridIds[0, 1] = startNode.NodeId;

            for (int col = 1; col <= 3; col++)
            {
                for (int row = 0; row < 3; row++)
                {
                    bool unstable = rng.NextDouble() < 0.45;
                    DungeonNodeType nType = RollRoomType(rng, col, row, depthFloor);

                    var node = new DungeonNodeState
                    {
                        NodeId = nextId++,
                        Column = col,
                        Row = row,
                        NodeType = nType,
                        IsUnstable = unstable && nType != DungeonNodeType.LockedMerchant,
                        Cleared = false,
                        RecommendedPower = basePower + col * 2 + (row == 1 ? 1 : 0)
                    };
                    gridIds[col, row] = node.NodeId;
                    map.Nodes.Add(node);
                }
            }

            // Column 4: Floor Guardian / Depth Trial (Row 1 center)
            var bossNode = new DungeonNodeState
            {
                NodeId = nextId++,
                Column = 4,
                Row = 1,
                NodeType = DungeonNodeType.BossGateTrial,
                IsUnstable = false,
                Cleared = false,
                RecommendedPower = basePower + 9
            };
            gridIds[4, 1] = bossNode.NodeId;
            map.Nodes.Add(bossNode);

            // Connect Column 0 -> Column 1
            for (int r = 0; r < 3; r++)
                startNode.NextNodeIds.Add(gridIds[1, r]);

            // Connect Columns 1 -> 2 -> 3 (adjacent rows so paths never cross messily)
            for (int col = 1; col <= 2; col++)
            {
                for (int r = 0; r < 3; r++)
                {
                    var from = GetNodeById(map, gridIds[col, r]);
                    from.NextNodeIds.Add(gridIds[col + 1, r]);
                    if (r > 0 && rng.NextDouble() < 0.65)
                        from.NextNodeIds.Add(gridIds[col + 1, r - 1]);
                    if (r < 2 && rng.NextDouble() < 0.65)
                        from.NextNodeIds.Add(gridIds[col + 1, r + 1]);
                }
            }

            // Connect Column 3 -> Column 4 Boss Trial
            for (int r = 0; r < 3; r++)
            {
                var from = GetNodeById(map, gridIds[3, r]);
                from.NextNodeIds.Add(bossNode.NodeId);
            }

            map.LastExpeditionLog = $"Andar {depthFloor} da Masmorra mapeado! Salas com '~' sofrem mutação.";
        }

        public bool IsNodeSelectable(DungeonMapState map, int targetNodeId)
        {
            if (map.ActiveExpeditionNodeId >= 0) return false;
            var current = GetNodeById(map, map.CurrentNodeId);
            var target = GetNodeById(map, targetNodeId);
            if (current == null || target == null) return false;
            if (target.NodeType == DungeonNodeType.LockedMerchant) return false;
            return current.NextNodeIds.Contains(targetNodeId);
        }

        public bool TryStartExpedition(DungeonMapState map, PetStateModel pet, int targetNodeId)
        {
            if (!IsNodeSelectable(map, targetNodeId)) return false;

            if (pet.Vitals.Energy < 15f || pet.Vitals.Hunger < 12f)
            {
                _bus.Publish(new FloatingFeedbackEvent
                {
                    Message = "Energia/Fome baixa para explorar o mapa!",
                    Tone = FeedbackTone.Warning
                });
                return false;
            }

            map.ActiveExpeditionNodeId = targetNodeId;
            map.ExpeditionElapsed = 0f;
            // Higher Agility slightly speeds up field expeditions!
            map.ExpeditionDuration = Math.Max(3.5f, 6.8f - pet.Stats.Agility * 0.22f);
            pet.Activity = PetActivityState.ExploringDungeonNode;

            var target = GetNodeById(map, targetNodeId);
            map.LastExpeditionLog = $"{pet.PetName} entrou na sala [{GetNodeLabel(target.NodeType)}]...";
            _bus.Publish(new PetStateUpdatedEvent { Reason = "ExpeditionStarted" });
            return true;
        }

        public void TickMap(
            DungeonMapState map,
            PetStateModel pet,
            List<InventoryEntry> inventory,
            ref int soulShards,
            float deltaSeconds)
        {
            // 1. Periodic Dungeon Odyssey node mutation on uncleared unstable rooms
            map.MutationTimer += deltaSeconds;
            if (map.MutationTimer >= MutationIntervalSeconds)
            {
                map.MutationTimer = 0f;
                MutateUnstableNodes(map);
            }

            // 2. Active Expedition progress
            if (map.ActiveExpeditionNodeId < 0) return;

            map.ExpeditionElapsed += deltaSeconds;
            pet.Vitals.Energy -= 1.2f * deltaSeconds;
            pet.Vitals.Hunger -= 0.9f * deltaSeconds;
            pet.Vitals.ClampAll();

            if (map.ExpeditionElapsed >= map.ExpeditionDuration)
            {
                ResolveExpedition(map, pet, inventory, ref soulShards);
            }
        }

        private void MutateUnstableNodes(DungeonMapState map)
        {
            var rng = new Random(Environment.TickCount ^ map.Seed);
            int mutated = 0;
            for (int i = 0; i < map.Nodes.Count; i++)
            {
                var n = map.Nodes[i];
                if (!n.IsUnstable || n.Cleared || n.NodeId == map.ActiveExpeditionNodeId || n.NodeId == map.CurrentNodeId)
                    continue;

                DungeonNodeType[] pool =
                {
                    DungeonNodeType.CombatSparring,
                    DungeonNodeType.ForageMoss,
                    DungeonNodeType.AncientTrainingTotem,
                    DungeonNodeType.UnstableMystery
                };
                var nextType = pool[rng.Next(pool.Length)];
                if (nextType != n.NodeType)
                {
                    n.NodeType = nextType;
                    mutated++;
                }
            }

            if (mutated > 0)
            {
                _bus.Publish(new MapMutatedEvent { MutatedNodesCount = mutated });
            }
        }

        private void ResolveExpedition(
            DungeonMapState map,
            PetStateModel pet,
            List<InventoryEntry> inventory,
            ref int soulShards)
        {
            var node = GetNodeById(map, map.ActiveExpeditionNodeId);
            map.ActiveExpeditionNodeId = -1;
            map.ExpeditionElapsed = 0f;
            pet.Activity = PetActivityState.Idle;

            if (node == null) return;

            int petPower = pet.Stats.TotalPowerScore + (int)(pet.Vitals.Affection * 0.05f);
            bool success = true;
            int shards = 0;
            string rewardItem = null;
            string summary;

            switch (node.NodeType)
            {
                case DungeonNodeType.ForageMoss:
                    rewardItem = (map.DepthFloor >= 2) ? "food_bread" : "food_moss";
                    AddInventoryItem(inventory, rewardItem, 2);
                    _training.AddStatProgress(pet, CombatStatType.Vitality, 22f);
                    shards = 2;
                    summary = $"{pet.PetName} colheu rações frescas na masmorra (+2 comidas, +VIT)!";
                    break;

                case DungeonNodeType.AncientTrainingTotem:
                    _training.AddStatProgress(pet, CombatStatType.Strength, 32f);
                    _training.AddStatProgress(pet, CombatStatType.Agility, 32f);
                    shards = 3;
                    summary = $"{pet.PetName} treinou no Totem Antigo (+FOR, +AGI)!";
                    break;

                case DungeonNodeType.UnstableMystery:
                    rewardItem = "food_stew";
                    AddInventoryItem(inventory, rewardItem, 1);
                    _training.AddStatProgress(pet, CombatStatType.Agility, 28f);
                    pet.Vitals.Affection = Math.Min(100f, pet.Vitals.Affection + 8f);
                    shards = 5;
                    summary = $"Evento Mutável! {pet.PetName} achou Sopa Brilhante e 5 Fragmentos!";
                    break;

                case DungeonNodeType.CombatSparring:
                case DungeonNodeType.BossGateTrial:
                default:
                    success = petPower >= (node.RecommendedPower - 2);
                    if (success)
                    {
                        bool isBoss = node.NodeType == DungeonNodeType.BossGateTrial;
                        shards = isBoss ? 10 : 4;
                        rewardItem = isBoss ? "food_stew" : "food_bread";
                        AddInventoryItem(inventory, rewardItem, 1);
                        _training.AddStatProgress(pet, CombatStatType.Strength, isBoss ? 45f : 26f);
                        _training.AddStatProgress(pet, CombatStatType.Vitality, isBoss ? 45f : 24f);
                        summary = isBoss
                            ? $"VITÓRIA NO GUARDIÃO! {pet.PetName} avançou para o Andar {map.DepthFloor + 1}!"
                            : $"{pet.PetName} venceu a escaramuça de treino (+{shards} Frag., +1 Pão)!";
                    }
                    else
                    {
                        pet.Vitals.Health = Math.Max(20f, pet.Vitals.Health - 15f);
                        _training.AddStatProgress(pet, CombatStatType.Vitality, 18f); // Learns from defeat!
                        pet.Activity = PetActivityState.RestingAtCampfire;
                        summary = $"Inimigo forte (Poder rec. {node.RecommendedPower}). {pet.PetName} recuou para a fogueira (+VIT)!";
                    }
                    break;
            }

            soulShards += shards;
            pet.ExpeditionsCompleted++;

            if (success)
            {
                node.Cleared = true;
                map.CurrentNodeId = node.NodeId;

                if (node.NodeType == DungeonNodeType.BossGateTrial)
                {
                    GenerateFloorMap(map, map.DepthFloor + 1, map.Seed + 17);
                }
            }

            map.LastExpeditionLog = summary;
            _bus.Publish(new ExpeditionCompletedEvent
            {
                Victory = success,
                SummaryLog = summary,
                ShardsEarned = shards,
                ItemRewardId = rewardItem
            });
            _bus.Publish(new FloatingFeedbackEvent
            {
                Message = success ? "Expedição Concluída!" : "Recuou para treinar mais!",
                Tone = success ? FeedbackTone.Positive : FeedbackTone.Warning
            });
            _bus.Publish(new PetStateUpdatedEvent { Reason = "ExpeditionResolved" });
        }

        public static DungeonNodeState GetNodeById(DungeonMapState map, int nodeId)
        {
            for (int i = 0; i < map.Nodes.Count; i++)
            {
                if (map.Nodes[i].NodeId == nodeId) return map.Nodes[i];
            }
            return null;
        }

        public static string GetNodeLabel(DungeonNodeType type)
        {
            switch (type)
            {
                case DungeonNodeType.CampEntrance: return "Fogueira";
                case DungeonNodeType.CombatSparring: return "Emboscada";
                case DungeonNodeType.ForageMoss: return "Coleta";
                case DungeonNodeType.AncientTrainingTotem: return "Totem";
                case DungeonNodeType.UnstableMystery: return "Mutável ?";
                case DungeonNodeType.BossGateTrial: return "Guardião";
                case DungeonNodeType.LockedMerchant: return "Mercador (Em Breve)";
                default: return "Sala";
            }
        }

        private static DungeonNodeType RollRoomType(Random rng, int col, int row, int depth)
        {
            // Occasional locked Merchant teaser in col 2 row 0 on depth >= 2
            if (depth >= 2 && col == 2 && row == 0 && rng.NextDouble() < 0.35)
                return DungeonNodeType.LockedMerchant;

            double roll = rng.NextDouble();
            if (roll < 0.34) return DungeonNodeType.CombatSparring;
            if (roll < 0.62) return DungeonNodeType.ForageMoss;
            if (roll < 0.84) return DungeonNodeType.AncientTrainingTotem;
            return DungeonNodeType.UnstableMystery;
        }

        private static void AddInventoryItem(List<InventoryEntry> inv, string itemId, int qty)
        {
            for (int i = 0; i < inv.Count; i++)
            {
                if (inv[i].ItemId == itemId)
                {
                    inv[i].Quantity += qty;
                    return;
                }
            }
            inv.Add(new InventoryEntry { ItemId = itemId, Quantity = qty });
        }
    }
}
