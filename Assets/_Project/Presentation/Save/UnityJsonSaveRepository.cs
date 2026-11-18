using System;
using System.IO;
using TBDA.Domain.Pets;
using TBDA.Domain.Session;
using UnityEngine;

namespace TBDA.Presentation.Save
{
    [Serializable]
    public struct SaveSlotSummary
    {
        public int SlotIndex;
        public bool Exists;
        public bool HasCompletedIntro;
        public string PetName;
        public PetSpeciesId Species;
        public int Strength;
        public int Agility;
        public int Vitality;
        public int DepthFloor;
        public int SoulShards;
        public long LastSavedUnixSeconds;

        public string FormattedLastSaved
        {
            get
            {
                if (LastSavedUnixSeconds <= 0) return "Novo";
                try
                {
                    var dt = DateTimeOffset.FromUnixTimeSeconds(LastSavedUnixSeconds).LocalDateTime;
                    return dt.ToString("dd/MM HH:mm");
                }
                catch
                {
                    return "-";
                }
            }
        }
    }

    public sealed class UnityJsonSaveRepository
    {
        public const int MaxSlots = 3;
        private const string PrefActiveSlot = "TBDA_ActiveSaveSlot";
        private const string LegacySaveFileName = "tbda_demo_save_v1.json";

        public int ActiveSlotIndex
        {
            get => PlayerPrefs.GetInt(PrefActiveSlot, 1);
            set
            {
                int clamped = Mathf.Clamp(value, 1, MaxSlots);
                PlayerPrefs.SetInt(PrefActiveSlot, clamped);
                PlayerPrefs.Save();
            }
        }

        public UnityJsonSaveRepository()
        {
            MigrateLegacySaveIfNeeded();
        }

        public string GetSlotFilePath(int slotIndex)
        {
            int s = Mathf.Clamp(slotIndex, 1, MaxSlots);
            return Path.Combine(Application.persistentDataPath, $"tbda_save_slot_{s}.json");
        }

        public bool SlotExists(int slotIndex)
        {
            return File.Exists(GetSlotFilePath(slotIndex));
        }

        public SaveSlotSummary GetSlotSummary(int slotIndex)
        {
            var summary = new SaveSlotSummary
            {
                SlotIndex = slotIndex,
                Exists = false,
                HasCompletedIntro = false,
                PetName = "Vazio",
                Species = PetSpeciesId.Dog,
                Strength = 0,
                Agility = 0,
                Vitality = 0,
                DepthFloor = 1,
                SoulShards = 0,
                LastSavedUnixSeconds = 0
            };

            var data = LoadSlotOrNull(slotIndex);
            if (data == null) return summary;

            summary.Exists = true;
            summary.HasCompletedIntro = data.HasCompletedIntro;
            summary.PetName = string.IsNullOrWhiteSpace(data.ActivePet?.PetName) ? "Sem Nome" : data.ActivePet.PetName;
            summary.Species = data.ActivePet != null ? data.ActivePet.Species : PetSpeciesId.Dog;
            summary.Strength = data.ActivePet?.Stats?.Strength ?? 0;
            summary.Agility = data.ActivePet?.Stats?.Agility ?? 0;
            summary.Vitality = data.ActivePet?.Stats?.Vitality ?? 0;
            summary.DepthFloor = data.DungeonMap != null ? data.DungeonMap.DepthFloor : 1;
            summary.SoulShards = data.SoulShards;
            summary.LastSavedUnixSeconds = data.LastSavedUnixSeconds;

            return summary;
        }

        public GameSaveData LoadSlotOrNull(int slotIndex)
        {
            string path = GetSlotFilePath(slotIndex);
            try
            {
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonUtility.FromJson<GameSaveData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TBDA Save] Falha ao carregar slot {slotIndex}: {ex.Message}");
                return null;
            }
        }

        public void SaveSlot(int slotIndex, GameSaveData data)
        {
            if (data == null) return;
            string path = GetSlotFilePath(slotIndex);
            try
            {
                data.LastSavedUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TBDA Save] Falha ao salvar slot {slotIndex}: {ex.Message}");
            }
        }

        public void DeleteSlot(int slotIndex)
        {
            string path = GetSlotFilePath(slotIndex);
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TBDA Save] Erro ao apagar slot {slotIndex}: {ex.Message}");
            }
        }

        // Active slot helpers
        public GameSaveData LoadOrNull() => LoadSlotOrNull(ActiveSlotIndex);
        public void Save(GameSaveData data) => SaveSlot(ActiveSlotIndex, data);
        public void DeleteSave() => DeleteSlot(ActiveSlotIndex);

        public void DeleteAllSlots()
        {
            for (int i = 1; i <= MaxSlots; i++)
            {
                DeleteSlot(i);
            }
            string legacy = Path.Combine(Application.persistentDataPath, LegacySaveFileName);
            if (File.Exists(legacy))
            {
                try { File.Delete(legacy); } catch { }
            }
        }

        private void MigrateLegacySaveIfNeeded()
        {
            try
            {
                string legacyPath = Path.Combine(Application.persistentDataPath, LegacySaveFileName);
                string slot1Path = GetSlotFilePath(1);
                if (File.Exists(legacyPath) && !File.Exists(slot1Path))
                {
                    File.Copy(legacyPath, slot1Path);
                    Debug.Log("[TBDA Save] Save legado v1 migrado para o Slot 1 com sucesso!");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TBDA Save] Aviso durante migração de save legado: {ex.Message}");
            }
        }
    }
}
