using System;
using System.Collections.Generic;
using UnityEngine;

namespace FortressFrontier.Runtime.Scenes
{
    /// <summary>Scene-only bridge for authored P1 world anchors. It owns no gameplay rules.</summary>
    public sealed class GameplayWorldContext : MonoBehaviour
    {
        [SerializeField] private Transform _playerGate;
        [SerializeField] private Transform _enemyGate;
        [SerializeField] private Transform _playerWall;
        [SerializeField] private Transform _enemyWall;
        [SerializeField] private Transform _playerDeployment;
        [SerializeField] private Transform _enemyDeployment;
        [SerializeField] private Transform _upperRoute;
        [SerializeField] private Transform _middleRoute;
        [SerializeField] private Transform _lowerRoute;
        [SerializeField] private Transform[] _resourcePoints = Array.Empty<Transform>();
        [SerializeField] private Transform[] _bossPoints = Array.Empty<Transform>();
        [SerializeField] private Transform _towerBuildArea;
        [SerializeField] private Transform[] _towerForbiddenAreas = Array.Empty<Transform>();
        [SerializeField] private Transform _worldUnitsRoot;
        [SerializeField] private Transform _worldConstructionRoot;
        [SerializeField] private Transform _worldEffectsRoot;
        [SerializeField] private RectTransform _worldUnitsOverlay;
        [SerializeField] private RectTransform _worldConstructionOverlay;
        [SerializeField] private RectTransform _worldEffectsOverlay;

        public bool IsInitialized { get; private set; }
        public Transform PlayerGate => _playerGate;
        public Transform EnemyGate => _enemyGate;
        public Transform PlayerWall => _playerWall;
        public Transform EnemyWall => _enemyWall;
        public Transform PlayerDeployment => _playerDeployment;
        public Transform EnemyDeployment => _enemyDeployment;
        public Transform UpperRoute => _upperRoute;
        public Transform MiddleRoute => _middleRoute;
        public Transform LowerRoute => _lowerRoute;
        public IReadOnlyList<Transform> ResourcePoints => _resourcePoints;
        public IReadOnlyList<Transform> BossPoints => _bossPoints;
        public Transform TowerBuildArea => _towerBuildArea;
        public IReadOnlyList<Transform> TowerForbiddenAreas => _towerForbiddenAreas;
        public Transform WorldUnitsRoot => _worldUnitsRoot;
        public Transform WorldConstructionRoot => _worldConstructionRoot;
        public Transform WorldEffectsRoot => _worldEffectsRoot;
        public RectTransform WorldUnitsOverlay => _worldUnitsOverlay;
        public RectTransform WorldConstructionOverlay => _worldConstructionOverlay;
        public RectTransform WorldEffectsOverlay => _worldEffectsOverlay;

        public void Initialize()
        {
            if (IsInitialized) return;
            if (!TryValidate(out var reason)) throw new InvalidOperationException(reason);
            IsInitialized = true;
        }

        public void Shutdown()
        {
            if (!IsInitialized) return;
            IsInitialized = false;
        }

        public bool TryValidate(out string reason)
        {
            if (_playerGate == null || _enemyGate == null || _playerWall == null || _enemyWall == null ||
                _playerDeployment == null || _enemyDeployment == null || _upperRoute == null || _middleRoute == null ||
                _lowerRoute == null || _towerBuildArea == null || _worldUnitsRoot == null ||
                _worldConstructionRoot == null || _worldEffectsRoot == null || _worldUnitsOverlay == null ||
                _worldConstructionOverlay == null || _worldEffectsOverlay == null)
            {
                reason = "GameplayWorldContext has a missing required anchor.";
                return false;
            }
            if (_resourcePoints == null || _resourcePoints.Length != 9 || Array.Exists(_resourcePoints, value => value == null) ||
                _bossPoints == null || _bossPoints.Length != 2 || Array.Exists(_bossPoints, value => value == null) ||
                _towerForbiddenAreas == null || _towerForbiddenAreas.Length == 0 || Array.Exists(_towerForbiddenAreas, value => value == null))
            {
                reason = "GameplayWorldContext requires nine resources, two Boss points and at least one tower-forbidden area.";
                return false;
            }
            reason = string.Empty;
            return true;
        }
    }
}
