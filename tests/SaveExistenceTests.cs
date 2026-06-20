using System;
using System.IO;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class SaveExistenceTests
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
    public void SaveExists_ReturnsTrueForSaveFolderWithMetadata()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);

        Assert.That(manager.SaveExists("slot"), Is.True);
    }

    [Test]
    public void SaveExists_ReturnsFalseWhenSaveIsMissingOrHasNoMetadata()
    {
        var manager = CreateManager();
        Directory.CreateDirectory(Path.Combine(_tempRoot, "metadata-less"));

        Assert.That(manager.SaveExists("missing"), Is.False);
        Assert.That(manager.SaveExists("metadata-less"), Is.False);
    }

    [Test]
    public void SaveExists_DoesNotRecoverTempFolders()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);

        Directory.Move(Path.Combine(_tempRoot, "slot"), Path.Combine(_tempRoot, "slot_tmp"));

        Assert.That(manager.SaveExists("slot"), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "slot_tmp")), Is.True);
    }

    [Test]
    public void SaveExists_DoesNotRequireRegistrationValidation()
    {
        var writer = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        writer.RegisterProvider(provider);
        SaveValue(writer, provider, "slot", 1);

        var reader = CreateManager();

        Assert.That(reader.SaveExists("slot"), Is.True);
    }

    [Test]
    public void BackupSlotExists_ReturnsTrueForBackupFolderWithMetadata()
    {
        var manager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 2);
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "slot", 2);

        Assert.That(manager.BackupSlotExists("slot", slotNumber: 1), Is.True);
    }

    [Test]
    public void BackupSlotExists_ReturnsFalseWhenBackupIsMissingOrHasNoMetadata()
    {
        var manager = CreateManager();
        Directory.CreateDirectory(Path.Combine(_tempRoot, "_backup", "slot_0001"));

        Assert.That(manager.BackupSlotExists("slot", slotNumber: 1), Is.False);
        Assert.That(manager.BackupSlotExists("slot", slotNumber: 2), Is.False);
    }

    [Test]
    public void BackupSlotExists_CanCheckWhenBackupsAreCurrentlyDisabled()
    {
        var backupPath = Path.Combine(_tempRoot, "_backup", "slot_0001");
        Directory.CreateDirectory(backupPath);
        File.WriteAllText(Path.Combine(backupPath, "savemetadata.json"), "{}");
        var manager = CreateManager();

        Assert.That(manager.BackupSlotExists("slot", slotNumber: 1), Is.True);
    }

    [Test]
    public void BackupSlotExists_RejectsInvalidSlotNumbers()
    {
        var manager = CreateManager();

        var ex = Assert.Throws<ArgumentException>(() => manager.BackupSlotExists("slot", slotNumber: 0));

        Assert.That(ex!.ParamName, Is.EqualTo("slotNumber"));
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
}
