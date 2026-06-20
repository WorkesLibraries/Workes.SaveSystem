using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class ProviderLifecycleTests
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
    public void CaptureSnapshot_CapturesProvidersInLoadPriorityOrder()
    {
        var log = new List<string>();
        var manager = new SaveManager<StringSaveIdentity>(CreateOptions());
        var late = new LifecycleProvider("late", loadPriority: 20, value: 2, log);
        var early = new LifecycleProvider("early", loadPriority: -10, value: 1, log);
        manager.RegisterProvider(late);
        manager.RegisterProvider(early);

        var snapshot = manager.CaptureSnapshot();

        Assert.That(snapshot.Entries.Select(entry => entry.SaveKey), Is.EqualTo(new[] { "early", "late" }));
        Assert.That(log, Is.EqualTo(new[]
        {
            "early:before",
            "early:capture",
            "late:before",
            "late:capture"
        }));
    }

    [Test]
    public void RestoreSnapshot_RestoresEntriesInSnapshotPriorityOrderThenRunsAfterLoadCallbacks()
    {
        var log = new List<string>();
        var manager = new SaveManager<StringSaveIdentity>(CreateOptions());
        var late = new LifecycleProvider("late", loadPriority: 20, value: 0, log);
        var early = new LifecycleProvider("early", loadPriority: -10, value: 0, log);
        manager.RegisterProvider(late);
        manager.RegisterProvider(early);
        var snapshot = new SaveSnapshot();
        snapshot.Add("late", schemaVersion: 1, state: new TestState { Value = 20 }, loadPriority: 20);
        snapshot.Add("early", schemaVersion: 1, state: new TestState { Value = 10 }, loadPriority: -10);

        manager.RestoreSnapshot(snapshot);

        Assert.That(early.Current.Value, Is.EqualTo(10));
        Assert.That(late.Current.Value, Is.EqualTo(20));
        Assert.That(log.Take(2), Is.EqualTo(new[] { "early:restore:10", "late:restore:20" }));
        Assert.That(log.Skip(2), Does.Contain("early:after"));
        Assert.That(log.Skip(2), Does.Contain("late:after"));
    }

    [Test]
    public void LoadFromDisk_ReturnsFalseWithoutAfterLoadCallbacksWhenSaveDoesNotExist()
    {
        var log = new List<string>();
        var manager = new SaveManager<StringSaveIdentity>(CreateOptions());
        manager.RegisterProvider<TestState>(new LifecycleProvider("player", loadPriority: 0, value: 1, log));
        log.Clear();

        var loaded = manager.LoadFromDisk(new StringSaveIdentity("missing"));

        Assert.That(loaded, Is.False);
        Assert.That(log, Is.Empty);
    }

    [Test]
    public void UnregisterProvider_RemovesProviderFromFutureSnapshots()
    {
        var log = new List<string>();
        var manager = new SaveManager<StringSaveIdentity>(CreateOptions());
        var provider = new LifecycleProvider("player", loadPriority: 0, value: 1, log);
        manager.RegisterProvider(provider);
        manager.UnregisterProvider(provider);

        var snapshot = manager.CaptureSnapshot();

        Assert.That(snapshot.Entries, Is.Empty);
        Assert.That(log, Is.Empty);
    }

    [Test]
    public void RegisterProvider_RejectsEmptySaveKeys()
    {
        var manager = new SaveManager<StringSaveIdentity>(CreateOptions());
        var provider = new LifecycleProvider(string.Empty, loadPriority: 0, value: 1, new List<string>());

        var ex = Assert.Throws<ArgumentException>(() => manager.RegisterProvider<TestState>(provider));

        Assert.That(ex!.ParamName, Is.EqualTo("provider"));
        Assert.That(ex.Message, Does.Contain("SaveKey"));
    }

    private SaveSystemOptions<StringSaveIdentity> CreateOptions()
    {
        return new SaveSystemOptions<StringSaveIdentity>(
            saveRootPath: _tempRoot,
            serializer: new JsonSaveSerializer(),
            tempFolderName: SaveSystemOptions<StringSaveIdentity>.DefaultTempFolderName(),
            saveNameResolver: identity => identity.SaveName,
            fileNameResolver: SaveSystemOptions<StringSaveIdentity>.DefaultFileNameResolver);
    }

    public sealed class TestState
    {
        public int Value { get; set; }
    }

    private sealed class LifecycleProvider : ISaveProvider, ISaveLifecycle
    {
        private readonly List<string> _log;

        public LifecycleProvider(string saveKey, int loadPriority, int value, List<string> log)
        {
            SaveKey = saveKey;
            LoadPriority = loadPriority;
            Current = new TestState { Value = value };
            _log = log;
        }

        public string SaveKey { get; }
        public int SchemaVersion => 1;
        public int LoadPriority { get; }
        public TestState Current { get; private set; }

        public object CaptureState()
        {
            _log.Add($"{SaveKey}:capture");
            return Current;
        }

        public void RestoreState(object state)
        {
            Current = (TestState)state;
            _log.Add($"{SaveKey}:restore:{Current.Value}");
        }

        public void OnBeforeSave()
        {
            _log.Add($"{SaveKey}:before");
        }

        public void OnAfterLoad()
        {
            _log.Add($"{SaveKey}:after");
        }
    }
}
