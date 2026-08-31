using System;

namespace FortressFrontier.Runtime.Audio
{
    public sealed class SfxAdmissionBudget
    {
        private readonly int _capacity;
        private readonly float _tokensPerSecond;
        private readonly float _burstTokens;
        private readonly int _frameStartLimit;
        private float _tokens;
        private int _frame = int.MinValue;
        private int _startsThisFrame;

        public SfxAdmissionBudget(int capacity, float tokensPerSecond, float burstTokens, int frameStartLimit)
        {
            _capacity = Math.Max(1, capacity);
            _tokensPerSecond = Math.Max(0f, tokensPerSecond);
            _burstTokens = Math.Max(1f, burstTokens);
            _frameStartLimit = Math.Max(1, frameStartLimit);
            _tokens = _burstTokens;
        }

        public int Capacity => _capacity;
        public float AvailableTokens => _tokens;

        public void Tick(float deltaTime) =>
            _tokens = Math.Min(_burstTokens, _tokens + Math.Max(0f, deltaTime) * _tokensPerSecond);

        public bool TryAdmit(int activeCount, int frame)
        {
            if (_frame != frame)
            {
                _frame = frame;
                _startsThisFrame = 0;
            }
            if (activeCount >= _capacity || _startsThisFrame >= _frameStartLimit || _tokens < 1f) return false;
            _tokens -= 1f;
            _startsThisFrame++;
            return true;
        }
    }
}
