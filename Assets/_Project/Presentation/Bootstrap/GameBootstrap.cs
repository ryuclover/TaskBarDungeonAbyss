using System;
using TBDA.Data;
using TBDA.Domain.Session;
using TBDA.Platform;
using TBDA.Presentation.Audio;
using TBDA.Presentation.Intro;
using TBDA.Presentation.Save;
using TBDA.Presentation.TaskbarWorld;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TBDA.Presentation.Bootstrap
{
    /// <summary>
    /// Single Composition Root & Entry Point for Task Bar: Dungeon Abyss.
    /// Wires Domain, Data, Platform, Audio, Save, Intro, and Taskbar Views cleanly.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameDatabaseSO database;

        private UnityJsonSaveRepository _saveRepository;
        private GameSessionService _session;
        private Win32TaskbarOverlayController _windowController;
        private DSChiptuneSynthesizer _audio;
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
            var loadedSave = _saveRepository.LoadOrNull();

            var careConfigs = database != null ? database.ExtractCareConfigs() : null;
            var drillConfigs = database != null ? database.ExtractDrillConfigs() : null;

            _session = new GameSessionService(careConfigs, drillConfigs, loadedSave);
            _session.ApplyOfflineCatchup(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

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
            var introGo = new GameObject("IntroView");
            introGo.transform.SetParent(transform, false);
            _introView = introGo.AddComponent<StardewDSIntroView>();

            var taskbarGo = new GameObject("TaskbarDungeonView");
            taskbarGo.transform.SetParent(transform, false);
            _taskbarView = taskbarGo.AddComponent<TaskbarDungeonStripView>();

            _introView.Initialize(_session, database, _audio, OnIntroCompleted);
            _taskbarView.Initialize(_session, database, _audio, _windowController, HandleResetDemoRun);

            if (_session.State.HasCompletedIntro)
            {
                EnterTaskbarDungeonMode();
            }
            else
            {
                EnterIntroMode();
            }
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
                    _saveRepository.Save(_session.State);
                }
            }
        }

        private void OnIntroCompleted()
        {
            _saveRepository.Save(_session.State);
            EnterTaskbarDungeonMode();
        }

        private void EnterIntroMode()
        {
            _windowController.ApplyDisplayMode(WindowDisplayMode.FramedDualScreenIntro);
            _taskbarView.SetVisible(false);
            _introView.SetVisible(true);
        }

        private void EnterTaskbarDungeonMode()
        {
            _introView.SetVisible(false);
            _windowController.ApplyDisplayMode(WindowDisplayMode.TaskbarOverlay);
            _taskbarView.SetVisible(true);
            _taskbarView.RefreshAllUI();
        }

        private void HandleResetDemoRun()
        {
            _saveRepository.DeleteSave();
            _session.ResetNewDemoRun();
            Destroy(_introView.gameObject);

            var introGo = new GameObject("IntroView");
            introGo.transform.SetParent(transform, false);
            _introView = introGo.AddComponent<StardewDSIntroView>();
            _introView.Initialize(_session, database, _audio, OnIntroCompleted);

            EnterIntroMode();
        }

        private void OnApplicationQuit()
        {
            if (_session != null && _session.State.HasCompletedIntro)
                _saveRepository?.Save(_session.State);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && _session != null && _session.State.HasCompletedIntro)
                _saveRepository?.Save(_session.State);
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
