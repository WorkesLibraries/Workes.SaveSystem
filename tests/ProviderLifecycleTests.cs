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
        var manager = new SaveManager<string>(CreateOptions());
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
        var manager = new SaveManager<string>(CreateOptions());
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
    public void RestoreSnapshot_RejectsNullSnapshot()
    {
        var manager = new SaveManager<string>(CreateOptions());

        var ex = Assert.Throws<ArgumentNullException>(() => manager.RestoreSnapshot(null!));

        Assert.That(ex!.ParamName, Is.EqualTo("snapshot"));
    }

    [Test]
    public void RestoreSnapshot_RejectsDuplicateEntriesBeforeMutatingProviders()
    {
        var log = new List<string>();
        var manager = new SaveManager<string>(CreateOptions());
        var provider = new LifecycleProvider("player", loadPriority: 0, value: 1, log);
        manager.RegisterProvider(provider);
        var snapshot = new SaveSnapshot();
        snapshot.Add("player", schemaVersion: 1, state: new TestState { Value = 10 });
        snapshot.Add("player", schemaVersion: 1, state: new TestState { Value = 20 });

        var ex = Assert.Throws<InvalidOperationException>(() => manager.RestoreSnapshot(snapshot));

        Assert.That(ex!.Message, Does.Contain("multiple entries"));
        Assert.That(provider.Current.Value, Is.EqualTo(1));
        Assert.That(log, Is.Empty);
    }

    [Test]
    public void RestoreSnapshot_RejectsUnknownProviderEntriesBeforeMutatingProviders()
    {
        var log = new List<string>();
        var manager = new SaveManager<string>(CreateOptions());
        var provider = new LifecycleProvider("player", loadPriority: 0, value: 1, log);
        manager.RegisterProvider(provider);
        var snapshot = new SaveSnapshot();
        snapshot.Add("missing", schemaVersion: 1, state: new TestState { Value = 10 });

        var ex = Assert.Throws<InvalidOperationException>(() => manager.RestoreSnapshot(snapshot));

        Assert.That(ex!.Message, Does.Contain("unregistered provider"));
        Assert.That(provider.Current.Value, Is.EqualTo(1));
        Assert.That(log, Is.Empty);
    }

    [Test]
    public void RestoreSnapshot_RejectsSchemaMismatchBeforeMutatingProviders()
    {
        var log = new List<string>();
        var manager = new SaveManager<string>(CreateOptions());
        var provider = new LifecycleProvider("player", loadPriority: 0, value: 1, log);
        manager.RegisterProvider(provider);
        var snapshot = new SaveSnapshot();
        snapshot.Add("player", schemaVersion: 2, state: new TestState { Value = 10 });

        var ex = Assert.Throws<InvalidOperationException>(() => manager.RestoreSnapshot(snapshot));

        Assert.That(ex!.Message, Does.Contain("schema version"));
        Assert.That(provider.Current.Value, Is.EqualTo(1));
        Assert.That(log, Is.Empty);
    }

    [Test]
    public void RestoreSnapshot_RejectsIncompatiblePersistedStateBeforeMutatingProviders()
    {
        var log = new List<string>();
        var manager = new SaveManager<string>(CreateOptions());
        var provider = new LifecycleProvider("player", loadPriority: 0, value: 1, log);
        manager.RegisterProvider(provider);
        var snapshot = new SaveSnapshot();
        snapshot.Add("player", schemaVersion: 1, state: "not test state");

        var ex = Assert.Throws<InvalidOperationException>(() => manager.RestoreSnapshot(snapshot));

        Assert.That(ex!.Message, Does.Contain("incompatible"));
        Assert.That(provider.Current.Value, Is.EqualTo(1));
        Assert.That(log, Is.Empty);
    }

    [Test]
    public void RestoreSnapshot_RejectsIncompatibleMemoryOnlyStateBeforeMutatingProviders()
    {
        var log = new List<string>();
        var manager = new SaveManager<string>(CreateOptions());
        var provider = new LifecycleProvider("cache", loadPriority: 0, value: 1, log);
        manager.RegisterMemoryProvider(provider);
        var snapshot = new SaveSnapshot();
        snapshot.Add("cache", schemaVersion: 1, state: "not test state");

        var ex = Assert.Throws<InvalidOperationException>(() => manager.RestoreSnapshot(snapshot));

        Assert.That(ex!.Message, Does.Contain("incompatible"));
        Assert.That(provider.Current.Value, Is.EqualTo(1));
        Assert.That(log, Is.Empty);
    }

    [Test]
    public void ValidateSnapshotForRestore_AllowsMissingRegisteredProviders()
    {
        var manager = new SaveManager<string>(CreateOptions());
        manager.RegisterProvider(new LifecycleProvider("player", loadPriority: 0, value: 1, new List<string>()));
        var snapshot = new SaveSnapshot();

        Assert.DoesNotThrow(() => manager.ValidateSnapshotForRestore(snapshot));
    }

    [Test]
    public void LoadFromDisk_ReturnsFalseWithoutAfterLoadCallbacksWhenSaveDoesNotExist()
    {
        var log = new List<string>();
        var manager = new SaveManager<string>(CreateOptions());
        manager.RegisterProvider(new LifecycleProvider("player", loadPriority: 0, value: 1, log));
        manager.ValidateRegistrations();
        log.Clear();

        var loaded = manager.LoadFromDisk("missing");

        Assert.That(loaded, Is.False);
        Assert.That(log, Is.Empty);
    }

    [Test]
    public void UnregisterProvider_RemovesProviderFromFutureSnapshots()
    {
        var log = new List<string>();
        var manager = new SaveManager<string>(CreateOptions());
        var provider = new LifecycleProvider("player", loadPriority: 0, value: 1, log);
        manager.RegisterProvider(provider);
        var removed = manager.UnregisterProvider(provider);

        var snapshot = manager.CaptureSnapshot();

        Assert.That(removed, Is.True);
        Assert.That(snapshot.Entries, Is.Empty);
        Assert.That(log, Is.Empty);
    }

    [Test]
    public void UnregisterProvider_WithDifferentInstanceSameKey_DoesNotRemoveRegisteredProvider()
    {
        var log = new List<string>();
        var manager = new SaveManager<string>(CreateOptions());
        var registered = new LifecycleProvider("player", loadPriority: 0, value: 1, log);
        var otherInstance = new LifecycleProvider("player", loadPriority: 0, value: 2, new List<string>());
        manager.RegisterProvider(registered);

        var removed = manager.UnregisterProvider(otherInstance);

        var snapshot = manager.CaptureSnapshot();
        Assert.That(removed, Is.False);
        Assert.That(snapshot.Entries.Select(entry => entry.SaveKey), Is.EqualTo(new[] { "player" }));
        Assert.That(registered.Current.Value, Is.EqualTo(1));
        Assert.That(log, Is.EqualTo(new[] { "player:before", "player:capture" }));
    }

    [Test]
    public void UnregisterProvider_BySaveKey_RemovesProviderFromFutureSnapshots()
    {
        var log = new List<string>();
        var manager = new SaveManager<string>(CreateOptions());
        var provider = new LifecycleProvider("player", loadPriority: 0, value: 1, log);
        manager.RegisterProvider(provider);

        var removed = manager.UnregisterProvider("player");

        var snapshot = manager.CaptureSnapshot();
        Assert.That(removed, Is.True);
        Assert.That(snapshot.Entries, Is.Empty);
        Assert.That(log, Is.Empty);
    }

    [Test]
    public void UnregisterProvider_BySaveKeyRejectsEmptySaveKey()
    {
        var manager = new SaveManager<string>(CreateOptions());

        var ex = Assert.Throws<ArgumentException>(() => manager.UnregisterProvider(" "));

        Assert.That(ex!.ParamName, Is.EqualTo("saveKey"));
        Assert.That(ex.Message, Does.Contain("SaveKey"));
    }

    [Test]
    public void UnregisterProvider_RequiresRegistrationValidationBeforeNextDiskOperation()
    {
        var log = new List<string>();
        var manager = new SaveManager<string>(CreateOptions());
        var provider = new LifecycleProvider("player", loadPriority: 0, value: 1, log);
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();

        manager.UnregisterProvider(provider);

        var ex = Assert.Throws<InvalidOperationException>(() => manager.SaveToDisk("slot"));
        Assert.That(ex!.Message, Does.Contain("ValidateRegistrations"));
    }

    [Test]
    public void RegisterProvider_RejectsEmptySaveKeys()
    {
        var manager = new SaveManager<string>(CreateOptions());
        var provider = new LifecycleProvider(string.Empty, loadPriority: 0, value: 1, new List<string>());

        var ex = Assert.Throws<ArgumentException>(() => manager.RegisterProvider(provider));

        Assert.That(ex!.ParamName, Is.EqualTo("provider"));
        Assert.That(ex.Message, Does.Contain("SaveKey"));
    }

    private SaveSystemOptions<string> CreateOptions()
    {
        return new SaveSystemOptions<string>(
            saveRootPath: _tempRoot,
            serializer: new JsonSaveSerializer(),
            tempFolderName: SaveSystemOptions<string>.DefaultTempFolderName(),
            saveNameResolver: identity => identity,
            fileNameResolver: SaveSystemOptions<string>.DefaultFileNameResolver);
    }

    public sealed class TestState
    {
        public int Value { get; set; }
    }

    private sealed class LifecycleProvider : ISaveProvider<TestState>, ISaveLifecycle
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

        public TestState CaptureState()
        {
            _log.Add($"{SaveKey}:capture");
            return Current;
        }

        public void RestoreState(TestState state)
        {
            Current = state;
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
