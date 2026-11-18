using System;
using TBDA.Data;
using TBDA.Domain.Session;
using TBDA.Platform;
using TBDA.Presentation.Audio;
using TBDA.Presentation.Intro;
using TBDA.Presentation.Save;
using TBDA.Presentation.TaskbarWorld;
using TBDA.Presentation.Title;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TBDA.Presentation.Bootstrap
{
    /// <summary>
    /// Single Composition Root & Entry Point for Task Bar: Dungeon Abyss.
    /// Wires Domain, Data, Platform, Audio, Multi-Slot Save, Title Screen, Intro, and Taskbar Views.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameDatabaseSO database;

        private UnityJsonSaveRepository _saveRepository;
        private GameSessionService _session;
        private Win32TaskbarOverlayController _windowController;
        private DSChiptuneSynthesizer _audio;

        private TitleScreenSaveSelectView _titleView;
        private StardewDSIntroView _introView;
        private TaskbarDungeonStripView _taskbarView;

        private float _autoSaveTimer;
        private const float AutoSaveIntervalSeconds = 8f;

        private void Start()
        {
            EnsureCameraAndEventSystem();

            if (database == null)
            {
                database = Resources.Load<GameDatabaseSO>("Database/GameDatabase");
            }

            _saveRepository = new UnityJsonSaveRepository();

            // Platform Window Service
            _windowController = GetComponent<Win32TaskbarOverlayController>();
            if (_windowController == null)
                _windowController = gameObject.AddComponent<Win32TaskbarOverlayController>();
            _windowController.SetCamera(Camera.main);

            // Chiptune DS Audio
            if (GetComponent<AudioSource>() == null)
                gameObject.AddComponent<AudioSource>();
            _audio = gameObject.AddComponent<DSChiptuneSynthesizer>();

            // Views
            var titleGo = new GameObject("TitleSaveSelectView");
            titleGo.transform.SetParent(transform, false);
            _titleView = titleGo.AddComponent<TitleScreenSaveSelectView>();

            var introGo = new GameObject("IntroView");
            introGo.transform.SetParent(transform, false);
            _introView = introGo.AddComponent<StardewDSIntroView>();

            var taskbarGo = new GameObject("TaskbarDungeonView");
            taskbarGo.transform.SetParent(transform, false);
            _taskbarView = taskbarGo.AddComponent<TaskbarDungeonStripView>();

            // Initialize Views
            _titleView.Initialize(_saveRepository, database, _audio, OnSlotChosen);
            _introView.Initialize(null, database, _audio, OnIntroCompleted);
            _taskbarView.Initialize(null, database, _audio, _windowController, ReturnToTitleScreen);

            // Start on Single-Screen Title / Multi-Save Selection
            ReturnToTitleScreen();
        }

        private void Update()
        {
            if (_session == null) return;

            _session.TickSimulation(Time.deltaTime);

            if (_session.State.HasCompletedIntro)
            {
                _autoSaveTimer += Time.unscaledDeltaTime;
                if (_autoSaveTimer >= AutoSaveIntervalSeconds)
                {
                    _autoSaveTimer = 0f;
                    _saveRepository.SaveSlot(_saveRepository.ActiveSlotIndex, _session.State);
                }
            }
        }

        private void OnSlotChosen(int slotIndex, bool isNewGame)
        {
            _saveRepository.ActiveSlotIndex = slotIndex;

            var careConfigs = database != null ? database.ExtractCareConfigs() : null;
            var drillConfigs = database != null ? database.ExtractDrillConfigs() : null;

            if (isNewGame)
            {
                _session = new GameSessionService(careConfigs, drillConfigs, null);
                _introView.BindSession(_session, OnIntroCompleted);
                _taskbarView.BindSession(_session);

                _titleView.SetVisible(false);
                EnterIntroMode();
            }
            else
            {
                var loadedSave = _saveRepository.LoadSlotOrNull(slotIndex);
                _session = new GameSessionService(careConfigs, drillConfigs, loadedSave);
                _session.ApplyOfflineCatchup(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                _introView.BindSession(_session, OnIntroCompleted);
                _taskbarView.BindSession(_session);

                _titleView.SetVisible(false);

                if (_session.State.HasCompletedIntro)
                {
                    EnterTaskbarDungeonMode();
                }
                else
                {
                    EnterIntroMode();
                }
            }
        }

        private void OnIntroCompleted()
        {
            _saveRepository.SaveSlot(_saveRepository.ActiveSlotIndex, _session.State);
            EnterTaskbarDungeonMode();
        }

        private void EnterIntroMode()
        {
            _windowController.ApplyDisplayMode(WindowDisplayMode.FramedDualScreenIntro);
            _titleView.SetVisible(false);
            _taskbarView.SetVisible(false);
            _introView.SetVisible(true);
        }

        private void EnterTaskbarDungeonMode()
        {
            _titleView.SetVisible(false);
            _introView.SetVisible(false);
            _windowController.ApplyDisplayMode(WindowDisplayMode.TaskbarOverlay);
            _taskbarView.SetVisible(true);
            _taskbarView.RefreshAllUI();
        }

        public void ReturnToTitleScreen()
        {
            if (_session != null && _session.State.HasCompletedIntro)
            {
                _saveRepository.SaveSlot(_saveRepository.ActiveSlotIndex, _session.State);
            }

            _introView.SetVisible(false);
            _taskbarView.SetVisible(false);
            _windowController.ApplyDisplayMode(WindowDisplayMode.FramedDualScreenIntro);

            _titleView.SetVisible(true);
            _titleView.RefreshAllSlots();
        }

        private void OnApplicationQuit()
        {
            if (_session != null && _session.State.HasCompletedIntro)
                _saveRepository?.SaveSlot(_saveRepository.ActiveSlotIndex, _session.State);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && _session != null && _session.State.HasCompletedIntro)
                _saveRepository?.SaveSlot(_saveRepository.ActiveSlotIndex, _session.State);
        }

        private static void EnsureCameraAndEventSystem()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                camGo.tag = "MainCamera";
                cam = camGo.GetComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);

            if (EventSystem.current == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }
        }
    }
}
