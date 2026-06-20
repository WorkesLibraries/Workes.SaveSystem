using System;
using System.Collections.Generic;
using System.IO;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class TryLoadResultTests
{
    private string _tempRoot = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "Workes.SaveSystem.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Test]
    public void TryLoadFromDisk_ReturnsSuccessAndRestoresProviders()
    {
        var manager = CreateManager();
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        provider.Current = new TestState { Value = 99 };

        var result = manager.TryLoadFromDisk("slot");

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.Success));
        Assert.That(result.Exception, Is.Null);
        Assert.That(provider.Current.Value, Is.EqualTo(1));
    }

    [Test]
    public void TryLoadFromDisk_ReturnsNotFoundForMissingSave()
    {
        var manager = CreateManager();
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();

        var result = manager.TryLoadFromDisk("missing");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.NotFound));
        Assert.That(result.Exception, Is.Null);
    }

    [Test]
    public void TryLoadFromDisk_ReturnsRegistrationsNotValidated()
    {
        var writer = CreateManager();
        var writerProvider = new TestProvider("player", new TestState { Value = 1 });
        writer.RegisterProvider(writerProvider);
        SaveValue(writer, writerProvider, "slot", 1);

        var reader = CreateManager();
        reader.RegisterProvider(new TestProvider("player", new TestState { Value = 99 }));

        var result = reader.TryLoadFromDisk("slot");

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.RegistrationsNotValidated));
        Assert.That(result.Exception, Is.Not.Null);
    }

    [Test]
    public void TryLoadFromDisk_ReturnsMissingProviderFile()
    {
        var manager = CreateManager();
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        File.Delete(Path.Combine(_tempRoot, "slot", "player.json"));
        provider.Current = new TestState { Value = 99 };

        var result = manager.TryLoadFromDisk("slot");

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.MissingProviderFile));
        Assert.That(result.Exception, Is.Not.Null);
        Assert.That(provider.Current.Value, Is.EqualTo(99));
    }

    [Test]
    public void TryLoadFromDisk_ReturnsCorruptData()
    {
        var manager = CreateManager();
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        File.WriteAllText(Path.Combine(_tempRoot, "slot", "player.json"), "not json");

        var result = manager.TryLoadFromDisk("slot");

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.CorruptData));
        Assert.That(result.Exception, Is.Not.Null);
    }

    [Test]
    public void TryLoadFromDisk_ReturnsMigrationFailed()
    {
        var oldManager = CreateManager();
        oldManager.RegisterProvider(new V1Provider(new V1State { Name = "Scout" }));
        oldManager.ValidateRegistrations();
        oldManager.SaveToDisk("slot");

        var newManager = CreateManager();
        newManager.RegisterProvider(
            new MigratingProvider(
                schemaVersion: 2,
                current: new V2State { Name = "Unset", Level = 0 },
                migrations: Array.Empty<SaveMigrationStep>()));
        newManager.ValidateRegistrations();

        var result = newManager.TryLoadFromDisk("slot");

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.MigrationFailed));
        Assert.That(result.Exception, Is.Not.Null);
    }

    [Test]
    public void TryLoadBackupSlotFromDisk_ReturnsBackupSystemDisabled()
    {
        var manager = CreateManager();

        var result = manager.TryLoadBackupSlotFromDisk("slot", slotNumber: 1);

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.BackupSystemDisabled));
        Assert.That(result.Exception, Is.Null);
    }

    [Test]
    public void TryLoadBackupSlotFromDisk_ReturnsNotFoundForMissingBackup()
    {
        var manager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 2);
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();

        var result = manager.TryLoadBackupSlotFromDisk("slot", slotNumber: 1);

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.NotFound));
        Assert.That(result.Exception, Is.Null);
    }

    private SaveManager<string> CreateManager(
        bool enableBackupSystem = false,
        int backupSystemMaxBackupCount = 0)
    {
        return new SaveManager<string>(
            new SaveSystemOptions<string>(
                saveRootPath: _tempRoot,
                serializer: new JsonSaveSerializer(),
                tempFolderName: SaveSystemOptions<string>.DefaultTempFolderName(),
                savePathResolver: identity => identity,
                fileNameResolver: SaveSystemOptions<string>.DefaultFileNameResolver,
                enableBackupSystem: enableBackupSystem,
                backupSystemMaxBackupCount: backupSystemMaxBackupCount));
    }

    private static void SaveValue(
        SaveManager<string> manager,
        TestProvider provider,
        string slot,
        int value)
    {
        provider.Current = new TestState { Value = value };
        manager.ValidateRegistrations();
        manager.SaveToDisk(slot);
    }

    public sealed class TestState
    {
        public int Value { get; set; }
    }

    public sealed class V1State
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class V2State
    {
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
    }

    private sealed class TestProvider : ISaveProvider<TestState>
    {
        public TestProvider(string saveKey, TestState current)
        {
            SaveKey = saveKey;
            Current = current;
        }

        public string SaveKey { get; }
        public int SchemaVersion => 1;
        public int LoadPriority => 0;
        public TestState Current { get; set; }

        public TestState CaptureState()
        {
            return Current;
        }

        public void RestoreState(TestState state)
        {
            Current = state;
        }
    }

    private sealed class V1Provider : ISaveProvider<V1State>
    {
        public V1Provider(V1State current)
        {
            Current = current;
        }

        public string SaveKey => "player";
        public int SchemaVersion => 1;
        public int LoadPriority => 0;
        public V1State Current { get; set; }

        public V1State CaptureState()
        {
            return Current;
        }

        public void RestoreState(V1State state)
        {
            Current = state;
        }
    }

    private sealed class MigratingProvider : ISaveProvider<V2State>, ISaveMigratable
    {
        private readonly ISaveMigrationSource _migrationSource;

        public MigratingProvider(
            int schemaVersion,
            V2State current,
            IReadOnlyList<SaveMigrationStep> migrations)
        {
            SchemaVersion = schemaVersion;
            Current = current;
            _migrationSource = new StaticMigrationSource(migrations);
        }

        public string SaveKey => "player";
        public int SchemaVersion { get; }
        public int LoadPriority => 0;
        public V2State Current { get; private set; }

        public V2State CaptureState()
        {
            return Current;
        }

        public void RestoreState(V2State state)
        {
            Current = state;
        }

        public ISaveMigrationSource CreateMigrationSource()
        {
            return _migrationSource;
        }
    }

    private sealed class StaticMigrationSource : ISaveMigrationSource
    {
        public StaticMigrationSource(IReadOnlyList<SaveMigrationStep> migrations)
        {
            Migrations = migrations;
        }

        public IReadOnlyList<SaveMigrationStep> Migrations { get; }
    }
}
