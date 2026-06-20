using System;
using System.IO;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class ReadmeExampleTests
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
    public void QuickStart_SaveAndLoadSlot_RestoresProviderState()
    {
        var serializer = new JsonSaveSerializer();
        var manager = SaveManager<string>.CreateDefault(
            serializer,
            saveRootPath: _tempRoot);

        var playerProvider = new PlayerSaveProvider();
        manager.RegisterProvider<PlayerState>(playerProvider);
        manager.ValidateRegistrations();

        manager.SaveToDisk("slot-1");

        playerProvider.Current = new PlayerState();
        var loaded = manager.LoadFromDisk("slot-1");

        Assert.That(loaded, Is.True);
        Assert.That(playerProvider.Current.Name, Is.EqualTo("Rook"));
        Assert.That(playerProvider.Current.Level, Is.EqualTo(5));
        Assert.That(File.Exists(Path.Combine(_tempRoot, "slot-1", "player.json")), Is.True);
    }

    [Test]
    public void BackupExample_LoadsMostRecentPreviousSave()
    {
        var options = new SaveSystemOptions<string>(
            saveRootPath: _tempRoot,
            serializer: new JsonSaveSerializer(),
            tempFolderName: SaveSystemOptions<string>.DefaultTempFolderName(),
            saveNameResolver: identity => identity,
            fileNameResolver: SaveSystemOptions<string>.DefaultFileNameResolver,
            enableBackupSystem: true,
            backupSystemMaxBackupCount: 3);

        var manager = new SaveManager<string>(options);
        var playerProvider = new PlayerSaveProvider();
        manager.RegisterProvider<PlayerState>(playerProvider);
        manager.ValidateRegistrations();
        manager.SaveToDisk("slot-1");
        playerProvider.Current = new PlayerState { Name = "Later", Level = 8 };
        manager.SaveToDisk("slot-1");
        playerProvider.Current = new PlayerState { Name = "Changed", Level = 1 };

        var loaded = manager.LoadBackupSlotFromDisk("slot-1", slotNumber: 1);

        Assert.That(loaded, Is.True);
        Assert.That(playerProvider.Current.Name, Is.EqualTo("Rook"));
        Assert.That(playerProvider.Current.Level, Is.EqualTo(5));
    }

    public sealed class PlayerState
    {
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
    }

    public sealed class PlayerSaveProvider : ISaveProvider
    {
        public string SaveKey => "player";
        public int SchemaVersion => 1;
        public int LoadPriority => 0;

        public PlayerState Current { get; set; } = new PlayerState
        {
            Name = "Rook",
            Level = 5
        };

        public object CaptureState()
        {
            return Current;
        }

        public void RestoreState(object state)
        {
            Current = (PlayerState)state;
        }
    }
}
