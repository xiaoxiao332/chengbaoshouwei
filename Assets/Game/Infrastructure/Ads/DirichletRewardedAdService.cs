using System;
using System.Threading;
using System.Threading.Tasks;
using Dirichlet.Mediation;
using FortressFrontier.Runtime.Monetization;
using UnityEngine;

namespace FortressFrontier.Infrastructure.Ads
{
    public sealed class DirichletRewardedAdService : IRewardedAdService
    {
        private readonly RewardedAdConfiguration _configuration;
        private readonly SemaphoreSlim _singleFlight = new(1, 1);
        private Task<bool> _initialization;

        public DirichletRewardedAdService(RewardedAdConfiguration configuration) =>
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        public bool IsAvailable
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return _configuration.IsComplete;
#else
                return false;
#endif
            }
        }

        public async Task<RewardedAdPlaybackResult> ShowAsync(string matchId, int rewardAmount,
            CancellationToken cancellationToken)
        {
            if (!IsAvailable) return new RewardedAdPlaybackResult(RewardedAdPlaybackStatus.Unavailable);
            await _singleFlight.WaitAsync(cancellationToken);
            try
            {
                if (!await EnsureInitializedAsync(cancellationToken))
                    return new RewardedAdPlaybackResult(RewardedAdPlaybackStatus.Failed, "广告服务初始化失败，请稍后重试。");

                var completion = new TaskCompletionSource<RewardedAdPlaybackResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                var listener = new Listener(completion);
                using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
                var request = new DirichletAdRequest.Builder()
                    .WithSpaceId(_configuration.RewardSpaceId)
                    .WithUserId(matchId)
                    .WithRewardName("金币")
                    .WithRewardAmount(Math.Max(1, rewardAmount))
                    .Build();
                DirichletAdManager.CreateAdNative().ShowRewardVideoAutoAd(request, listener);
                var completed = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(45), cancellationToken));
                if (completed == completion.Task) return await completion.Task;
                listener.Abort();
                cancellationToken.ThrowIfCancellationRequested();
                return new RewardedAdPlaybackResult(RewardedAdPlaybackStatus.Failed, "广告响应超时，请稍后重试。");
            }
            finally
            {
                _singleFlight.Release();
            }
        }

        private async Task<bool> EnsureInitializedAsync(CancellationToken cancellationToken)
        {
            _initialization ??= InitializeAsync();
            using var registration = cancellationToken.Register(() => { });
            var initialized = await _initialization;
            if (!initialized) _initialization = null;
            return initialized;
        }

        private Task<bool> InitializeAsync()
        {
            if (DirichletSdk.IsInitialized) return Task.FromResult(true);
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var config = new DirichletAdConfig.Builder()
                .WithMediaId(_configuration.MediaId)
                .WithMediaName("城垒争锋")
                .WithMediaKey(_configuration.MediaKey)
                .EnableDebug(Debug.isDebugBuild)
                .ShakeEnabled(false)
                .Build();
            DirichletSdk.Init(config, _ => completion.TrySetResult(true), error =>
            {
                Debug.LogWarning($"TapADN initialization failed ({error.Code}): {error.Message}");
                completion.TrySetResult(false);
            });
            return completion.Task;
        }

        private sealed class Listener : IDirichletRewardVideoAutoAdListener
        {
            private readonly TaskCompletionSource<RewardedAdPlaybackResult> _completion;
            private bool _verified;
            private bool _previousAudioPause;
            private bool _audioPauseCaptured;
            public Listener(TaskCompletionSource<RewardedAdPlaybackResult> completion) => _completion = completion;
            public void OnError(DirichletError error)
            {
                RestoreAudio();
                _completion.TrySetResult(new RewardedAdPlaybackResult(
                    RewardedAdPlaybackStatus.Failed, $"广告加载失败（{error.Code}），请稍后重试。"));
            }
            public void OnAdShow()
            {
                _previousAudioPause = AudioListener.pause;
                _audioPauseCaptured = true;
                AudioListener.pause = true;
            }
            public void OnAdClose()
            {
                RestoreAudio();
                _completion.TrySetResult(new RewardedAdPlaybackResult(_verified
                    ? RewardedAdPlaybackStatus.Verified
                    : RewardedAdPlaybackStatus.ClosedWithoutReward));
            }
            public void OnRewardVerify(DirichletRewardVerificationEventArgs args) => _verified |= args?.IsVerified == true;
            public void OnAdClick() { }
            public void Abort() => RestoreAudio();
            private void RestoreAudio()
            {
                if (!_audioPauseCaptured) return;
                AudioListener.pause = _previousAudioPause;
                _audioPauseCaptured = false;
            }
        }
    }
}
