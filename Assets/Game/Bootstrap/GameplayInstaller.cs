using System.Collections.Generic;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Prototype;
using FortressFrontier.Runtime.Scenes;
using FortressFrontier.Runtime.Gameplay;
using FortressFrontier.Presentation.Prototype;
using FortressFrontier.Presentation.Audio;
using System;
using System.Linq;
using UnityEngine;

namespace FortressFrontier.Bootstrap
{
    public sealed class GameplayInstaller : SceneSystemInstallerBase
    {
        [SerializeField] private GameplayWorldContext _worldContext;

        public override IEnumerable<GameSystemBase> CreateSystems(GameContext context, SceneSystemDependencies dependencies)
        {
            if (_worldContext == null)
                throw new InvalidOperationException("GameplayWorldContext is missing.");
            if (!_worldContext.TryValidate(out var worldReason))
                throw new InvalidOperationException(worldReason);
            var match = dependencies.ApplicationFlow.CurrentMatch
                ?? throw new InvalidOperationException("Gameplay cannot initialize without a pending or committed match.");
            var snapshot = dependencies.MatchSession.CurrentMatchSnapshot
                ?? throw new InvalidOperationException("Gameplay cannot initialize without an immutable match snapshot.");
            var runtime = MatchRuntimeFactory.Create(snapshot);
            var settlement = new MatchSettlementSystem(snapshot, match.MatchId, dependencies.Settlement, runtime.Simulation);
            var sprites = new GameplaySpriteAssetSystem(dependencies.Resources, snapshot.Presentation,
                snapshot.Research.Upgrades.Select(value => value.PresentationKey).Concat(new[]
                {
                    snapshot.HandAndOffers.BuildingRewardArt,
                    snapshot.HandAndOffers.ResourceRewardArt,
                    snapshot.HandAndOffers.ReinforcementRewardArt
                }));

            foreach (var system in runtime.Systems) yield return system;
            yield return settlement;
            yield return sprites;
            yield return new GameplayAudioSystem(dependencies.Audio ??
                    throw new InvalidOperationException("Gameplay audio service is missing."), snapshot,
                runtime.Phases, runtime.Boss, runtime.Combat, runtime.PlayerGatherers, runtime.EnemyGatherers);
            yield return new GameplayWorldPresentationSystem(dependencies.Resources, _worldContext,
                runtime.ResourceNodes, runtime.PlayerGatherers, runtime.EnemyGatherers, runtime.Training, runtime.EnemyTraining, runtime.Combat,
                runtime.PlayerConstruction, runtime.EnemyConstruction, runtime.Boss, runtime.Simulation, snapshot.Presentation);
            yield return new GameplayVisualSystem(dependencies.Panels, dependencies.ApplicationFlow,
                runtime.Economy, runtime.Buildings, runtime.Camps, runtime.Training, runtime.Hand,
                runtime.ResourceNodes, runtime.PlayerGatherers, runtime.EnemyGatherers, runtime.Combat,
                runtime.Simulation, runtime.EnemyEconomy, runtime.AiStrategy,
                runtime.PlayerConstruction, runtime.EnemyConstruction, runtime.PlayerResearch, runtime.Boss,
                runtime.Analytics, settlement, snapshot, snapshot.Presentation, sprites, dependencies.RewardedAds);
        }
    }
}
