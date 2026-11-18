using System;
using System.IO;
using TBDA.Domain.Session;
using UnityEngine;

namespace TBDA.Presentation.Save
{
    public sealed class UnityJsonSaveRepository
    {
        private readonly string _saveFilePath;

        public UnityJsonSaveRepository(string fileName = "tbda_demo_save_v1.json")
        {
            _saveFilePath = Path.Combine(Application.persistentDataPath, fileName);
        }

        public string SaveFilePath => _saveFilePath;

        public GameSaveData LoadOrNull()
        {
            try
            {
                if (!File.Exists(_saveFilePath)) return null;
                string json = File.ReadAllText(_saveFilePath);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonUtility.FromJson<GameSaveData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TBDA Save] Falha ao carregar save, iniciando novo: {ex.Message}");
                return null;
            }
        }

        public void Save(GameSaveData data)
        {
            if (data == null) return;
            try
            {
                data.LastSavedUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(_saveFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TBDA Save] Falha ao salvar progresso: {ex.Message}");
            }
        }

        public void DeleteSave()
        {
            try
            {
                if (File.Exists(_saveFilePath))
                    File.Delete(_saveFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TBDA Save] Erro ao apagar save: {ex.Message}");
            }
        }
    }
}
