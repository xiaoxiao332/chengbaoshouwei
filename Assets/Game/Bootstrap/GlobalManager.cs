using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Saving;
using FortressFrontier.Core.StateMachine;
using FortressFrontier.Core.Systems;
using FortressFrontier.Infrastructure.Resources;
using FortressFrontier.Infrastructure.Saving;
using FortressFrontier.Infrastructure.Scenes;
using FortressFrontier.Infrastructure.Ads;
using FortressFrontier.Infrastructure.Audio;
using FortressFrontier.Presentation.UI;
using FortressFrontier.Presentation.Prototype;
using FortressFrontier.Runtime.Scenes;
using FortressFrontier.Runtime.Flow;
using FortressFrontier.Runtime.Prototype;
using FortressFrontier.Runtime.Content;
using FortressFrontier.Runtime.Progression;
using FortressFrontier.Runtime.Settings;
using FortressFrontier.Runtime.Monetization;
using FortressFrontier.Runtime.Audio;
using UnityEngine;
using UnityEngine.Serialization;

namespace FortressFrontier.Bootstrap
{
    public sealed class GlobalManager : MonoBehaviour, IApplicationFlow, IMatchSessionContext, IBootMenuCommands
    {
        [Header("Catalogs")]
        [SerializeField] private ResourceCatalog _resourceCatalog;
        [SerializeField] private PanelCatalog _panelCatalog;
        [SerializeField] private UIRootView _uiRootView;
        [SerializeField] private string _gameContentConfigId = "config.game-content";

        [Header("Stable scene ids")]
        [FormerlySerializedAs("_mainMenuSceneId")]
        [SerializeField] private string _selectionSceneId = "scene.selection";
        [SerializeField] private string _gameplaySceneId = "scene.gameplay";
        [FormerlySerializedAs("_initialState")]
        [SerializeField] private AppStateId _startButtonState = AppStateId.Selection;

        [Header("TapADN rewarded ads (disabled until all values are set)")]
        [SerializeField] private long _tapAdMediaId;
        [SerializeField] private string _tapAdMediaKey = string.Empty;
        [SerializeField] private long _tapAdRewardSpaceId;
        [SerializeField] private string _privacyPolicyUrl = string.Empty;

        private readonly CancellationTokenSource _lifetimeCancellation = new();
        private readonly SystemHost _systemHost = new();
        private readonly AsyncStateMachine<AppStateId> _appStateMachine = new();
        private GameContext _context;
        private SceneFlowSystem _sceneFlow;
        private UIManager _uiManager;
        private SaveCoordinator _saveCoordinator;
        private ContentConfigSystem _contentConfigSystem;
        private ProgressionSystem _progressionSystem;
        private ApplicationSettingsSystem _applicationSettingsSystem;
        private SettingsVisualSystem _settingsVisualSystem;
        private RewardedAdSystem _rewardedAdSystem;
        private AudioPlaybackSystem _audioPlaybackSystem;
        private BootPanel _bootPanel;
        private MatchLaunchRequest? _pendingMatch;
        private MatchLaunchRequest? _committedMatch;
        private MatchConfigSnapshot _pendingMatchSnapshot;
        private MatchConfigSnapshot _committedMatchSnapshot;
        private Task _initializationTask;
        private bool _initializing;

        public bool IsInitialized { get; private set; }
        public MatchLaunchRequest? CurrentMatch => _pendingMatch ?? _committedMatch;
        public MatchConfigSnapshot CurrentMatchSnapshot => _pendingMatchSnapshot ?? _committedMatchSnapshot;

        private void Start()
        {
#if UNITY_EDITOR
            Application.runInBackground = true;
#endif
            _initializationTask = InitializeAfterStartupAsync();
        }

