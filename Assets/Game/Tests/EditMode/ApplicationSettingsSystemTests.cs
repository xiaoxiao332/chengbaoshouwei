using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Saving;
using FortressFrontier.Core.Systems;
using FortressFrontier.Infrastructure.Saving;
using FortressFrontier.Runtime.Settings;
using FortressFrontier.Runtime.Audio;
using NUnit.Framework;

namespace FortressFrontier.Tests.EditMode
{
    public sealed class ApplicationSettingsSystemTests
    {
        private string _directory;

        [SetUp]
        public void SetUp() =>
            _directory = Path.Combine(Path.GetTempPath(), "FortressFrontierSettingsTests", Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test]
        public async Task DefaultsClampAndMute_ApplyEffectiveVolume()
        {
            var appliedVolume = new AudioVolumeSettings(0, 0, 0, true);
            var system = new ApplicationSettingsSystem(_ => Task.CompletedTask, value => appliedVolume = value);
            await system.InitializeAsync(new GameContext("settings-defaults"), CancellationToken.None);

            Assert.That(system.GetSnapshot().MasterVolumePercent, Is.EqualTo(100));
            Assert.That(system.GetSnapshot().MusicVolumePercent, Is.EqualTo(70));
            Assert.That(system.GetSnapshot().SfxVolumePercent, Is.EqualTo(80));
            Assert.That(system.GetSnapshot().Muted, Is.False);
            Assert.That(appliedVolume.EffectiveMasterVolume, Is.EqualTo(1f));

            Assert.That(await system.ApplyAsync(140, 130, -5, true, CancellationToken.None), Is.True);
            Assert.That(system.GetSnapshot().MasterVolumePercent, Is.EqualTo(100));
            Assert.That(system.GetSnapshot().Muted, Is.True);
            Assert.That(system.GetSnapshot().MusicVolumePercent, Is.EqualTo(100));
            Assert.That(system.GetSnapshot().SfxVolumePercent, Is.Zero);
            Assert.That(appliedVolume.EffectiveMasterVolume, Is.Zero);

            Assert.That(await system.ApplyAsync(-20, 35, 45, false, CancellationToken.None), Is.True);
            Assert.That(system.GetSnapshot().MasterVolumePercent, Is.Zero);
            Assert.That(appliedVolume.EffectiveMasterVolume, Is.Zero);
        }

        [Test]
        public async Task Apply_WhenSaveFails_RollsBackStateAndOutput()
        {
            var appliedVolume = new AudioVolumeSettings(0, 0, 0, true);
            var system = new ApplicationSettingsSystem(
                _ => Task.FromException(new IOException("simulated write failure")),
                value => appliedVolume = value);
            await system.InitializeAsync(new GameContext("settings-rollback"), CancellationToken.None);

            Assert.That(await system.ApplyAsync(35, 25, 15, true, CancellationToken.None), Is.False);
            Assert.That(system.GetSnapshot().MasterVolumePercent, Is.EqualTo(100));
            Assert.That(system.GetSnapshot().Muted, Is.False);
            Assert.That(system.GetSnapshot().MusicVolumePercent, Is.EqualTo(70));
            Assert.That(system.GetSnapshot().SfxVolumePercent, Is.EqualTo(80));
            Assert.That(appliedVolume.EffectiveMasterVolume, Is.EqualTo(1f));
        }

        [Test]
        public async Task Apply_SaveAndReload_RestoresSettingsSection()
        {
            ApplicationSettingsSystem first = null;
            SaveCoordinator firstCoordinator = null;
            first = new ApplicationSettingsSystem(
                token => firstCoordinator.SaveAsync(SaveFileKind.Settings, token), _ => { });
            firstCoordinator = new SaveCoordinator(_directory, "test", () => new ISaveParticipant[] { first });
            await first.InitializeAsync(new GameContext("settings-save"), CancellationToken.None);
            Assert.That(await first.ApplyAsync(64, 54, 44, true, CancellationToken.None), Is.True);
            Assert.That(await first.SetRewardedAdConsentAsync(true, CancellationToken.None), Is.True);
            Assert.That(File.Exists(Path.Combine(_directory, "settings.json")), Is.True);

            var appliedVolume = new AudioVolumeSettings(0, 0, 0, false);
            ApplicationSettingsSystem second = null;
            SaveCoordinator secondCoordinator = null;
            second = new ApplicationSettingsSystem(
                token => secondCoordinator.SaveAsync(SaveFileKind.Settings, token), value => appliedVolume = value);
            secondCoordinator = new SaveCoordinator(_directory, "test", () => new ISaveParticipant[] { second });
            await second.InitializeAsync(new GameContext("settings-reload"), CancellationToken.None);
            var result = await secondCoordinator.LoadAsync(SaveFileKind.Settings, CancellationToken.None);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(second.GetSnapshot().MasterVolumePercent, Is.EqualTo(64));
            Assert.That(second.GetSnapshot().MusicVolumePercent, Is.EqualTo(54));
            Assert.That(second.GetSnapshot().SfxVolumePercent, Is.EqualTo(44));
            Assert.That(second.GetSnapshot().Muted, Is.True);
            Assert.That(second.GetSnapshot().RewardedAdConsentGranted, Is.True);
            Assert.That(appliedVolume.EffectiveMasterVolume, Is.Zero);
        }

        [Test]
        public async Task RestoreVersion2_MigratesCategoryVolumesToDefaults()
        {
            var system = new ApplicationSettingsSystem(_ => Task.CompletedTask, _ => { });
            system.RestoreState(new ApplicationSettingsSaveData
            {
                Version = 2,
                MasterVolumePercent = 61,
                MusicVolumePercent = 0,
                SfxVolumePercent = 0,
                Muted = false
            }, 2);
            await system.InitializeAsync(new GameContext("settings-v2-migration"), CancellationToken.None);

            var snapshot = system.GetSnapshot();
            Assert.That(snapshot.MasterVolumePercent, Is.EqualTo(61));
            Assert.That(snapshot.MusicVolumePercent, Is.EqualTo(70));
            Assert.That(snapshot.SfxVolumePercent, Is.EqualTo(80));
        }
    }
}
