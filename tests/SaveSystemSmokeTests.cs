using System;
using System.IO;
using System.Linq;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class SaveSystemSmokeTests
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
    public void Options_PreserveConfiguredValues()
    {
        var serializer = new JsonSaveSerializer();
        var options = CreateOptions(serializer);

        Assert.That(options.SaveRootPath, Is.EqualTo(_tempRoot));
        Assert.That(options.Serializer, Is.SameAs(serializer));
        Assert.That(options.TempFolderName, Is.EqualTo("_tmp"));
        Assert.That(options.EnableBackupSystem, Is.False);
        Assert.That(options.BackupSystemMaxBackupCount, Is.Zero);
        Assert.That(options.SaveNameResolver("slot"), Is.EqualTo("slot"));
        Assert.That(options.FileNameResolver(new SaveFileContext("player", 1, serializer.GetType())), Is.EqualTo("player"));
    }

    [Test]
    public void RegisterProvider_RejectsDuplicateSaveKey()
    {
        var manager = new SaveManager<string>(CreateOptions(new JsonSaveSerializer()));
        var first = new TestProvider("player", new TestState { Name = "A", Level = 1 });
        var second = new TestProvider("player", new TestState { Name = "B", Level = 2 });

        manager.RegisterProvider<TestState>(first);

        var ex = Assert.Throws<InvalidOperationException>(() => manager.RegisterProvider<TestState>(second));
        Assert.That(ex!.Message, Does.Contain("already registered"));
    }

    [Test]
    public void SaveAndLoad_RestoresRegisteredProviderState()
    {
        var manager = new SaveManager<string>(CreateOptions(new JsonSaveSerializer()));
        var provider = new TestProvider("player", new TestState { Name = "BeforeSave", Level = 7 });
        manager.RegisterProvider<TestState>(provider);

        manager.SaveToDisk("slot-a");
        provider.Current = new TestState { Name = "Changed", Level = 1 };

        var loaded = manager.LoadFromDisk("slot-a");

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Name, Is.EqualTo("BeforeSave"));
        Assert.That(provider.Current.Level, Is.EqualTo(7));
    }

    [Test]
    public void MemoryOnlyProvider_ParticipatesInSnapshotsButIsNotWrittenToDisk()
    {
        var manager = new SaveManager<string>(CreateOptions(new JsonSaveSerializer()));
        var provider = new TestProvider("cache", new TestState { Name = "Memory", Level = 3 });
        manager.RegisterProvider(provider);

        var snapshot = manager.CaptureSnapshot();
        manager.SaveToDisk("slot-b");
        provider.Current = new TestState { Name = "Changed", Level = 9 };

        var loaded = manager.LoadFromDisk("slot-b");

        Assert.That(snapshot.Entries.Single().SaveKey, Is.EqualTo("cache"));
        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Name, Is.EqualTo("Changed"));
        Assert.That(File.Exists(Path.Combine(_tempRoot, "slot-b", "cache.json")), Is.False);
    }

    private SaveSystemOptions<string> CreateOptions(ISaveSerializer serializer)
    {
        return new SaveSystemOptions<string>(
            saveRootPath: _tempRoot,
            serializer: serializer,
            tempFolderName: SaveSystemOptions<string>.DefaultTempFolderName(),
            saveNameResolver: identity => identity,
            fileNameResolver: SaveSystemOptions<string>.DefaultFileNameResolver);
    }

    public sealed class TestState
    {
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
    }

    private sealed class TestProvider : ISaveProvider
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
