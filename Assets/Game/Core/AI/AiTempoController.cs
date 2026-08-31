using System;

namespace FortressFrontier.Core.AI
{
    public enum AiTempoState
    {
        Recovering = 0,
        Rallying = 1,
        PressureDue = 2
    }

    public readonly struct AiTempoConfig
    {
        public AiTempoConfig(
            int pressureMinIntervalTicks,
            int pressureTargetIntervalTicks,
            int pressureMaxIntervalTicks,
            int activeUnitSoftCap,
            int queuedUnitSoftCap)
        {
            PressureMinIntervalTicks = Math.Max(1, pressureMinIntervalTicks);
            PressureTargetIntervalTicks = Math.Max(PressureMinIntervalTicks, pressureTargetIntervalTicks);
            PressureMaxIntervalTicks = Math.Max(PressureTargetIntervalTicks, pressureMaxIntervalTicks);
            ActiveUnitSoftCap = Math.Max(1, activeUnitSoftCap);
            QueuedUnitSoftCap = Math.Max(1, queuedUnitSoftCap);
        }

        public int PressureMinIntervalTicks { get; }

        public int PressureTargetIntervalTicks { get; }

        public int PressureMaxIntervalTicks { get; }

        public int ActiveUnitSoftCap { get; }

        public int QueuedUnitSoftCap { get; }
    }

    public readonly struct AiTempoSignals
    {
        public AiTempoSignals(
            int elapsedSincePressureTicks,
            int pressureDueMilli,
            int recoveryNeededMilli,
            int overextensionMilli,
            AiTempoState state)
        {
            ElapsedSincePressureTicks = Math.Max(0, elapsedSincePressureTicks);
            PressureDueMilli = ClampMilli(pressureDueMilli);
            RecoveryNeededMilli = ClampMilli(recoveryNeededMilli);
            OverextensionMilli = ClampMilli(overextensionMilli);
            State = state;
        }

        public int ElapsedSincePressureTicks { get; }

        public int PressureDueMilli { get; }

        public int RecoveryNeededMilli { get; }

        public int OverextensionMilli { get; }

        public AiTempoState State { get; }

        private static int ClampMilli(int value)
        {
            return Math.Max(0, Math.Min(1000, value));
        }
    }

    public sealed class AiTempoController
    {
        public AiTempoSignals Evaluate(
            AiTempoConfig config,
            int currentTick,
            int lastSuccessfulPressureTick,
            int activeUnitCount,
            int queuedUnitCount)
        {
            int elapsed = Math.Max(0, currentTick - Math.Max(0, lastSuccessfulPressureTick));
            int pressureDue = InterpolateAfter(
                elapsed,
                config.PressureTargetIntervalTicks,
                config.PressureMaxIntervalTicks);

            int activeOverextension = InterpolateAfter(
                activeUnitCount,
                config.ActiveUnitSoftCap,
                config.ActiveUnitSoftCap + Math.Max(1, config.ActiveUnitSoftCap / 3));
            int queueOverextension = InterpolateAfter(
                queuedUnitCount,
                config.QueuedUnitSoftCap,
                config.QueuedUnitSoftCap + Math.Max(1, config.QueuedUnitSoftCap / 2));
            int overextension = Math.Max(activeOverextension, queueOverextension);

            int recoveryNeeded = overextension;
            if (elapsed < config.PressureMinIntervalTicks)
            {
                recoveryNeeded = Math.Max(
                    recoveryNeeded,
                    (config.PressureMinIntervalTicks - elapsed) * 1000 /
                    config.PressureMinIntervalTicks);
            }

            AiTempoState state = overextension > 0 ||
                                 lastSuccessfulPressureTick > 0 &&
                                 elapsed < config.PressureMinIntervalTicks
                ? AiTempoState.Recovering
                : pressureDue >= 500
                    ? AiTempoState.PressureDue
                    : AiTempoState.Rallying;

            return new AiTempoSignals(
                elapsed,
                pressureDue,
                recoveryNeeded,
                overextension,
                state);
        }

        public AiGateFailureReason GetOffensiveGateFailure(
            AiTempoConfig config,
            int currentTick,
            int lastSuccessfulPressureTick,
            int activeUnitCount,
            int queuedUnitCount,
            bool emergencyDefense)
        {
            if (emergencyDefense)
            {
                return AiGateFailureReason.None;
            }

            if (activeUnitCount >= config.ActiveUnitSoftCap ||
                queuedUnitCount >= config.QueuedUnitSoftCap)
            {
                return AiGateFailureReason.ArmyCap;
            }

            int elapsed = Math.Max(0, currentTick - Math.Max(0, lastSuccessfulPressureTick));
            return elapsed < config.PressureMinIntervalTicks
                ? AiGateFailureReason.PacingCooldown
                : AiGateFailureReason.None;
        }

        private static int InterpolateAfter(int value, int start, int end)
        {
            if (value <= start)
            {
                return 0;
            }

            if (value >= end || end <= start)
            {
                return 1000;
            }

            return (value - start) * 1000 / (end - start);
        }
    }
}
