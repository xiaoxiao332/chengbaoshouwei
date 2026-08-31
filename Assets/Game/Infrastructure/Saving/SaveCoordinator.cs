using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Saving;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FortressFrontier.Infrastructure.Saving
{
    public sealed class SaveCoordinator
    {
        private sealed class SaveEnvelope
        {
            [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; } = 1;
            [JsonProperty("gameVersion")] public string GameVersion { get; set; }
            [JsonProperty("savedAtUtc")] public DateTimeOffset SavedAtUtc { get; set; }
            [JsonProperty("sections")] public Dictionary<string, SaveSection> Sections { get; set; } = new();
        }

        private sealed class SaveSection
        {
            [JsonProperty("version")] public int Version { get; set; }
            [JsonProperty("data")] public JToken Data { get; set; }
        }

        private readonly string _directoryPath;
        private readonly string _gameVersion;
        private readonly Func<IEnumerable<ISaveParticipant>> _participantsProvider;
        private readonly JsonSerializer _serializer = JsonSerializer.CreateDefault();
        private readonly SemaphoreSlim _ioGate = new(1, 1);

        public SaveCoordinator(
            string directoryPath,
            string gameVersion,
            Func<IEnumerable<ISaveParticipant>> participantsProvider)
        {
            _directoryPath = string.IsNullOrWhiteSpace(directoryPath)
                ? throw new ArgumentException("Save directory cannot be empty.", nameof(directoryPath))
                : directoryPath;
            _gameVersion = gameVersion ?? "0.0.0";
            _participantsProvider = participantsProvider ?? throw new ArgumentNullException(nameof(participantsProvider));
        }

        public async Task SaveAllAsync(CancellationToken cancellationToken)
        {
            foreach (SaveFileKind kind in Enum.GetValues(typeof(SaveFileKind)))
            {
                await SaveAsync(kind, cancellationToken);
            }
        }

        public async Task<IReadOnlyList<SaveLoadResult>> LoadAllAsync(CancellationToken cancellationToken)
        {
            var results = new List<SaveLoadResult>();
            foreach (SaveFileKind kind in Enum.GetValues(typeof(SaveFileKind)))
            {
                results.Add(await LoadAsync(kind, cancellationToken));
            }

            return results;
        }

        public async Task SaveAsync(SaveFileKind kind, CancellationToken cancellationToken)
        {
            await _ioGate.WaitAsync(cancellationToken);
            try
            {
                var envelope = new SaveEnvelope
                {
                    GameVersion = _gameVersion,
                    SavedAtUtc = DateTimeOffset.UtcNow
                };

                foreach (var participant in GetParticipants(kind))
                {
                    if (envelope.Sections.ContainsKey(participant.SectionKey))
                    {
                        throw new InvalidOperationException($"Duplicate save section: '{participant.SectionKey}'.");
                    }

                    var state = participant.CaptureState();
                    envelope.Sections.Add(participant.SectionKey, new SaveSection
                    {
                        Version = participant.SectionVersion,
                        Data = state == null
                            ? JValue.CreateNull()
                            : JToken.FromObject(state, _serializer)
                    });
                }

                var json = JsonConvert.SerializeObject(envelope, Formatting.Indented);
                await Task.Run(() => AtomicWrite(GetPath(kind), json), cancellationToken);
            }
            finally
            {
                _ioGate.Release();
            }
        }

        public async Task<SaveLoadResult> LoadAsync(SaveFileKind kind, CancellationToken cancellationToken)
        {
            await _ioGate.WaitAsync(cancellationToken);
            try
            {
                var path = GetPath(kind);
                var backupPath = path + ".bak";

                if (TryRead(path, out var envelope, out var mainError))
                {
                    Restore(kind, envelope);
                    return SaveLoadResult.Success();
                }

                if (TryRead(backupPath, out envelope, out var backupError))
                {
                    Restore(kind, envelope);
                    return SaveLoadResult.Success(true);
                }

                RestoreDefaults(kind);
                if (!File.Exists(path) && !File.Exists(backupPath))
                {
                    return SaveLoadResult.Success();
                }

                return SaveLoadResult.Failure($"Main save error: {mainError}; backup error: {backupError}");
            }
            finally
            {
                _ioGate.Release();
            }
        }

        private IEnumerable<ISaveParticipant> GetParticipants(SaveFileKind kind)
        {
            return _participantsProvider()
                .Where(participant => participant.FileKind == kind)
                .OrderBy(participant => participant.SectionKey, StringComparer.Ordinal);
        }

        private void Restore(SaveFileKind kind, SaveEnvelope envelope)
        {
            foreach (var participant in GetParticipants(kind))
            {
                if (!envelope.Sections.TryGetValue(participant.SectionKey, out var section) || section.Data == null)
                {
                    participant.RestoreState(participant.CreateDefaultState(), 0);
                    continue;
                }

                var state = section.Data.ToObject(participant.StateType, _serializer)
                    ?? participant.CreateDefaultState();
                participant.RestoreState(state, section.Version);
            }
        }

        private void RestoreDefaults(SaveFileKind kind)
        {
            foreach (var participant in GetParticipants(kind))
            {
                participant.RestoreState(participant.CreateDefaultState(), 0);
            }
        }

        private bool TryRead(string path, out SaveEnvelope envelope, out string error)
        {
            envelope = null;
            error = string.Empty;
            if (!File.Exists(path))
            {
                error = "File does not exist";
                return false;
            }

            try
            {
                envelope = JsonConvert.DeserializeObject<SaveEnvelope>(File.ReadAllText(path));
                if (envelope?.Sections == null)
                {
                    throw new InvalidDataException("Save envelope or sections are null.");
                }

                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private string GetPath(SaveFileKind kind)
        {
            var fileName = kind switch
            {
                SaveFileKind.Settings => "settings.json",
                SaveFileKind.Profile => "profile.json",
                SaveFileKind.Run => "run.json",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };

            return Path.Combine(_directoryPath, fileName);
        }

        private static void AtomicWrite(string path, string content)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = path + ".tmp";
            var backupPath = path + ".bak";
            File.WriteAllText(tempPath, content);

            if (!File.Exists(path))
            {
                File.Move(tempPath, path);
                return;
            }

            try
            {
                File.Replace(tempPath, path, backupPath, true);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(path, backupPath, true);
                File.Delete(path);
                File.Move(tempPath, path);
            }
            catch (IOException)
            {
                File.Copy(path, backupPath, true);
                File.Delete(path);
                File.Move(tempPath, path);
            }
        }
    }
}
