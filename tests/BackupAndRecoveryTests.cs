using System;
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
        manager.RegisterProvider<TestState>(provider);

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
        manager.RegisterProvider<TestState>(provider);
        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "slot", 2);
        SaveValue(manager, provider, "slot", 3);
        provider.Current = new TestState { Value = 99 };

        var loaded = manager.LoadBackupSlotFromDisk("slot", slotNumber: 1);

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Value, Is.EqualTo(2));
    }

    [Test]
    public void LoadBackupSlotFromDisk_ReturnsFalseWhenBackupsAreDisabled()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider<TestState>(provider);

        var loaded = manager.LoadBackupSlotFromDisk("slot", slotNumber: 1);

        Assert.That(loaded, Is.False);
    }

    [Test]
    public void SaveToDisk_WithBackups_NormalizesGappedBackupSlotsBeforeRotation()
    {
        var manager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 3);
        var provider = new TestProvider(new TestState { Value = 0 });
        manager.RegisterProvider<TestState>(provider);
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
        manager.RegisterProvider<TestState>(provider);
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
        manager.RegisterProvider<TestState>(provider);
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
    public void RecoverSave_CompletesInterruptedSwapWhenMainIsMissing()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider<TestState>(provider);
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
    public void RecoverSave_PromotesTempFolderWhenMainAndToDeleteAreMissing()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider<TestState>(provider);
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
    public void RecoverSave_CompletesSwapWhenMainAndTempBothExist()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider<TestState>(provider);
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
    public void RecoverSave_CleansToDeleteFolderWhenPromotedMainAlreadyExists()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider<TestState>(provider);
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
        manager.RegisterProvider<TestState>(provider);
        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "other", 2);

        Directory.Move(Path.Combine(_tempRoot, "other"), Path.Combine(_tempRoot, "slot_tmp"));

        var ex = Assert.Throws<InvalidOperationException>(() => manager.RecoverSave("slot"));

        Assert.That(ex!.Message, Does.Contain("SaveId mismatch"));
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
                saveNameResolver: identity => identity,
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

    private sealed class TestProvider : ISaveProvider
    {
        public TestProvider(TestState current)
        {
            Current = current;
        }

        public string SaveKey => "player";
        public int SchemaVersion => 1;
        public int LoadPriority => 0;
        public TestState Current { get; set; }

        public object CaptureState()
        {
            return Current;
        }

        public void RestoreState(object state)
        {
            Current = (TestState)state;
        }
    }
}
