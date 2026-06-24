using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class SaveValidationTests
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
    public void ValidateSave_ReturnsSuccessWithMetadataAndDoesNotRestoreProviders()
    {
        var manager = CreateManager();
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        provider.Current = new TestState { Value = 99 };

        var result = manager.ValidateSave("slot");

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.Success));
        Assert.That(result.Exception, Is.Null);
        Assert.That(result.Metadata, Is.Not.Null);
        Assert.That(result.Metadata!.SaveId, Is.Not.Empty);
        Assert.That(provider.Current.Value, Is.EqualTo(99));
    }

    [Test]
    public void ValidateSave_ReturnsNotFoundForMissingSave()
    {
        var manager = CreateValidatedManager();

        var result = manager.ValidateSave("missing");

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.NotFound));
        Assert.That(result.Exception, Is.Null);
    }

    [Test]
    public void ValidateSave_ReturnsRegistrationsNotValidated()
    {
        var writer = CreateManager();
        var writerProvider = new TestProvider("player", new TestState { Value = 1 });
        writer.RegisterProvider(writerProvider);
        SaveValue(writer, writerProvider, "slot", 1);

        var reader = CreateManager();
        reader.RegisterProvider(new TestProvider("player", new TestState { Value = 99 }));

        var result = reader.ValidateSave("slot");

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.RegistrationsNotValidated));
        Assert.That(result.Exception, Is.Not.Null);
    }

    [Test]
    public void ValidateSave_ReturnsCorruptDataWhenMetadataIsMissing()
    {
        var manager = CreateManager();
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        File.Delete(Path.Combine(_tempRoot, "slot", "metadata.json"));

        var result = manager.ValidateSave("slot");

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.CorruptData));
        Assert.That(result.Exception, Is.Not.Null);
        Assert.That(result.Exception!.Message, Does.Contain("Missing metadata.json"));
    }

    [Test]
    public void ValidateSave_ReturnsCorruptDataWhenMetadataIsUnreadable()
    {
        var manager = CreateManager();
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        File.WriteAllText(Path.Combine(_tempRoot, "slot", "metadata.json"), "not json");

        var result = manager.ValidateSave("slot");

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.CorruptData));
        Assert.That(result.Exception, Is.Not.Null);
    }

    [Test]
    public void ValidateSave_ReturnsMissingProviderFileWhenRegisteredProviderFileIsMissing()
    {
        var manager = CreateManager();
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        File.Delete(Path.Combine(_tempRoot, "slot", "player.json"));
        provider.Current = new TestState { Value = 99 };

        var result = manager.ValidateSave("slot");

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.MissingProviderFile));
        Assert.That(result.Exception, Is.Not.Null);
        Assert.That(provider.Current.Value, Is.EqualTo(99));
    }

    [Test]
    public void ValidateSave_WithSkipMissingProviderFileBehavior_CanValidatePartialSave()
    {
        var manager = CreateManager(MissingProviderFileBehavior.Skip);
        var player = new TestProvider("player", new TestState { Value = 1 });
        var inventory = new TestProvider("inventory", new TestState { Value = 2 });
        manager.RegisterProvider(player);
        manager.RegisterProvider(inventory);
        manager.ValidateRegistrations();
        manager.SaveToDisk("slot");
        RemoveProviderManifest("slot");
        File.Delete(Path.Combine(_tempRoot, "slot", "inventory.json"));
        player.Current = new TestState { Value = 99 };
        inventory.Current = new TestState { Value = 88 };

        var result = manager.ValidateSave("slot");

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Metadata, Is.Not.Null);
        Assert.That(player.Current.Value, Is.EqualTo(99));
        Assert.That(inventory.Current.Value, Is.EqualTo(88));
    }

    private void RemoveProviderManifest(string slot)
    {
        var metadataPath = Path.Combine(_tempRoot, slot, "metadata.json");
        var metadata = JObject.Parse(File.ReadAllText(metadataPath));
        ((JObject)metadata["Data"]!).Remove("ProviderManifest");
        File.WriteAllText(metadataPath, metadata.ToString());
    }

    [Test]
    public void ValidateSave_ReturnsCorruptDataWhenProviderPayloadIsUnreadable()
    {
        var manager = CreateManager();
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        File.WriteAllText(Path.Combine(_tempRoot, "slot", "player.json"), "not json");

        var result = manager.ValidateSave("slot");

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.CorruptData));
        Assert.That(result.Exception, Is.Not.Null);
    }

    [Test]
    public void ValidateSave_AppliesMigrationInMemoryWithoutWritingMigratedData()
    {
        var oldManager = CreateManager();
        oldManager.RegisterProvider(new V1Provider(new V1State { Name = "Scout" }));
        oldManager.ValidateRegistrations();
        oldManager.SaveToDisk("slot");
        var playerPath = Path.Combine(_tempRoot, "slot", "player.json");
        var before = File.ReadAllBytes(playerPath);

        var newManager = CreateManager();
        var newProvider = new MigratingProvider(
            schemaVersion: 2,
            current: new V2State { Name = "Unset", Level = 0 },
            migrations: new[]
            {
                new SaveMigrationStep(1, (data, factory) => data.Set("Level", factory.CreateInt(12)))
            });
        newManager.RegisterProvider(newProvider);
        newManager.ValidateRegistrations();

        var result = newManager.ValidateSave("slot");
        var after = File.ReadAllBytes(playerPath);

        Assert.That(result.IsValid, Is.True);
        Assert.That(newProvider.Current.Name, Is.EqualTo("Unset"));
        Assert.That(newProvider.Current.Level, Is.EqualTo(0));
        Assert.That(after, Is.EqualTo(before));
        Assert.That(Encoding.UTF8.GetString(after), Does.Contain("\"SchemaVersion\": 1"));
    }

    [Test]
    public void ValidateSave_ReturnsMigrationFailedWhenMigrationPathIsMissing()
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

        var result = newManager.ValidateSave("slot");

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.MigrationFailed));
        Assert.That(result.Exception, Is.Not.Null);
    }

    [Test]
    public void ValidateSave_DoesNotMutateRecoveryArtifacts()
    {
        var manager = CreateManager();
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        var tempPath = Path.Combine(_tempRoot, "slot_tmp");
        var toDeletePath = Path.Combine(_tempRoot, "slot_toDelete");
        Directory.CreateDirectory(tempPath);
        Directory.CreateDirectory(toDeletePath);

        var result = manager.ValidateSave("slot");

        Assert.That(result.IsValid, Is.True);
        Assert.That(Directory.Exists(tempPath), Is.True);
        Assert.That(Directory.Exists(toDeletePath), Is.True);
    }

    [Test]
    public void ValidateSave_DoesNotCallLifecycleHooks()
    {
        var manager = CreateManager();
        var provider = new LifecycleProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        provider.BeforeSaveCalls = 0;
        provider.AfterLoadCalls = 0;

        var result = manager.ValidateSave("slot");

        Assert.That(result.IsValid, Is.True);
        Assert.That(provider.BeforeSaveCalls, Is.Zero);
        Assert.That(provider.AfterLoadCalls, Is.Zero);
    }

    [Test]
    public void ValidateSave_ReturnsCorruptDataWhenSerializerMetadataIsInvalid()
    {
        var serializer = new MetadataJsonSerializer();
        var manager = CreateManager(serializer: serializer);
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        var metadataPath = Path.Combine(_tempRoot, "slot", "metadata.json");
        File.WriteAllText(
            metadataPath,
            File.ReadAllText(metadataPath).Replace(
                MetadataJsonSerializer.ExpectedToken,
                "wrong",
                StringComparison.Ordinal));

        var result = manager.ValidateSave("slot");

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.CorruptData));
        Assert.That(result.Exception, Is.Not.Null);
        Assert.That(result.Exception!.Message, Does.Contain("Serializer metadata"));
    }

    [Test]
    public void ValidateBackupSlot_ReturnsBackupSystemDisabledBeforeRequestAndRegistrationValidation()
    {
        var manager = CreateManager();
        manager.RegisterProvider(new TestProvider("player", new TestState { Value = 1 }));

        var nullIdentityResult = manager.ValidateBackupSlot(null!, slotNumber: 1);
        var invalidSlotResult = manager.ValidateBackupSlot("slot", slotNumber: 0);

        Assert.That(nullIdentityResult.Status, Is.EqualTo(SaveLoadStatus.BackupSystemDisabled));
        Assert.That(nullIdentityResult.Exception, Is.Null);
        Assert.That(invalidSlotResult.Status, Is.EqualTo(SaveLoadStatus.BackupSystemDisabled));
        Assert.That(invalidSlotResult.Exception, Is.Null);
    }

    [Test]
    public void ValidateBackupSlot_ReturnsInvalidRequestWhenSlotNumberIsInvalidAndBackupsAreEnabled()
    {
        var manager = CreateValidatedManager(enableBackupSystem: true, backupSystemMaxBackupCount: 1);

        var result = manager.ValidateBackupSlot("slot", slotNumber: 0);

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.InvalidRequest));
        Assert.That(result.Exception, Is.Not.Null);
    }

    [Test]
    public void ValidateBackupSlot_ReturnsNotFoundWhenBackupIsMissing()
    {
        var manager = CreateValidatedManager(enableBackupSystem: true, backupSystemMaxBackupCount: 1);

        var result = manager.ValidateBackupSlot("slot", slotNumber: 1);

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.NotFound));
        Assert.That(result.Exception, Is.Null);
    }

    [Test]
    public void ValidateBackupSlot_ReturnsSuccessWithMetadataForValidBackup()
    {
        var manager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 1);
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "slot", 2);
        provider.Current = new TestState { Value = 99 };

        var result = manager.ValidateBackupSlot("slot", slotNumber: 1);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Metadata, Is.Not.Null);
        Assert.That(provider.Current.Value, Is.EqualTo(99));
    }

    [Test]
    public void TryLoadFromDisk_ReturnsCorruptDataWhenMetadataIsMissing()
    {
        var manager = CreateManager();
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        File.Delete(Path.Combine(_tempRoot, "slot", "metadata.json"));

        var result = manager.TryLoadFromDisk("slot");

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.CorruptData));
        Assert.That(result.Exception, Is.Not.Null);
    }

    [Test]
    public void TryLoadBackupSlotFromDisk_ReturnsCorruptDataWhenBackupMetadataIsMissing()
    {
        var manager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 1);
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "slot", 2);
        File.Delete(Path.Combine(_tempRoot, "_backup", "slot_0001", "metadata.json"));

        var result = manager.TryLoadBackupSlotFromDisk("slot", slotNumber: 1);

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.CorruptData));
        Assert.That(result.Exception, Is.Not.Null);
    }

    private SaveManager<string> CreateValidatedManager(
        bool enableBackupSystem = false,
        int backupSystemMaxBackupCount = 0)
    {
        var manager = CreateManager(
            enableBackupSystem: enableBackupSystem,
            backupSystemMaxBackupCount: backupSystemMaxBackupCount);
        manager.RegisterProvider(new TestProvider("player", new TestState { Value = 1 }));
        manager.ValidateRegistrations();
        return manager;
    }

    private SaveManager<string> CreateManager(
        MissingProviderFileBehavior missingProviderFileBehavior = MissingProviderFileBehavior.Throw,
        bool enableBackupSystem = false,
        int backupSystemMaxBackupCount = 0,
        ISaveSerializer? serializer = null)
    {
        return new SaveManager<string>(
            new SaveSystemOptions<string>(
                saveRootPath: _tempRoot,
                serializer: serializer ?? new JsonSaveSerializer(),
                tempFolderName: SaveSystemOptions<string>.DefaultTempFolderName(),
                savePathResolver: identity => identity,
                fileNameResolver: SaveSystemOptions<string>.DefaultFileNameResolver,
                enableBackupSystem: enableBackupSystem,
                backupSystemMaxBackupCount: backupSystemMaxBackupCount,
                missingProviderFileBehavior: missingProviderFileBehavior));
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

    private class TestProvider : ISaveProvider<TestState>
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

    private sealed class LifecycleProvider : TestProvider, ISaveLifecycle
    {
        public LifecycleProvider(string saveKey, TestState current)
            : base(saveKey, current)
        {
        }

        public int BeforeSaveCalls { get; set; }
        public int AfterLoadCalls { get; set; }

        public void OnBeforeSave()
        {
            BeforeSaveCalls++;
        }

        public void OnAfterLoad()
        {
            AfterLoadCalls++;
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

    private sealed class MetadataJsonSerializer : ISaveSerializer, ISaveSerializerMetadataHandler
    {
        public const string ExpectedToken = "expected-token";
        private readonly JsonSaveSerializer _inner = new JsonSaveSerializer();

        public string FileExtension => _inner.FileExtension;
        public ISaveMigrationCapableSerializer? Migration => _inner.Migration;
        public ISaveSerializerMetadataHandler? Metadata => this;

        public ISaveSchematic CreateSchematic(Type stateType)
        {
            return _inner.CreateSchematic(stateType);
        }

        public byte[] Serialize(object state, ISaveSchematic schematic)
        {
            return _inner.Serialize(state, schematic);
        }

        public object Deserialize(byte[] data, ISaveSchematic schematic)
        {
            return _inner.Deserialize(data, schematic);
        }

        public int ExtractSchemaVersion(byte[] data)
        {
            return _inner.ExtractSchemaVersion(data);
        }

        public void WriteMetadata(SaveSerializerMetadataWriteContext context)
        {
            context.Metadata.Global["Token"] = ExpectedToken;
        }

        public void ValidateMetadata(SaveSerializerMetadataValidationContext context)
        {
            if (!context.Metadata.Global.TryGetValue("Token", out var token) ||
                token != ExpectedToken)
            {
                throw new InvalidOperationException("Serializer metadata token is invalid.");
            }
        }
    }
}
