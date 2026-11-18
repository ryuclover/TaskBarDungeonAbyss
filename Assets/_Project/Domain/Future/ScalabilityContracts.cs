using System;
using System.Collections.Generic;
using TBDA.Domain.Pets;

namespace TBDA.Domain.Future
{
    /// <summary>
    /// Built-in architectural contracts for post-Demo expansion:
    /// - Digimon-like branching evolutions (including the Labyrinth-Builder evolution)
    /// - Taskbar Hub Decoration slots
    /// - Unlockable Dungeon Merchant & Act 2: Abyss
    /// </summary>
    public enum WorldActId
    {
        Act1_Dungeon = 1,
        Act2_Abyss = 2
    }

    [Serializable]
    public class EvolutionBranchCondition
    {
        public string TargetFormId;
        public string DisplayName;
        public int MinStrength;
        public int MinAgility;
        public int MinVitality;
        public int MinAffection;
        public bool UnlocksLabyrinthBuilderMode; // Pet builds playable labyrinth to recruit new pets!
    }

    public interface IEvolutionEvaluator
    {
        bool CanEvolve(PetStateModel pet, EvolutionBranchCondition condition);
    }

    [Serializable]
    public class TaskbarDecorationPlacement
    {
        public int SlotIndex;
        public string DecorationItemId;
    }

    [Serializable]
    public class FutureProgressionFlags
    {
        public bool MerchantUnlocked = false;
        public bool AbyssActUnlocked = false;
        public List<TaskbarDecorationPlacement> PlacedDecorations = new List<TaskbarDecorationPlacement>();
    }
}
