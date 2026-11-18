using System.Collections.Generic;
using System.IO;
using TBDA.Data;
using TBDA.Domain.Future;
using TBDA.Domain.Pets;
using TBDA.Domain.Session;
using TBDA.Platform;
using TBDA.Presentation.Bootstrap;
using TBDA.Presentation.Save;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace TBDA.Editor
{
    /// <summary>
    /// Zero-friction setup wizard for Unity 6.3 LTS:
    /// Automatically configures DX11 BitBlt Swapchain (required for Windows Taskbar transparency),
    /// generates ScriptableObjects wired to all pixel art sprites, and creates the playable Demo scene!
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetupWizard
    {
        private const string DatabaseAssetPath = "Assets/_Project/Resources/Database/GameDatabase.asset";
        private const string DemoScenePath = "Assets/_Project/Scenes/01_TaskBarDungeonDemo.unity";

        static ProjectSetupWizard()
        {
            EditorApplication.delayCall += AutoInitializeIfNeeded;
        }

        private static void AutoInitializeIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<GameDatabaseSO>(DatabaseAssetPath) != null &&
                File.Exists(DemoScenePath))
            {
                return;
            }

            RunFullProjectSetup(showDialog: true);
        }

        [MenuItem("TaskBar Dungeon Abyss/1. Configurar Projeto e Gerar Cena Demo", priority = 1)]
        public static void MenuRunSetup()
        {
            RunFullProjectSetup(showDialog: true);
        }

        [MenuItem("TaskBar Dungeon Abyss/2. Apagar Todos os Saves (Resetar 3 Slots)", priority = 2)]
        public static void MenuClearSave()
        {
            var repo = new UnityJsonSaveRepository();
            repo.DeleteAllSlots();
            Debug.Log("[TBDA] Todos os saves locais foram apagados!");
            EditorUtility.DisplayDialog(
                "Task Bar: Dungeon Abyss",
                "Todos os 3 slots de save foram apagados com sucesso!\nAperte Play (▶) para abrir a Tela de Seleção de Arquivos limpa.",
                "OK");
        }

        public static void RunFullProjectSetup(bool showDialog)
        {
            ConfigureWindowsTaskbarPlayerSettings();

            EnsureFolder("Assets/_Project/Resources");
            EnsureFolder("Assets/_Project/Resources/Database");
            EnsureFolder("Assets/_Project/Scenes");

            // Ensure sprites are imported as Sprites before wiring database
            AssetDatabase.ImportAsset("Assets/_Project/Art", ImportAssetOptions.ImportRecursive);

            var db = CreateOrUpdateDatabaseAssets();
            CreateOrUpdateDemoScene(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[TBDA Setup] Projeto Unity 6 configurado com sucesso! Cena pronta em: " + DemoScenePath);

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Task Bar: Dungeon Abyss — Pronto!",
                    "Projeto configurado automaticamente para Unity 6!\n\n" +
                    "• Direct3D 11 + BitBlt Swapchain ativados (transparência real na Barra de Tarefas)\n" +
                    "• Sprites Pixel Art estilo Nintendo DS configurados\n" +
                    "• ScriptableObjects e Cena 01_TaskBarDungeonDemo gerados\n\n" +
                    "Basta apertar o botão PLAY (▶) no topo para jogar, ou ir em File -> Build And Run para ver na sua Barra de Tarefas do Windows!",
                    "Bora Jogar!");
            }
        }

        private static void ConfigureWindowsTaskbarPlayerSettings()
        {
            PlayerSettings.companyName = "DungeonAbyssStudio";
            PlayerSettings.productName = "Task Bar Dungeon Abyss";
            PlayerSettings.runInBackground = true;

            // CRITICAL for DwmExtendFrameIntoClientArea transparency on Windows 10/11!
            PlayerSettings.useFlipModelSwapchain = false;

            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 780;
            PlayerSettings.resizableWindow = false;

            // Force Direct3D11 on Windows x64 (D3D12 does not support DWM alpha window transparency)
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
        }

        private static GameDatabaseSO CreateOrUpdateDatabaseAssets()
        {
            const string folder = "Assets/_Project/Resources/Database";

            // 1. Pet Species ScriptableObjects
            var dog = CreateOrLoadSO<PetSpeciesSO>($"{folder}/Pet_Dog.asset");
            dog.SpeciesId = PetSpeciesId.Dog;
            dog.DisplayNamePtBr = "Cãozinho Leal";
            dog.DescriptionPtBr = "Cresceu ao seu lado e agora vigia sua Barra de Tarefas.";
            dog.IdleSprites = new[]
            {
                LoadSprite("Assets/_Project/Art/Pets/dog_idle_0.png"),
                LoadSprite("Assets/_Project/Art/Pets/dog_idle_1.png")
            };
            dog.TrainSprite = LoadSprite("Assets/_Project/Art/Pets/dog_train.png");
            dog.SleepSprite = LoadSprite("Assets/_Project/Art/Pets/dog_sleep.png");
            dog.FutureEvolutions = new List<EvolutionBranchCondition>
            {
                new EvolutionBranchCondition
                {
                    TargetFormId = "dog_guardian_hound",
                    DisplayName = "Cão Guardião de Pedra",
                    MinStrength = 10, MinVitality = 10, MinAffection = 70
                },
                new EvolutionBranchCondition
                {
                    TargetFormId = "dog_labyrinth_architect",
                    DisplayName = "Arquicão do Labirinto",
                    MinAgility = 10, MinVitality = 8, MinAffection = 85,
                    UnlocksLabyrinthBuilderMode = true
                }
            };
            EditorUtility.SetDirty(dog);

            var cat = CreateOrLoadSO<PetSpeciesSO>($"{folder}/Pet_Cat.asset");
            cat.SpeciesId = PetSpeciesId.Cat;
            cat.DisplayNamePtBr = "Gatinho das Sombras";
            cat.DescriptionPtBr = "Ágil e curioso, caça relíquias entre os tijolos da masmorra.";
            cat.IdleSprites = new[]
            {
                LoadSprite("Assets/_Project/Art/Pets/cat_idle_0.png"),
                LoadSprite("Assets/_Project/Art/Pets/cat_idle_1.png")
            };
            cat.TrainSprite = LoadSprite("Assets/_Project/Art/Pets/cat_train.png");
            cat.SleepSprite = LoadSprite("Assets/_Project/Art/Pets/cat_sleep.png");
            cat.FutureEvolutions = new List<EvolutionBranchCondition>
            {
                new EvolutionBranchCondition
                {
                    TargetFormId = "cat_abyss_lynx",
                    DisplayName = "Lince Arcano",
                    MinAgility = 12, MinStrength = 8, MinAffection = 70
                }
            };
            EditorUtility.SetDirty(cat);

            var golem = CreateOrLoadSO<PetSpeciesSO>($"{folder}/Pet_SpiritGolem.asset");
            golem.SpeciesId = PetSpeciesId.SpiritGolem;
            golem.DisplayNamePtBr = "Espírito Golem";
            golem.DescriptionPtBr = "Pequeno coração rúnico musgoso inspirado em Golem Hearts.";
            golem.IdleSprites = new[]
            {
                LoadSprite("Assets/_Project/Art/Pets/golem_idle_0.png"),
                LoadSprite("Assets/_Project/Art/Pets/golem_idle_1.png")
            };
            golem.TrainSprite = LoadSprite("Assets/_Project/Art/Pets/golem_train.png");
            golem.SleepSprite = LoadSprite("Assets/_Project/Art/Pets/golem_sleep.png");
            golem.FutureEvolutions = new List<EvolutionBranchCondition>
            {
                new EvolutionBranchCondition
                {
                    TargetFormId = "golem_dungeon_weaver",
                    DisplayName = "Forjador de Labirintos",
                    MinVitality = 12, MinStrength = 8, MinAffection = 80,
                    UnlocksLabyrinthBuilderMode = true
                }
            };
            EditorUtility.SetDirty(golem);

            // 2. Care Item ScriptableObjects
            var fallbackFoods = GameSessionService.BuildFallbackCareCatalog();
            string[] foodIcons =
            {
                "Assets/_Project/Art/Icons/icon_food_moss.png",
                "Assets/_Project/Art/Icons/icon_food_bread.png",
                "Assets/_Project/Art/Icons/icon_food_stew.png"
            };
            var careSOs = new List<CareItemSO>();
            for (int i = 0; i < fallbackFoods.Count; i++)
            {
                var cso = CreateOrLoadSO<CareItemSO>($"{folder}/Care_{fallbackFoods[i].ItemId}.asset");
                cso.Config = fallbackFoods[i];
                cso.Icon = LoadSprite(foodIcons[i]);
                EditorUtility.SetDirty(cso);
                careSOs.Add(cso);
            }

            // 3. Training Drill ScriptableObjects
            var fallbackDrills = GameSessionService.BuildFallbackDrills();
            string[] drillSprites =
            {
                "Assets/_Project/Art/Dungeon/prop_dummy_str.png",
                "Assets/_Project/Art/Dungeon/prop_target_agi.png",
                "Assets/_Project/Art/Dungeon/prop_rune_vit.png"
            };
            var drillSOs = new List<TrainingDrillSO>();
            for (int i = 0; i < fallbackDrills.Count; i++)
            {
                var dso = CreateOrLoadSO<TrainingDrillSO>($"{folder}/Drill_{fallbackDrills[i].DrillId}.asset");
                dso.Config = fallbackDrills[i];
                dso.StationSprite = LoadSprite(drillSprites[i]);
                EditorUtility.SetDirty(dso);
                drillSOs.Add(dso);
            }

            // 4. Master GameDatabaseSO
            var db = CreateOrLoadSO<GameDatabaseSO>(DatabaseAssetPath);
            db.PetSpecies = new List<PetSpeciesSO> { dog, cat, golem };
            db.CareItems = careSOs;
            db.TrainingDrills = drillSOs;

            db.DungeonStripBg = LoadSprite("Assets/_Project/Art/Dungeon/dungeon_strip_bg.png");
            db.CampfireFrames = new[]
            {
                LoadSprite("Assets/_Project/Art/Dungeon/prop_campfire_0.png"),
                LoadSprite("Assets/_Project/Art/Dungeon/prop_campfire_1.png")
            };
            db.PortalGateSprite = LoadSprite("Assets/_Project/Art/Dungeon/prop_portal.png");
            db.HeartIcon = LoadSprite("Assets/_Project/Art/Icons/icon_heart.png");

            db.NodeCampIcon = LoadSprite("Assets/_Project/Art/Icons/icon_node_camp.png");
            db.NodeCombatIcon = LoadSprite("Assets/_Project/Art/Icons/icon_node_combat.png");
            db.NodeTrainIcon = LoadSprite("Assets/_Project/Art/Icons/icon_node_train.png");
            db.NodeMysteryIcon = LoadSprite("Assets/_Project/Art/Icons/icon_node_mystery.png");

            db.IntroAdoptSprite = LoadSprite("Assets/_Project/Art/Intro/intro_01_adopt.png");
            db.IntroGrowingSprite = LoadSprite("Assets/_Project/Art/Intro/intro_02_growing.png");
            db.IntroFarewellSprite = LoadSprite("Assets/_Project/Art/Intro/intro_03_farewell.png");
            db.IntroPcEmailSprite = LoadSprite("Assets/_Project/Art/Intro/intro_04_pc_email.png");

            EditorUtility.SetDirty(db);
            return db;
        }

        private static void CreateOrUpdateDemoScene(GameDatabaseSO db)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Transparent Orthographic Camera
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);

            // UI EventSystem
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // Composition Root + Win32 Overlay Service
            var bootGo = new GameObject("GameBootstrap", typeof(Win32TaskbarOverlayController), typeof(GameBootstrap));
            var boot = bootGo.GetComponent<GameBootstrap>();
            var so = new SerializedObject(boot);
            so.FindProperty("database").objectReferenceValue = db;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, DemoScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(DemoScenePath, true)
            };
        }

        private static T CreateOrLoadSO<T>(string assetPath) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, assetPath);
            return created;
        }

        private static Sprite LoadSprite(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
