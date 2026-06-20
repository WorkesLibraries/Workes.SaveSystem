using System;
using System.IO;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class EngineNeutralDefaultsTests
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
    public void CreateDefault_WithExplicitRoot_SavesAndLoadsFromProvidedPath()
    {
        var serializer = new JsonSaveSerializer();
        var manager = SaveManager<StringSaveIdentity>.CreateDefault(serializer, _tempRoot);
        var provider = new TestProvider("player", new TestState { Name = "Saved", Level = 4 });
        manager.RegisterProvider<TestState>(provider);

        manager.SaveToDisk(new StringSaveIdentity("slot"));
        provider.Current = new TestState { Name = "Changed", Level = 1 };

        var loaded = manager.LoadFromDisk(new StringSaveIdentity("slot"));

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Name, Is.EqualTo("Saved"));
        Assert.That(provider.Current.Level, Is.EqualTo(4));
        Assert.That(File.Exists(Path.Combine(_tempRoot, "slot", "player.json")), Is.True);
    }

    [Test]
    public void SaveSystemOptions_RejectsEmptySaveRootPath()
    {
        var ex = Assert.Throws<ArgumentException>(() => CreateOptions(string.Empty));

        Assert.That(ex!.ParamName, Is.EqualTo("saveRootPath"));
    }

    [Test]
    public void SaveSystemOptions_RejectsNullSerializer()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new SaveSystemOptions<StringSaveIdentity>(
                saveRootPath: _tempRoot,
                serializer: null!,
                tempFolderName: SaveSystemOptions<StringSaveIdentity>.DefaultTempFolderName(),
                saveNameResolver: id => id.SaveName,
                fileNameResolver: SaveSystemOptions<StringSaveIdentity>.DefaultFileNameResolver));

        Assert.That(ex!.ParamName, Is.EqualTo("serializer"));
    }

    [Test]
    public void SaveSystemOptions_RejectsNullSaveNameResolver()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new SaveSystemOptions<StringSaveIdentity>(
                saveRootPath: _tempRoot,
                serializer: new JsonSaveSerializer(),
                tempFolderName: SaveSystemOptions<StringSaveIdentity>.DefaultTempFolderName(),
                saveNameResolver: null!,
                fileNameResolver: SaveSystemOptions<StringSaveIdentity>.DefaultFileNameResolver));

        Assert.That(ex!.ParamName, Is.EqualTo("saveNameResolver"));
    }

    [Test]
    public void SaveManager_RejectsNullOptions()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new SaveManager<StringSaveIdentity>(null!));

        Assert.That(ex!.ParamName, Is.EqualTo("options"));
    }

    private SaveSystemOptions<StringSaveIdentity> CreateOptions(string saveRootPath)
    {
        return new SaveSystemOptions<StringSaveIdentity>(
            saveRootPath: saveRootPath,
            serializer: new JsonSaveSerializer(),
            tempFolderName: SaveSystemOptions<StringSaveIdentity>.DefaultTempFolderName(),
            saveNameResolver: id => id.SaveName,
            fileNameResolver: null);
    }

    private sealed class TestState
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
