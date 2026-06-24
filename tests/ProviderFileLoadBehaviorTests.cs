using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class ProviderFileLoadBehaviorTests
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
    public void LoadFromDisk_ThrowsWhenRegisteredProviderFileIsMissing()
    {
        var manager = CreateManager();
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();
        manager.SaveToDisk("slot");
        File.Delete(Path.Combine(_tempRoot, "slot", "player.json"));
        provider.Current = new TestState { Value = 99 };

        var ex = Assert.Throws<InvalidOperationException>(() => manager.LoadFromDisk("slot"));

        Assert.That(ex!.Message, Does.Contain("Missing save file"));
        Assert.That(ex.Message, Does.Contain("player"));
        Assert.That(provider.Current.Value, Is.EqualTo(99));
    }

    [Test]
    public void LoadFromDisk_WithSkipMissingProviderFileBehavior_ThrowsWhenManifestPresentProviderFileIsMissing()
    {
        var manager = CreateManager(MissingProviderFileBehavior.Skip);
        var player = new TestProvider("player", new TestState { Value = 1 });
        var inventory = new TestProvider("inventory", new TestState { Value = 2 });
        manager.RegisterProvider(player);
        manager.RegisterProvider(inventory);
        manager.ValidateRegistrations();
        manager.SaveToDisk("slot");
        File.Delete(Path.Combine(_tempRoot, "slot", "inventory.json"));
        player.Current = new TestState { Value = 99 };
        inventory.Current = new TestState { Value = 88 };

        var ex = Assert.Throws<InvalidOperationException>(() => manager.LoadFromDisk("slot"));

        Assert.That(ex!.Message, Does.Contain("Missing save file"));
        Assert.That(ex.Message, Does.Contain("inventory"));
        Assert.That(player.Current.Value, Is.EqualTo(99));
        Assert.That(inventory.Current.Value, Is.EqualTo(88));
    }

    [Test]
    public void LoadFromDisk_WithSkipMissingProviderFileBehavior_PreservesLegacyMissingFileBehavior()
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

        var loaded = manager.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(player.Current.Value, Is.EqualTo(1));
        Assert.That(inventory.Current.Value, Is.EqualTo(88));
    }

    [Test]
    public void LoadFromDisk_IgnoresUnknownExtraProviderFiles()
    {
        var manager = CreateManager();
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();
        manager.SaveToDisk("slot");
        File.WriteAllText(Path.Combine(_tempRoot, "slot", "unknown.json"), "{}");
        provider.Current = new TestState { Value = 99 };

        var loaded = manager.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Value, Is.EqualTo(1));
    }

    [Test]
    public void LoadFromDisk_ThrowsForPartialSaveFolderMissingMetadata()
    {
        var manager = CreateManager();
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();
        Directory.CreateDirectory(Path.Combine(_tempRoot, "slot"));

        var ex = Assert.Throws<InvalidOperationException>(() => manager.LoadFromDisk("slot"));

        Assert.That(ex!.Message, Does.Contain("Missing metadata.json"));
    }

    [Test]
    public void SaveToDisk_WritesProviderManifestForPersistedProvidersOnly()
    {
        var manager = CreateManager();
        manager.RegisterProvider(new TestProvider("player", new TestState { Value = 1 }));
        manager.RegisterMemoryProvider(new TestProvider("cache", new TestState { Value = 2 }));
        manager.ValidateRegistrations();

        manager.SaveToDisk("slot");

        var metadata = ReadMetadata("slot");
        var manifest = (JArray)metadata["Data"]!["ProviderManifest"]!;
        Assert.That(manifest.Select(entry => entry["SaveKey"]!.Value<string>()), Is.EquivalentTo(new[] { "player" }));
        Assert.That(manifest[0]["SchemaVersion"]!.Value<int>(), Is.EqualTo(1));
        Assert.That(manifest[0]["FileName"]!.Value<string>(), Is.EqualTo("player.json"));
    }

    [Test]
    public void LoadFromDisk_UsesManifestFileNameForCustomResolver()
    {
        var versionedResolver = new Func<SaveFileContext, string>(context => $"{context.SaveKey}-v{context.SchemaVersion}");
        var writer = CreateManager(fileNameResolver: versionedResolver);
        var writerProvider = new VersionedProvider("player", schemaVersion: 1, new TestState { Value = 7 });
        writer.RegisterProvider(writerProvider);
        writer.ValidateRegistrations();
        writer.SaveToDisk("slot");

        var reader = CreateManager(fileNameResolver: versionedResolver);
        var readerProvider = new VersionedProvider("player", schemaVersion: 2, new TestState { Value = 99 });
        reader.RegisterProvider(readerProvider);
        reader.ValidateRegistrations();

        var loaded = reader.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(readerProvider.Current.Value, Is.EqualTo(7));
    }

    [Test]
    public void LoadFromDisk_WhenProviderIsAbsentFromManifest_LeavesProviderUnchanged()
    {
        var writer = CreateManager();
        var player = new TestProvider("player", new TestState { Value = 1 });
        writer.RegisterProvider(player);
        writer.ValidateRegistrations();
        writer.SaveToDisk("slot");

        var reader = CreateManager();
        var readerPlayer = new TestProvider("player", new TestState { Value = 99 });
        var quest = new TestProvider("quest", new TestState { Value = 88 });
        reader.RegisterProvider(readerPlayer);
        reader.RegisterProvider(quest);
        reader.ValidateRegistrations();

        var loaded = reader.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(readerPlayer.Current.Value, Is.EqualTo(1));
        Assert.That(quest.Current.Value, Is.EqualTo(88));
    }

    [Test]
    public void LoadFromDisk_WhenProviderIsAbsentFromManifest_RestoresDefaultStateWhenProviderOptsIn()
    {
        var writer = CreateManager();
        var player = new TestProvider("player", new TestState { Value = 1 });
        writer.RegisterProvider(player);
        writer.ValidateRegistrations();
        writer.SaveToDisk("slot");

        var reader = CreateManager();
        var readerPlayer = new TestProvider("player", new TestState { Value = 99 });
        var quest = new DefaultStateProvider("quest", new TestState { Value = 88 }, new TestState { Value = 42 });
        reader.RegisterProvider(readerPlayer);
        reader.RegisterProvider(quest);
        reader.ValidateRegistrations();

        var loaded = reader.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(readerPlayer.Current.Value, Is.EqualTo(1));
        Assert.That(quest.Current.Value, Is.EqualTo(42));
    }

    [Test]
    public void ValidateSave_ReturnsMissingProviderFileWhenManifestPresentFileIsMissing()
    {
        var manager = CreateManager(MissingProviderFileBehavior.Skip);
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();
        manager.SaveToDisk("slot");
        File.Delete(Path.Combine(_tempRoot, "slot", "player.json"));

        var result = manager.ValidateSave("slot");

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.MissingProviderFile));
    }

    [Test]
    public void ValidateSave_ReturnsCorruptDataWhenProviderManifestHasDuplicateSaveKeys()
    {
        var manager = CreateManager();
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();
        manager.SaveToDisk("slot");
        var metadata = ReadMetadata("slot");
        var manifest = (JArray)metadata["Data"]!["ProviderManifest"]!;
        manifest.Add(manifest[0]!.DeepClone());
        WriteMetadata("slot", metadata);

        var result = manager.ValidateSave("slot");

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.CorruptData));
    }

    [Test]
    public void ValidateSave_ReturnsCorruptDataWhenProviderManifestFileNameIsInvalid()
    {
        var manager = CreateManager();
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();
        manager.SaveToDisk("slot");
        var metadata = ReadMetadata("slot");
        metadata["Data"]!["ProviderManifest"]![0]!["FileName"] = "../player.json";
        WriteMetadata("slot", metadata);

        var result = manager.ValidateSave("slot");

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.CorruptData));
    }

    [Test]
    public void LoadBackupSlotFromDisk_ThrowsWhenRegisteredProviderFileIsMissingFromBackup()
    {
        var manager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 1);
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "slot", 2);
        File.Delete(Path.Combine(_tempRoot, "_backup", "slot_0001", "player.json"));
        provider.Current = new TestState { Value = 99 };

        var ex = Assert.Throws<InvalidOperationException>(() => manager.LoadBackupSlotFromDisk("slot", slotNumber: 1));

        Assert.That(ex!.Message, Does.Contain("Missing save file"));
        Assert.That(ex.Message, Does.Contain("player"));
        Assert.That(provider.Current.Value, Is.EqualTo(99));
    }

    [Test]
    public void ValidateBackupSlot_ReturnsMissingProviderFileWhenManifestPresentFileIsMissingFromBackup()
    {
        var manager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 1);
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "slot", 2);
        File.Delete(Path.Combine(_tempRoot, "_backup", "slot_0001", "player.json"));

        var result = manager.ValidateBackupSlot("slot", slotNumber: 1);

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.MissingProviderFile));
    }

    private SaveManager<string> CreateManager(
        MissingProviderFileBehavior missingProviderFileBehavior = MissingProviderFileBehavior.Throw,
        bool enableBackupSystem = false,
        int backupSystemMaxBackupCount = 0,
        Func<SaveFileContext, string>? fileNameResolver = null)
    {
        return new SaveManager<string>(
            new SaveSystemOptions<string>(
                saveRootPath: _tempRoot,
                serializer: new JsonSaveSerializer(),
                tempFolderName: SaveSystemOptions<string>.DefaultTempFolderName(),
                savePathResolver: identity => identity,
                fileNameResolver: fileNameResolver ?? SaveSystemOptions<string>.DefaultFileNameResolver,
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

    private JObject ReadMetadata(string slot)
    {
        return JObject.Parse(File.ReadAllText(Path.Combine(_tempRoot, slot, "metadata.json")));
    }

    private void WriteMetadata(string slot, JObject metadata)
    {
        File.WriteAllText(Path.Combine(_tempRoot, slot, "metadata.json"), metadata.ToString());
    }

    private void RemoveProviderManifest(string slot)
    {
        var path = Path.Combine(_tempRoot, slot, "metadata.json");
        var metadata = JObject.Parse(File.ReadAllText(path));
        ((JObject)metadata["Data"]!).Remove("ProviderManifest");
        WriteMetadata(slot, metadata);
    }

    public sealed class TestState
    {
        public int Value { get; set; }
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

    private sealed class VersionedProvider : ISaveProvider<TestState>, ISaveMigratable
    {
        public VersionedProvider(string saveKey, int schemaVersion, TestState current)
        {
            SaveKey = saveKey;
            SchemaVersion = schemaVersion;
            Current = current;
        }

        public string SaveKey { get; }
        public int SchemaVersion { get; }
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

        public ISaveMigrationSource CreateMigrationSource()
        {
            return new StaticMigrationSource(new[] { SaveMigrationStep.From(1, (node, factory) => { }) });
        }
    }

    private sealed class DefaultStateProvider : ISaveProvider<TestState>, ISaveDefaultStateProvider<TestState>
    {
        private readonly TestState _defaultState;

        public DefaultStateProvider(string saveKey, TestState current, TestState defaultState)
        {
            SaveKey = saveKey;
            Current = current;
            _defaultState = defaultState;
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

        public TestState CreateDefaultStateForMissingSave()
        {
            return _defaultState;
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
