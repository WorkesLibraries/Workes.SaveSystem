using System;
using System.IO;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class SaveDeletionTests
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
    public void DeleteSave_DeletesMainSaveAndLeavesBackups()
    {
        var manager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 2);
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "slot", 2);

        var deleted = manager.DeleteSave("slot");

        Assert.That(deleted, Is.True);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "slot")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "slot_0001")), Is.True);
    }

    [Test]
    public void DeleteSave_ReturnsFalseWhenNoSaveOrArtifactsExist()
    {
        var manager = CreateManager();

        var deleted = manager.DeleteSave("missing");

        Assert.That(deleted, Is.False);
    }

    [Test]
    public void DeleteSave_RemovesTempAndToDeleteArtifacts()
    {
        var manager = CreateManager();
        Directory.CreateDirectory(Path.Combine(_tempRoot, "slot_tmp"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "slot_toDelete"));

        var deleted = manager.DeleteSave("slot");

        Assert.That(deleted, Is.True);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "slot_tmp")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "slot_toDelete")), Is.False);
    }

    [Test]
    public void DeleteBackupSlot_DeletesRequestedBackupOnly()
    {
        var manager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 2);
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "slot", 2);
        SaveValue(manager, provider, "slot", 3);

        var deleted = manager.DeleteBackupSlot("slot", slotNumber: 1);

        Assert.That(deleted, Is.True);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "slot_0001")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "slot_0002")), Is.True);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "slot")), Is.True);
    }

    [Test]
    public void DeleteBackupSlot_CanDeleteWhenBackupsAreCurrentlyDisabled()
    {
        var backupPath = Path.Combine(_tempRoot, "_backup", "slot_0001");
        Directory.CreateDirectory(backupPath);
        var manager = CreateManager();

        var deleted = manager.DeleteBackupSlot("slot", slotNumber: 1);

        Assert.That(deleted, Is.True);
        Assert.That(Directory.Exists(backupPath), Is.False);
    }

    [Test]
    public void DeleteBackupSlot_RejectsInvalidSlotNumbers()
    {
        var manager = CreateManager();

        var ex = Assert.Throws<ArgumentException>(() => manager.DeleteBackupSlot("slot", slotNumber: 0));

        Assert.That(ex!.ParamName, Is.EqualTo("slotNumber"));
    }

    [Test]
    public void DeleteAllBackupSlots_DeletesAllNumberedBackupsAndLeavesMainSave()
    {
        var manager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 3);
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "slot", 2);
        SaveValue(manager, provider, "slot", 3);
        SaveValue(manager, provider, "slot", 4);

        var deleted = manager.DeleteAllBackupSlots("slot");

        Assert.That(deleted, Is.EqualTo(3));
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "slot")), Is.True);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "slot_0001")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "slot_0002")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "slot_0003")), Is.False);
    }

    [Test]
    public void DeleteAllBackupSlots_CanDeleteWhenBackupsAreCurrentlyDisabled()
    {
        var backupPath = Path.Combine(_tempRoot, "_backup", "slot_0001");
        Directory.CreateDirectory(backupPath);
        var manager = CreateManager();

        var deleted = manager.DeleteAllBackupSlots("slot");

        Assert.That(deleted, Is.EqualTo(1));
        Assert.That(Directory.Exists(backupPath), Is.False);
    }

    [Test]
    public void DeleteAllBackupSlots_ReturnsZeroWhenNoMatchingBackupsExist()
    {
        var manager = CreateManager();

        var deleted = manager.DeleteAllBackupSlots("missing");

        Assert.That(deleted, Is.EqualTo(0));
    }

    [Test]
    public void DeleteAllBackupSlots_RejectsInvalidIdentity()
    {
        var manager = CreateManager();

        Assert.Throws<ArgumentNullException>(() => manager.DeleteAllBackupSlots(null!));
        Assert.Throws<InvalidOperationException>(() => manager.DeleteAllBackupSlots("../slot"));
    }

    [Test]
    public void DeleteAllBackupSlots_SupportsNestedResolvedSavePaths()
    {
        var manager = CreateManager(
            enableBackupSystem: true,
            backupSystemMaxBackupCount: 2,
            savePathResolver: identity => Path.Combine("profile-a", identity));
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "slot", 2);
        SaveValue(manager, provider, "slot", 3);

        var deleted = manager.DeleteAllBackupSlots("slot");

        Assert.That(deleted, Is.EqualTo(2));
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "profile-a", "slot")), Is.True);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "profile-a", "slot_0001")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "profile-a", "slot_0002")), Is.False);
    }

    [Test]
    public void DeleteAllBackupSlots_IgnoresMalformedAndUnrelatedBackupFolders()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, "_backup", "slot_0001"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "_backup", "slot_extra"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "_backup", "slot_00001"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "_backup", "slot_00x1"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "_backup", "slot2_0001"));
        var manager = CreateManager();

        var deleted = manager.DeleteAllBackupSlots("slot");

        Assert.That(deleted, Is.EqualTo(1));
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "slot_0001")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "slot_extra")), Is.True);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "slot_00001")), Is.True);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "slot_00x1")), Is.True);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "slot2_0001")), Is.True);
    }

    private SaveManager<string> CreateManager(
        bool enableBackupSystem = false,
        int backupSystemMaxBackupCount = 0,
        Func<string, string>? savePathResolver = null)
    {
        return new SaveManager<string>(
            new SaveSystemOptions<string>(
                saveRootPath: _tempRoot,
                serializer: new JsonSaveSerializer(),
                tempFolderName: SaveSystemOptions<string>.DefaultTempFolderName(),
                savePathResolver: savePathResolver ?? (identity => identity),
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
