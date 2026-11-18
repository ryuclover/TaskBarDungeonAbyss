using System.Collections.Generic;
using TBDA.Domain.Future;
using TBDA.Domain.Pets;
using UnityEngine;

namespace TBDA.Data
{
    [CreateAssetMenu(menuName = "TaskBar Dungeon Abyss/Pet Species", fileName = "PetSpeciesSO")]
    public class PetSpeciesSO : ScriptableObject
    {
        public PetSpeciesId SpeciesId;
        public string DisplayNamePtBr;
        [TextArea(2, 4)] public string DescriptionPtBr;
        public Sprite[] IdleSprites;
        public Sprite TrainSprite;
        public Sprite SleepSprite;

        [Header("Escalabilidade Futura (Árvore estilo Digimon)")]
        public List<EvolutionBranchCondition> FutureEvolutions = new List<EvolutionBranchCondition>();
    }

    [CreateAssetMenu(menuName = "TaskBar Dungeon Abyss/Care Item", fileName = "CareItemSO")]
    public class CareItemSO : ScriptableObject
    {
        public CareItemConfig Config = new CareItemConfig();
        public Sprite Icon;
    }

    [CreateAssetMenu(menuName = "TaskBar Dungeon Abyss/Training Drill", fileName = "TrainingDrillSO")]
    public class TrainingDrillSO : ScriptableObject
    {
        public TrainingDrillConfig Config = new TrainingDrillConfig();
        public Sprite StationSprite;
    }

    [CreateAssetMenu(menuName = "TaskBar Dungeon Abyss/Game Database", fileName = "GameDatabase")]
    public class GameDatabaseSO : ScriptableObject
    {
        [Header("Pets Iniciais")]
        public List<PetSpeciesSO> PetSpecies = new List<PetSpeciesSO>();

        [Header("Cuidado Tamagotchi")]
        public List<CareItemSO> CareItems = new List<CareItemSO>();

        [Header("Estações de Treino (Punch Club)")]
        public List<TrainingDrillSO> TrainingDrills = new List<TrainingDrillSO>();

        [Header("Arte da Barra da Masmorra (DS 256x192 / 32x32)")]
        public Sprite DungeonStripBg;
        public Sprite[] CampfireFrames;
        public Sprite PortalGateSprite;
        public Sprite HeartIcon;

        [Header("Ícones do Mini-Mapa (16x16)")]
        public Sprite NodeCampIcon;
        public Sprite NodeCombatIcon;
        public Sprite NodeTrainIcon;
        public Sprite NodeMysteryIcon;

        [Header("Cenas da Intro (Nintendo DS 256x192 Top Screen)")]
        public Sprite IntroAdoptSprite;
        public Sprite IntroGrowingSprite;
        public Sprite IntroFarewellSprite;
        public Sprite IntroPcEmailSprite;

        public PetSpeciesSO GetSpecies(PetSpeciesId id)
        {
            for (int i = 0; i < PetSpecies.Count; i++)
            {
                if (PetSpecies[i] != null && PetSpecies[i].SpeciesId == id)
                    return PetSpecies[i];
            }
            return PetSpecies.Count > 0 ? PetSpecies[0] : null;
        }

        public CareItemSO GetCareItem(string itemId)
        {
            for (int i = 0; i < CareItems.Count; i++)
            {
                if (CareItems[i] != null && CareItems[i].Config.ItemId == itemId)
                    return CareItems[i];
            }
            return null;
        }

        public List<CareItemConfig> ExtractCareConfigs()
        {
            var list = new List<CareItemConfig>();
            for (int i = 0; i < CareItems.Count; i++)
            {
                if (CareItems[i] != null) list.Add(CareItems[i].Config);
            }
            return list;
        }

        public List<TrainingDrillConfig> ExtractDrillConfigs()
        {
            var list = new List<TrainingDrillConfig>();
            for (int i = 0; i < TrainingDrills.Count; i++)
            {
                if (TrainingDrills[i] != null) list.Add(TrainingDrills[i].Config);
            }
            return list;
        }
    }
}