        private async Task InitializeAfterStartupAsync()
        {
            try
            {
                await Task.Yield();
                await InitializeAsync(_lifetimeCancellation.Token);
            }
            catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            if (IsInitialized || _initializing)
            {
                return;
            }

            ValidateConfiguration();
            _initializing = true;
            try
            {
                _context = new GameContext(Application.version);
                var resourceSystem = new AddressablesResourceSystem(_resourceCatalog);
                _contentConfigSystem = new ContentConfigSystem(
                    resourceSystem,
                    new ResourceKey(_gameContentConfigId));
                _saveCoordinator = new SaveCoordinator(
                    Application.persistentDataPath,
                    Application.version,
                    GetSaveParticipants);
                _progressionSystem = new ProgressionSystem(
                    _contentConfigSystem,
                    token => _saveCoordinator.SaveAsync(SaveFileKind.Profile, token));
                _uiManager = new UIManager(resourceSystem, _panelCatalog, _uiRootView);
                _audioPlaybackSystem = new AudioPlaybackSystem(resourceSystem, transform);
                _applicationSettingsSystem = new ApplicationSettingsSystem(
                    token => _saveCoordinator.SaveAsync(SaveFileKind.Settings, token),
                    _audioPlaybackSystem.ApplyVolumes);
                _settingsVisualSystem = new SettingsVisualSystem(_uiManager, _applicationSettingsSystem,
                    _applicationSettingsSystem);
                var rewardedAdConfiguration = new RewardedAdConfiguration(
                    _tapAdMediaId, _tapAdMediaKey, _tapAdRewardSpaceId, _privacyPolicyUrl);
                _rewardedAdSystem = new RewardedAdSystem(rewardedAdConfiguration,
                    new DirichletRewardedAdService(rewardedAdConfiguration),
                    _applicationSettingsSystem, _applicationSettingsSystem, _progressionSystem);
                var sceneService = new AddressableSceneService(_resourceCatalog);
                _sceneFlow = new SceneFlowSystem(
                    sceneService,
                    new SceneSystemDependencies(resourceSystem, _uiManager, this,
                        _contentConfigSystem, _progressionSystem, _progressionSystem, this, _progressionSystem,
                        _contentConfigSystem, _settingsVisualSystem, _rewardedAdSystem, _audioPlaybackSystem));

                _systemHost.Register(resourceSystem);
                _systemHost.Register(_contentConfigSystem);
                _systemHost.Register(_progressionSystem);
                _systemHost.Register(_uiManager);
                _systemHost.Register(_audioPlaybackSystem);
                _systemHost.Register(_applicationSettingsSystem);
                _systemHost.Register(_settingsVisualSystem);
                _systemHost.Register(_rewardedAdSystem);
                _systemHost.Register(_sceneFlow);

                RegisterAppStates(_uiManager);
                await _systemHost.InitializeAsync(_context, SystemLifetime.Global, cancellationToken);
                await _audioPlaybackSystem.SetMusicAsync(GameAudioKeys.Boot, 0f, cancellationToken);
                await _appStateMachine.TransitionAsync(AppStateId.Boot, cancellationToken);
                _bootPanel = await _uiManager.OpenAsync<BootPanel>(new PanelKey("ui.boot"), null, cancellationToken);
                _bootPanel.Bind(this);
                await resourceSystem.PreloadAsync(_resourceCatalog.GetPreloadResourceKeys(), cancellationToken);
                await _saveCoordinator.LoadAllAsync(cancellationToken);
                IsInitialized = true;
                _bootPanel.SetReady();
            }
            finally
            {
                _initializing = false;
            }
        }

        public async Task ChangeStateAsync(AppStateId state, CancellationToken cancellationToken)
        {
            EnsureInitialized();
            await _appStateMachine.TransitionAsync(state, cancellationToken);
            if (state == AppStateId.Selection)
                await _audioPlaybackSystem.SetMusicAsync(GameAudioKeys.Selection, 0.75f, cancellationToken);
        }

        public Task StartGameAsync(CancellationToken cancellationToken)
        {
            EnsureInitialized();
            return TransitionWithFeedbackAsync(_startButtonState, cancellationToken);
        }

        public Task OpenSettingsAsync(CancellationToken cancellationToken)
        {
            EnsureInitialized();
            return _settingsVisualSystem.OpenSettingsAsync(cancellationToken);
        }

        public async Task StartMatchAsync(MatchLaunchRequest request, CancellationToken cancellationToken)
        {
            EnsureInitialized();
            var previousMatch = _committedMatch;
            var previousSnapshot = _committedMatchSnapshot;
            _pendingMatch = request;
            _pendingMatchSnapshot = _contentConfigSystem.CreateMatchSnapshot(request.BattlefieldId, request.MapModeId, request.Seed);
            try
            {
                await TransitionWithFeedbackAsync(AppStateId.Gameplay, cancellationToken);
                _committedMatch = _pendingMatch;
                _committedMatchSnapshot = _pendingMatchSnapshot;
            }
            catch
            {
                _committedMatch = previousMatch;
                _committedMatchSnapshot = previousSnapshot;
                throw;
            }
            finally
            {
                _pendingMatch = null;
                _pendingMatchSnapshot = null;
            }
        }

