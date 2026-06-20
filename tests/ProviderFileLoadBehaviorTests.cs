using System;
using System.IO;
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
    public void LoadFromDisk_WithSkipMissingProviderFileBehavior_LeavesMissingProviderUnchanged()
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
    public void LoadFromDisk_ThrowsForPartialSaveFolderMissingRegisteredProviderFile()
    {
        var manager = CreateManager();
        var provider = new TestProvider("player", new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();
        Directory.CreateDirectory(Path.Combine(_tempRoot, "slot"));

        var ex = Assert.Throws<InvalidOperationException>(() => manager.LoadFromDisk("slot"));

        Assert.That(ex!.Message, Does.Contain("Missing save file"));
        Assert.That(ex.Message, Does.Contain("player"));
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

    private SaveManager<string> CreateManager(
        MissingProviderFileBehavior missingProviderFileBehavior = MissingProviderFileBehavior.Throw,
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
}
