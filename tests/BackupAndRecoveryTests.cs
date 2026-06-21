using System;
using System.Collections.Generic;
using System.IO;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class BackupAndRecoveryTests
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
    public void SaveToDisk_WithBackups_RotatesMostRecentBackupToSlotOne()
    {
        var manager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 2);
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);

        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "slot", 2);
        SaveValue(manager, provider, "slot", 3);
        SaveValue(manager, provider, "slot", 4);

        Assert.That(ReadValueFromFolder(Path.Combine(_tempRoot, "slot")), Is.EqualTo(4));
        Assert.That(ReadValueFromFolder(Path.Combine(_tempRoot, "_backup", "slot_0001")), Is.EqualTo(3));
        Assert.That(ReadValueFromFolder(Path.Combine(_tempRoot, "_backup", "slot_0002")), Is.EqualTo(2));
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "slot_0003")), Is.False);
    }

    [Test]
    public void LoadBackupSlotFromDisk_LoadsRequestedBackupSlot()
    {
        var manager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 2);
        var provider = new TestProvider(new TestState { Value = 0 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "slot", 2);
        SaveValue(manager, provider, "slot", 3);
        provider.Current = new TestState { Value = 99 };

        var loaded = manager.LoadBackupSlotFromDisk("slot", slotNumber: 1);

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Value, Is.EqualTo(2));
    }

    [Test]
    public void SaveToDisk_WithBackups_SupportsNestedSavePaths()
    {
        var manager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 2);
        var provider = new TestProvider(new TestState { Value = 0 });
        manager.RegisterProvider(provider);

        SaveValue(manager, provider, "profile-a/slot", 1);
        SaveValue(manager, provider, "profile-a/slot", 2);
        provider.Current = new TestState { Value = 99 };

        var loaded = manager.LoadBackupSlotFromDisk("profile-a/slot", slotNumber: 1);

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Value, Is.EqualTo(1));
        Assert.That(ReadValueFromFolder(Path.Combine(_tempRoot, "profile-a", "slot")), Is.EqualTo(2));
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "profile-a", "slot_0001")), Is.True);
    }

    [Test]
    public void LoadBackupSlotFromDisk_ReturnsFalseWhenBackupsAreDisabled()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();

        var loaded = manager.LoadBackupSlotFromDisk("slot", slotNumber: 1);

        Assert.That(loaded, Is.False);
    }

    [Test]
    public void SaveToDisk_WithBackups_NormalizesGappedBackupSlotsBeforeRotation()
    {
        var manager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 3);
        var provider = new TestProvider(new TestState { Value = 0 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "slot", 2);
        SaveValue(manager, provider, "slot", 3);
        SaveValue(manager, provider, "slot", 4);
        Directory.Delete(Path.Combine(_tempRoot, "_backup", "slot_0002"), recursive: true);

        SaveValue(manager, provider, "slot", 5);

        Assert.That(ReadValueFromFolder(Path.Combine(_tempRoot, "slot")), Is.EqualTo(5));
        Assert.That(ReadValueFromFolder(Path.Combine(_tempRoot, "_backup", "slot_0001")), Is.EqualTo(4));
        Assert.That(ReadValueFromFolder(Path.Combine(_tempRoot, "_backup", "slot_0002")), Is.EqualTo(3));
        Assert.That(ReadValueFromFolder(Path.Combine(_tempRoot, "_backup", "slot_0003")), Is.EqualTo(1));
    }

    [Test]
    public void SaveToDisk_WithBackups_RemovesTamperedBackupBeyondConfiguredLimit()
    {
        var manager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 2);
        var provider = new TestProvider(new TestState { Value = 0 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "slot", 2);
        SaveValue(manager, provider, "slot", 3);
        CopyDirectory(
            Path.Combine(_tempRoot, "_backup", "slot_0002"),
            Path.Combine(_tempRoot, "_backup", "slot_0004"));

        SaveValue(manager, provider, "slot", 4);

        Assert.That(ReadValueFromFolder(Path.Combine(_tempRoot, "slot")), Is.EqualTo(4));
        Assert.That(ReadValueFromFolder(Path.Combine(_tempRoot, "_backup", "slot_0001")), Is.EqualTo(3));
        Assert.That(ReadValueFromFolder(Path.Combine(_tempRoot, "_backup", "slot_0002")), Is.EqualTo(2));
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "slot_0003")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "slot_0004")), Is.False);
    }

    [Test]
    public void SaveToDisk_WithBackups_IgnoresUnrelatedBackupFolderNames()
    {
        var manager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 2);
        var provider = new TestProvider(new TestState { Value = 0 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "slot", 2);
        Directory.CreateDirectory(Path.Combine(_tempRoot, "_backup", "slot_extra"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "_backup", "slot_00001"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "_backup", "slot_00x1"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "_backup", "slot2_0001"));

        SaveValue(manager, provider, "slot", 3);

        Assert.That(ReadValueFromFolder(Path.Combine(_tempRoot, "_backup", "slot_0001")), Is.EqualTo(2));
        Assert.That(ReadValueFromFolder(Path.Combine(_tempRoot, "_backup", "slot_0002")), Is.EqualTo(1));
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "slot_extra")), Is.True);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "slot_00001")), Is.True);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "slot_00x1")), Is.True);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "slot2_0001")), Is.True);
    }

    [Test]
    public void RecoverSave_RequiresValidatedRegistrations()
    {
        var manager = CreateManager();
        manager.RegisterProvider(new TestProvider(new TestState { Value = 1 }));

        var ex = Assert.Throws<InvalidOperationException>(() => manager.RecoverSave("slot"));

        Assert.That(ex!.Message, Does.Contain("ValidateRegistrations"));
    }

    [Test]
    public void RecoverSave_CompletesInterruptedSwapWhenMainIsMissing()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "other", 7);

        var slotPath = Path.Combine(_tempRoot, "slot");
        var tempPath = Path.Combine(_tempRoot, "slot_tmp");
        var toDeletePath = Path.Combine(_tempRoot, "slot_toDelete");
        Directory.Move(slotPath, tempPath);
        Directory.Move(Path.Combine(_tempRoot, "other"), toDeletePath);

        var loaded = manager.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Value, Is.EqualTo(1));
        Assert.That(Directory.Exists(slotPath), Is.True);
        Assert.That(Directory.Exists(tempPath), Is.False);
        Assert.That(Directory.Exists(toDeletePath), Is.False);
    }

    [Test]
    public void RecoverSave_WhenMainIsMissingAndTempIsCorrupt_RestoresValidToDeleteFolder()
    {
        var warnings = new List<string>();
        var manager = CreateManager(warningSink: warnings.Add);
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "other", 7);

        var slotPath = Path.Combine(_tempRoot, "slot");
        var tempPath = Path.Combine(_tempRoot, "slot_tmp");
        var toDeletePath = Path.Combine(_tempRoot, "slot_toDelete");
        Directory.Move(Path.Combine(_tempRoot, "other"), tempPath);
        File.WriteAllText(Path.Combine(tempPath, "player.json"), "not json");
        Directory.Move(slotPath, toDeletePath);

        var loaded = manager.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Value, Is.EqualTo(1));
        Assert.That(ReadValueFromFolder(slotPath), Is.EqualTo(1));
        Assert.That(Directory.Exists(tempPath), Is.False);
        Assert.That(Directory.Exists(toDeletePath), Is.False);
        Assert.That(warnings, Has.Count.EqualTo(1));
        Assert.That(warnings[0], Does.Contain("falling back"));
    }

    [Test]
    public void RecoverSave_WithSkipMissingProviderFiles_WhenMainIsMissingAndTempIsPartial_RestoresValidToDeleteFolder()
    {
        var warnings = new List<string>();
        var manager = CreateManager(
            missingProviderFileBehavior: MissingProviderFileBehavior.Skip,
            warningSink: warnings.Add);
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "other", 7);

        var slotPath = Path.Combine(_tempRoot, "slot");
        var tempPath = Path.Combine(_tempRoot, "slot_tmp");
        var toDeletePath = Path.Combine(_tempRoot, "slot_toDelete");
        Directory.Move(Path.Combine(_tempRoot, "other"), tempPath);
        File.Delete(Path.Combine(tempPath, "player.json"));
        Directory.Move(slotPath, toDeletePath);

        var loaded = manager.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Value, Is.EqualTo(1));
        Assert.That(ReadValueFromFolder(slotPath), Is.EqualTo(1));
        Assert.That(Directory.Exists(tempPath), Is.False);
        Assert.That(Directory.Exists(toDeletePath), Is.False);
        Assert.That(warnings, Has.Count.EqualTo(1));
        Assert.That(warnings[0], Does.Contain("falling back"));
    }

    [Test]
    public void RecoverSave_WhenMainIsMissingAndNoCandidateIsValid_PreservesRecoveryArtifacts()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "other", 7);

        var slotPath = Path.Combine(_tempRoot, "slot");
        var tempPath = Path.Combine(_tempRoot, "slot_tmp");
        var toDeletePath = Path.Combine(_tempRoot, "slot_toDelete");
        Directory.Move(Path.Combine(_tempRoot, "other"), tempPath);
        Directory.Move(slotPath, toDeletePath);
        File.WriteAllText(Path.Combine(tempPath, "player.json"), "not json");
        File.WriteAllText(Path.Combine(toDeletePath, "player.json"), "not json");

        var ex = Assert.Throws<InvalidOperationException>(() => manager.RecoverSave("slot"));

        Assert.That(ex!.Message, Does.Contain("Recovery failed"));
        Assert.That(Directory.Exists(tempPath), Is.True);
        Assert.That(Directory.Exists(toDeletePath), Is.True);
        Assert.That(Directory.Exists(slotPath), Is.False);
    }

    [Test]
    public void RecoverSave_PromotesTempFolderWhenMainAndToDeleteAreMissing()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);

        var slotPath = Path.Combine(_tempRoot, "slot");
        var tempPath = Path.Combine(_tempRoot, "slot_tmp");
        Directory.Move(slotPath, tempPath);

        var loaded = manager.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Value, Is.EqualTo(1));
        Assert.That(Directory.Exists(slotPath), Is.True);
        Assert.That(Directory.Exists(tempPath), Is.False);
    }

    [Test]
    public void RecoverSave_WithSkipMissingProviderFiles_WhenOnlyTempExistsAndIsPartial_PreservesTempAndFails()
    {
        var manager = CreateManager(missingProviderFileBehavior: MissingProviderFileBehavior.Skip);
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);

        var slotPath = Path.Combine(_tempRoot, "slot");
        var tempPath = Path.Combine(_tempRoot, "slot_tmp");
        Directory.Move(slotPath, tempPath);
        File.Delete(Path.Combine(tempPath, "player.json"));

        var ex = Assert.Throws<InvalidOperationException>(() => manager.LoadFromDisk("slot"));

        Assert.That(ex!.Message, Does.Contain("Recovery failed"));
        Assert.That(ex.Message, Does.Contain("Missing save file"));
        Assert.That(Directory.Exists(slotPath), Is.False);
        Assert.That(Directory.Exists(tempPath), Is.True);
    }

    [Test]
    public void RecoverSave_CompletesSwapWhenMainAndTempBothExist()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);

        var slotPath = Path.Combine(_tempRoot, "slot");
        var tempPath = Path.Combine(_tempRoot, "slot_tmp");
        CopyDirectory(slotPath, tempPath);
        WriteValueToFolder(tempPath, 2);

        var loaded = manager.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Value, Is.EqualTo(2));
        Assert.That(ReadValueFromFolder(slotPath), Is.EqualTo(2));
        Assert.That(Directory.Exists(tempPath), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "slot_toDelete")), Is.False);
    }

    [Test]
    public void RecoverSave_WithSkipMissingProviderFiles_WhenMainAndPartialTempExist_KeepsMainSave()
    {
        var warnings = new List<string>();
        var manager = CreateManager(
            missingProviderFileBehavior: MissingProviderFileBehavior.Skip,
            warningSink: warnings.Add);
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);

        var slotPath = Path.Combine(_tempRoot, "slot");
        var tempPath = Path.Combine(_tempRoot, "slot_tmp");
        CopyDirectory(slotPath, tempPath);
        WriteValueToFolder(tempPath, 2);
        File.Delete(Path.Combine(tempPath, "player.json"));
        provider.Current = new TestState { Value = 99 };

        var loaded = manager.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Value, Is.EqualTo(1));
        Assert.That(ReadValueFromFolder(slotPath), Is.EqualTo(1));
        Assert.That(Directory.Exists(tempPath), Is.False);
        Assert.That(warnings, Has.Count.EqualTo(1));
        Assert.That(warnings[0], Does.Contain("keeping the existing main save"));
    }

    [Test]
    public void RecoverSave_CleansToDeleteFolderWhenPromotedMainAlreadyExists()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "old", 9);

        var toDeletePath = Path.Combine(_tempRoot, "slot_toDelete");
        Directory.Move(Path.Combine(_tempRoot, "old"), toDeletePath);

        var loaded = manager.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Value, Is.EqualTo(1));
        Assert.That(Directory.Exists(toDeletePath), Is.False);
    }

    [Test]
    public void RecoverSave_RejectsTempFolderWithMismatchedSaveId()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "other", 2);

        Directory.Move(Path.Combine(_tempRoot, "other"), Path.Combine(_tempRoot, "slot_tmp"));

        var ex = Assert.Throws<InvalidOperationException>(() => manager.RecoverSave("slot"));

        Assert.That(ex!.Message, Does.Contain("SaveId mismatch"));
    }

    [Test]
    public void RecoverSave_RejectsTempFolderWithOlderSchemaInsteadOfRunningMigration()
    {
        var oldManager = CreateManager();
        oldManager.RegisterProvider(new V1Provider(new V1State { Name = "Scout" }));
        oldManager.ValidateRegistrations();
        oldManager.SaveToDisk("slot");

        var slotPath = Path.Combine(_tempRoot, "slot");
        var tempPath = Path.Combine(_tempRoot, "slot_tmp");
        Directory.Move(slotPath, tempPath);

        var newProvider = new MigratingProvider(new V2State { Name = "Unset", Level = 0 });
        var newManager = CreateManager();
        newManager.RegisterProvider(newProvider);
        newManager.ValidateRegistrations();

        var ex = Assert.Throws<InvalidOperationException>(() => newManager.RecoverSave("slot"));

        Assert.That(ex!.Message, Does.Contain("Recovery failed"));
        Assert.That(ex.Message, Does.Contain("does not run migrations"));
        Assert.That(newProvider.Current.Name, Is.EqualTo("Unset"));
        Assert.That(newProvider.Current.Level, Is.EqualTo(0));
        Assert.That(Directory.Exists(slotPath), Is.False);
        Assert.That(Directory.Exists(tempPath), Is.True);
    }

    private SaveManager<string> CreateManager(
        bool enableBackupSystem = false,
        int backupSystemMaxBackupCount = 0,
        Action<string>? warningSink = null,
        MissingProviderFileBehavior missingProviderFileBehavior = MissingProviderFileBehavior.Throw)
    {
        return new SaveManager<string>(
            new SaveSystemOptions<string>(
                saveRootPath: _tempRoot,
                serializer: new JsonSaveSerializer(),
                tempFolderName: SaveSystemOptions<string>.DefaultTempFolderName(),
                savePathResolver: identity => identity,
                fileNameResolver: SaveSystemOptions<string>.DefaultFileNameResolver,
                enableBackupSystem: enableBackupSystem,
                backupSystemMaxBackupCount: backupSystemMaxBackupCount,
                missingProviderFileBehavior: missingProviderFileBehavior,
                warningSink: warningSink));
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

    private static int ReadValueFromFolder(string folderPath)
    {
        var json = File.ReadAllText(Path.Combine(folderPath, "player.json"));
        var valueMarker = "\"Value\": ";
        var valueIndex = json.IndexOf(valueMarker, StringComparison.Ordinal);
        if (valueIndex < 0)
            throw new InvalidOperationException("Saved value was not found.");

        valueIndex += valueMarker.Length;
        var valueEnd = json.IndexOfAny(new[] { '\r', '\n', ',' }, valueIndex);
        if (valueEnd < 0)
            valueEnd = json.Length;

        return int.Parse(json.Substring(valueIndex, valueEnd - valueIndex).Trim());
    }

    private static void WriteValueToFolder(string folderPath, int value)
    {
        var filePath = Path.Combine(folderPath, "player.json");
        var json = File.ReadAllText(filePath);
        var valueMarker = "\"Value\": ";
        var valueIndex = json.IndexOf(valueMarker, StringComparison.Ordinal);
        if (valueIndex < 0)
            throw new InvalidOperationException("Saved value was not found.");

        valueIndex += valueMarker.Length;
        var valueEnd = json.IndexOfAny(new[] { '\r', '\n', ',' }, valueIndex);
        if (valueEnd < 0)
            valueEnd = json.Length;

        File.WriteAllText(filePath, json.Substring(0, valueIndex) + value + json.Substring(valueEnd));
    }

    private static void CopyDirectory(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);

        foreach (var filePath in Directory.GetFiles(sourcePath))
        {
            File.Copy(filePath, Path.Combine(destinationPath, Path.GetFileName(filePath)));
        }

        foreach (var directoryPath in Directory.GetDirectories(sourcePath))
        {
            CopyDirectory(directoryPath, Path.Combine(destinationPath, Path.GetFileName(directoryPath)));
        }
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
        public TestProvider(TestState current)
        {
            Current = current;
        }

        public string SaveKey => "player";
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
        public MigratingProvider(V2State current)
        {
            Current = current;
        }

        public string SaveKey => "player";
        public int SchemaVersion => 2;
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
            return new MigrationSource();
        }
    }

    private sealed class MigrationSource : ISaveMigrationSource
    {
        public IReadOnlyList<SaveMigrationStep> Migrations { get; } =
            new[]
            {
                SaveMigrationStep.AddIntDefault(fromVersion: 1, key: "Level", value: 12)
            };
    }
}