        public async Task ReturnToSelectionAsync(CancellationToken cancellationToken)
        {
            EnsureInitialized();
            await TransitionWithFeedbackAsync(AppStateId.Selection, cancellationToken);
            _committedMatch = null;
            _committedMatchSnapshot = null;
        }

        public Task SaveAllAsync(CancellationToken cancellationToken)
        {
            EnsureInitialized();
            return _saveCoordinator.SaveAllAsync(cancellationToken);
        }

        internal void Tick(float deltaTime)
        {
            if (IsInitialized)
            {
                _systemHost.Tick(deltaTime);
            }
        }

        private async void OnApplicationPause(bool isPaused)
        {
            if (!IsInitialized)
            {
                return;
            }

            try
            {
                await _systemHost.NotifyApplicationPauseAsync(isPaused, _lifetimeCancellation.Token);
                if (isPaused)
                {
                    await _saveCoordinator.SaveAllAsync(_lifetimeCancellation.Token);
                }
            }
            catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private async void OnDestroy()
        {
            _lifetimeCancellation.Cancel();
            try
            {
                if (_initializationTask != null)
                {
                    await _initializationTask;
                }
                await _systemHost.ShutdownAsync(SystemLifetime.Global, CancellationToken.None);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                _lifetimeCancellation.Dispose();
                IsInitialized = false;
            }
        }

        private IEnumerable<ISaveParticipant> GetSaveParticipants()
        {
            return _systemHost.Systems.OfType<ISaveParticipant>()
                .Concat(_sceneFlow?.ActiveSceneSystems.OfType<ISaveParticipant>()
                    ?? Enumerable.Empty<ISaveParticipant>());
        }

        private void RegisterAppStates(UIManager uiManager)
        {
            _appStateMachine.Register(new AppCompositionState(
                AppStateId.Boot, _sceneFlow, uiManager, null, UIStateId.Boot, ResolveUiState));
            _appStateMachine.Register(new AppCompositionState(
                AppStateId.Selection,
                _sceneFlow,
                uiManager,
                new SceneKey(_selectionSceneId),
                UIStateId.Selection,
                ResolveUiState));
            _appStateMachine.Register(new AppCompositionState(
                AppStateId.Gameplay,
                _sceneFlow,
                uiManager,
                new SceneKey(_gameplaySceneId),
                UIStateId.Gameplay,
                ResolveUiState));
            _appStateMachine.Register(new AppCompositionState(
                AppStateId.FatalError, _sceneFlow, uiManager, null, UIStateId.FatalError, ResolveUiState));
        }

        private async Task TransitionWithFeedbackAsync(AppStateId state, CancellationToken cancellationToken)
        {
            var loadingId = new PanelKey("ui.loading");
            try
            {
                await _uiManager.OpenAsync<LoadingOverlayPanel>(loadingId, null, cancellationToken);
                await _appStateMachine.TransitionAsync(state, cancellationToken);
                if (state == AppStateId.Selection)
                    await _audioPlaybackSystem.SetMusicAsync(GameAudioKeys.Selection, 0.75f, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception) when (_lifetimeCancellation.IsCancellationRequested)
            {
                // The persistent composition root is being destroyed. Do not try to
                // present an error through UI objects that Unity is unloading.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                await _uiManager.OpenAsync<FatalErrorPanel>(new PanelKey("ui.fatal-error"), new FatalErrorPanelArguments(exception.Message), CancellationToken.None);
                throw;
            }
            finally
            {
                if (!_lifetimeCancellation.IsCancellationRequested)
                {
                    await _uiManager.CloseAsync(loadingId, CancellationToken.None);
                }
            }
        }

        private static UIStateId? ResolveUiState(AppStateId state) => state switch
        {
            AppStateId.Boot => UIStateId.Boot,
            AppStateId.Selection => UIStateId.Selection,
            AppStateId.Gameplay => UIStateId.Gameplay,
            AppStateId.FatalError => UIStateId.FatalError,
            _ => null
        };

        private void ValidateConfiguration()
        {
            if (_resourceCatalog == null) throw new InvalidOperationException("ResourceCatalog is not assigned.");
            if (_panelCatalog == null) throw new InvalidOperationException("PanelCatalog is not assigned.");
            if (_uiRootView == null) throw new InvalidOperationException("UIRootView is not assigned.");
            _ = new SceneKey(_selectionSceneId);
            _ = new SceneKey(_gameplaySceneId);
            _ = new ResourceKey(_gameContentConfigId);
            if (_startButtonState != AppStateId.Selection)
                throw new InvalidOperationException("Boot start button must enter Selection.");
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("GlobalManager is not initialized.");
            }
        }
    }
}
