using System;

namespace FortressFrontier.Core.Saving
{
    public enum SaveFileKind
    {
        Settings,
        Profile,
        Run
    }

    public interface ISaveParticipant
    {
        SaveFileKind FileKind { get; }
        string SectionKey { get; }
        int SectionVersion { get; }
        Type StateType { get; }
        object CaptureState();
        object CreateDefaultState();
        void RestoreState(object state, int storedVersion);
    }

    public readonly struct SaveLoadResult
    {
        public SaveLoadResult(bool succeeded, bool usedBackup, string errorMessage)
        {
            Succeeded = succeeded;
            UsedBackup = usedBackup;
            ErrorMessage = errorMessage;
        }

        public bool Succeeded { get; }
        public bool UsedBackup { get; }
        public string ErrorMessage { get; }

        public static SaveLoadResult Success(bool usedBackup = false) => new(true, usedBackup, string.Empty);
        public static SaveLoadResult Failure(string message) => new(false, false, message ?? string.Empty);
    }
}
