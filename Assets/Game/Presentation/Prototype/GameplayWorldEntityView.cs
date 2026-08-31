using FortressFrontier.Runtime.Resources;
using UnityEngine;
using UnityEngine.UI;

namespace FortressFrontier.Presentation.Prototype
{
    public enum WorldEntityMotionState
    {
        Static,
        Idle,
        Moving,
        Gathering,
        Projectile,
        Preview
    }

    public sealed class GameplayWorldEntityView : MonoBehaviour, IPoolable
    {
        private const float AttackSeconds = 0.18f;
        private const float HitWhiteSeconds = 0.06f;
        private const float HitRecoverySeconds = 0.10f;
        private const float IdleBreathPeriodSeconds = 2.4f;
        private const float MovingBreathPeriodSeconds = 1.2f;
        private const float MovingBobPeriodSeconds = 0.6f;
        private const float GatheringBreathPeriodSeconds = 1.6f;
        private const float GatheringSwingPeriodSeconds = 0.72f;

        [SerializeField] private RectTransform _visualPivot;
        [SerializeField] private Image _icon;
        [SerializeField] private Text _label;

        private RectTransform _root;
        private RectTransform _projectileOrigin;
        private bool _projectileOriginResolved;
        private Vector2 _targetPosition;
        private Color _baseTint = Color.white;
        private WorldEntityMotionState _motionState;
        private bool _positionInitialized;
        private bool _heavyAttack;
        private int _facingDirection = 1;
        private float _visualTime;
        private float _attackElapsed = AttackSeconds;
        private float _hitElapsed = HitWhiteSeconds + HitRecoverySeconds;
        private float _baseScale = 1f;
        private float _visualHeight = 1f;
        private float _movementRotation;

        public RectTransform VisualPivot => _visualPivot;
        public int FacingDirection => _facingDirection;
        public WorldEntityMotionState MotionState => _motionState;
        public Color CurrentTint => _icon != null ? _icon.color : Color.white;

        public bool TryGetProjectileOrigin(RectTransform targetParent, out Vector2 position)
        {
            EnsureInitialized();
            position = default;
            if (_root == null || _projectileOrigin == null || targetParent == null) return false;

            var localPosition = (Vector3)_projectileOrigin.anchoredPosition;
            localPosition.x *= _facingDirection;
            var pointInTargetParent = (Vector2)targetParent.InverseTransformPoint(_root.TransformPoint(localPosition));
            position = pointInTargetParent - targetParent.rect.min;
            return true;
        }

        public void Render(int x, int y, string label, float scale = 1f) => Render(x, y, label, scale, Color.white);

        public void Render(int x, int y, string label, float scale, Color tint)
        {
            Present(x, y, label, scale, tint, WorldEntityMotionState.Static, 0, false, false, false, false);
        }

        public void Present(int x, int y, string label, float scale, Color tint,
            WorldEntityMotionState motionState, int facingDirection, bool attackTriggered,
            bool damageTriggered, bool heavyAttack, bool smoothPosition, float movementRotation = 0f,
            Vector2? initialPosition = null)
        {
            EnsureInitialized();
            _targetPosition = new Vector2(x, y);
            _baseScale = scale;
            _baseTint = tint;
            var wasProjectile = _motionState == WorldEntityMotionState.Projectile;
            _motionState = motionState;
            _heavyAttack = heavyAttack;
            if (motionState == WorldEntityMotionState.Projectile && (!_positionInitialized || !wasProjectile))
                _movementRotation = movementRotation;
            if (facingDirection != 0) _facingDirection = facingDirection > 0 ? 1 : -1;
            if (!_positionInitialized || !smoothPosition)
            {
                _root.anchoredPosition = smoothPosition && initialPosition.HasValue
                    ? initialPosition.Value : _targetPosition;
                _positionInitialized = true;
            }
            if (attackTriggered) _attackElapsed = 0f;
            if (damageTriggered) _hitElapsed = 0f;
            if (_label != null) _label.text = label ?? string.Empty;
            ApplyVisual();
        }

        public void TickVisual(float deltaTime, bool paused)
        {
            EnsureInitialized();
            if (paused || deltaTime <= 0f) return;

            _visualTime += deltaTime;
            _attackElapsed = Mathf.Min(AttackSeconds, _attackElapsed + deltaTime);
            _hitElapsed = Mathf.Min(HitWhiteSeconds + HitRecoverySeconds, _hitElapsed + deltaTime);
            if (_positionInitialized)
            {
                var previousPosition = _root.anchoredPosition;
                var blend = 1f - Mathf.Exp(-deltaTime * 24f);
                _root.anchoredPosition = Vector2.Lerp(_root.anchoredPosition, _targetPosition, blend);
                var movement = _root.anchoredPosition - previousPosition;
                if (_motionState == WorldEntityMotionState.Projectile && movement.sqrMagnitude > 0.0001f)
                    _movementRotation = Mathf.Atan2(movement.y, movement.x) * Mathf.Rad2Deg;
            }
            ApplyVisual();
        }

        public void OnRent()
        {
            EnsureInitialized();
            ResetVisualState();
            gameObject.SetActive(true);
        }

        public void OnReturn()
        {
            EnsureInitialized();
            ResetVisualState();
            if (_label != null) _label.text = string.Empty;
            gameObject.SetActive(false);
        }

        private void EnsureInitialized()
        {
            if (_root == null) _root = transform as RectTransform;
            if (!_projectileOriginResolved)
            {
                _projectileOrigin = transform.Find("point") as RectTransform;
                _projectileOriginResolved = true;
            }
            if (_visualPivot == null && _icon != null) _visualPivot = _icon.rectTransform;
            if (_visualPivot != null)
            {
                var height = _visualPivot.rect.height;
                if (height <= 0f && _root != null) height = _root.rect.height;
                _visualHeight = Mathf.Max(1f, height);
            }
        }

        private void ApplyVisual()
        {
            if (_root != null) _root.localScale = Vector3.one * _baseScale;
            if (_visualPivot == null) return;

            var scalePulse = 1f;
            var verticalOffset = 0f;
            var rotation = 0f;
            switch (_motionState)
            {
                case WorldEntityMotionState.Idle:
                    scalePulse = 1f + Oscillation(IdleBreathPeriodSeconds) * 0.01f;
                    break;
                case WorldEntityMotionState.Moving:
                    scalePulse = 1f + Oscillation(MovingBreathPeriodSeconds) * 0.02f;
                    verticalOffset = Mathf.Abs(Oscillation(MovingBobPeriodSeconds)) * _visualHeight * 0.03f;
                    break;
                case WorldEntityMotionState.Gathering:
                    scalePulse = 1f + Oscillation(GatheringBreathPeriodSeconds) * 0.01f;
                    rotation = -_facingDirection * Oscillation(GatheringSwingPeriodSeconds) * 7f;
                    break;
                case WorldEntityMotionState.Projectile:
                    rotation = _movementRotation;
                    break;
            }

            if (_attackElapsed < AttackSeconds)
            {
                var normalized = _attackElapsed / AttackSeconds;
                var amplitude = _heavyAttack ? 12f : 8f;
                rotation += -_facingDirection * Mathf.Sin(normalized * Mathf.PI) * amplitude;
            }

            if (_visualPivot != _root)
            {
                _visualPivot.anchoredPosition = new Vector2(0f, verticalOffset);
                _visualPivot.localRotation = Quaternion.Euler(0f, 0f, rotation);
                var horizontalScale = _motionState == WorldEntityMotionState.Projectile ? scalePulse : _facingDirection * scalePulse;
                _visualPivot.localScale = new Vector3(horizontalScale, scalePulse, 1f);
            }
            else if (_motionState == WorldEntityMotionState.Projectile)
                _root.localRotation = Quaternion.Euler(0f, 0f, rotation);

            if (_icon == null) return;
            if (_hitElapsed < HitWhiteSeconds)
            {
                _icon.color = new Color(1f, 1f, 1f, _baseTint.a * 0.5f);
            }
            else if (_hitElapsed < HitWhiteSeconds + HitRecoverySeconds)
            {
                var recovery = (_hitElapsed - HitWhiteSeconds) / HitRecoverySeconds;
                var impact = new Color(1f, 0.38f, 0.26f, _baseTint.a * 0.65f);
                _icon.color = Color.Lerp(impact, _baseTint, recovery);
            }
            else
            {
                _icon.color = _baseTint;
            }
        }

        private float Oscillation(float periodSeconds) =>
            Mathf.Sin(_visualTime * Mathf.PI * 2f / Mathf.Max(0.01f, periodSeconds));

        private void ResetVisualState()
        {
            _targetPosition = Vector2.zero;
            _baseTint = Color.white;
            _motionState = WorldEntityMotionState.Static;
            _positionInitialized = false;
            _heavyAttack = false;
            _facingDirection = 1;
            _visualTime = 0f;
            _attackElapsed = AttackSeconds;
            _hitElapsed = HitWhiteSeconds + HitRecoverySeconds;
            _baseScale = 1f;
            _movementRotation = 0f;
            if (_root != null)
            {
                _root.anchoredPosition = Vector2.zero;
                _root.localScale = Vector3.one;
                _root.localRotation = Quaternion.identity;
            }
            if (_visualPivot != null)
            {
                _visualPivot.anchoredPosition = Vector2.zero;
                _visualPivot.localRotation = Quaternion.identity;
                _visualPivot.localScale = Vector3.one;
            }
            if (_icon != null) _icon.color = Color.white;
        }
    }
}
