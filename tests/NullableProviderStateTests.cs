using System;
using System.Collections.Generic;
using System.IO;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class NullableProviderStateTests
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
    public void SaveAndLoad_ReferenceRootNull_RoundTrips()
    {
        var writerProvider = new NullableStringProvider("player", null);
        var writer = CreateManager();
        writer.RegisterProvider(writerProvider);
        writer.ValidateRegistrations();

        writer.SaveToDisk("slot");

        var json = File.ReadAllText(Path.Combine(_tempRoot, "slot", "player.json"));
        var readerProvider = new NullableStringProvider("player", "before");
        var reader = CreateManager();
        reader.RegisterProvider(readerProvider);
        reader.ValidateRegistrations();
        var loaded = reader.LoadFromDisk("slot");

        Assert.That(json, Does.Contain("\"Data\": null"));
        Assert.That(loaded, Is.True);
        Assert.That(readerProvider.Current, Is.Null);
    }

    [Test]
    public void SaveAndLoad_NullableValueRootNull_RoundTrips()
    {
        var writer = CreateManager();
        writer.RegisterProvider(new NullableIntProvider("player", null));
        writer.ValidateRegistrations();
        writer.SaveToDisk("slot");

        var readerProvider = new NullableIntProvider("player", 99);
        var reader = CreateManager();
        reader.RegisterProvider(readerProvider);
        reader.ValidateRegistrations();
        var loaded = reader.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(readerProvider.Current, Is.Null);
    }

    [Test]
    public void SaveAndLoad_NullableValueRootValue_RoundTrips()
    {
        var writer = CreateManager();
        writer.RegisterProvider(new NullableIntProvider("player", 12));
        writer.ValidateRegistrations();
        writer.SaveToDisk("slot");

        var readerProvider = new NullableIntProvider("player", null);
        var reader = CreateManager();
        reader.RegisterProvider(readerProvider);
        reader.ValidateRegistrations();
        var loaded = reader.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(readerProvider.Current, Is.EqualTo(12));
    }

    [Test]
    public void TryLoad_NonNullableValueRootNull_ReturnsCorruptData()
    {
        var manager = CreateManager();
        var provider = new IntProvider("player", 7);
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();
        manager.SaveToDisk("slot");
        File.WriteAllText(Path.Combine(_tempRoot, "slot", "player.json"), """{"SchemaVersion":1,"Data":null}""");

        var result = manager.TryLoadFromDisk("slot");

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.CorruptData));
        Assert.That(provider.Current, Is.EqualTo(7));
    }

    [Test]
    public void Migration_CanProduceNullForCompatibleProvider()
    {
        var oldManager = CreateManager();
        oldManager.RegisterProvider(new StringProvider("player", "legacy"));
        oldManager.ValidateRegistrations();
        oldManager.SaveToDisk("slot");

        var provider = new MigratingNullableStringProvider(
            "player",
            "before",
            new[] { new SaveMigrationStep(1, (root, factory) => root.ReplaceWith(factory.CreateNull())) });
        var newManager = CreateManager();
        newManager.RegisterProvider(provider);
        newManager.ValidateRegistrations();

        var loaded = newManager.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current, Is.Null);
    }

    [Test]
    public void Migration_ProducesNullForIncompatibleProvider_FailsAsCorruptData()
    {
        var oldManager = CreateManager();
        oldManager.RegisterProvider(new IntProvider("player", 7));
        oldManager.ValidateRegistrations();
        oldManager.SaveToDisk("slot");

        var provider = new MigratingIntProvider(
            "player",
            99,
            new[] { new SaveMigrationStep(1, (root, factory) => root.ReplaceWith(factory.CreateNull())) });
        var newManager = CreateManager();
        newManager.RegisterProvider(provider);
        newManager.ValidateRegistrations();

        var result = newManager.TryLoadFromDisk("slot");

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.CorruptData));
        Assert.That(provider.Current, Is.EqualTo(99));
    }

    private SaveManager<string> CreateManager()
    {
        return new SaveManager<string>(
            new SaveSystemOptions<string>(
                saveRootPath: _tempRoot,
                serializer: new JsonSaveSerializer(),
                tempFolderName: SaveSystemOptions<string>.DefaultTempFolderName(),
                savePathResolver: identity => identity,
                fileNameResolver: SaveSystemOptions<string>.DefaultFileNameResolver));
    }

    private sealed class NullableStringProvider : ISaveProvider<string?>
    {
        public NullableStringProvider(string saveKey, string? current)
        {
            SaveKey = saveKey;
            Current = current;
        }

        public string SaveKey { get; }
        public int SchemaVersion => 1;
        public int LoadPriority => 0;
        public string? Current { get; private set; }
        public string? CaptureState() => Current;
        public void RestoreState(string? state) => Current = state;
    }

    private sealed class StringProvider : ISaveProvider<string>
    {
        public StringProvider(string saveKey, string current)
        {
            SaveKey = saveKey;
            Current = current;
        }

        public string SaveKey { get; }
        public int SchemaVersion => 1;
        public int LoadPriority => 0;
        public string Current { get; private set; }
        public string CaptureState() => Current;
        public void RestoreState(string? state) => Current = state!;
    }

    private sealed class NullableIntProvider : ISaveProvider<int?>
    {
        public NullableIntProvider(string saveKey, int? current)
        {
            SaveKey = saveKey;
            Current = current;
        }

        public string SaveKey { get; }
        public int SchemaVersion => 1;
        public int LoadPriority => 0;
        public int? Current { get; private set; }
        public int? CaptureState() => Current;
        public void RestoreState(int? state) => Current = state;
    }

    private sealed class IntProvider : ISaveProvider<int>
    {
        public IntProvider(string saveKey, int current)
        {
            SaveKey = saveKey;
            Current = current;
        }

        public string SaveKey { get; }
        public int SchemaVersion => 1;
        public int LoadPriority => 0;
        public int Current { get; private set; }
        public int CaptureState() => Current;
        public void RestoreState(int state) => Current = state;
    }

    private sealed class MigratingNullableStringProvider : ISaveProvider<string?>, ISaveMigratable
    {
        private readonly SaveMigrationStep[] _migrations;

        public MigratingNullableStringProvider(string saveKey, string? current, SaveMigrationStep[] migrations)
        {
            SaveKey = saveKey;
            Current = current;
            _migrations = migrations;
        }

        public string SaveKey { get; }
        public int SchemaVersion => 2;
        public int LoadPriority => 0;
        public string? Current { get; private set; }
        public string? CaptureState() => Current;
        public void RestoreState(string? state) => Current = state;
        public ISaveMigrationSource CreateMigrationSource() => new MigrationSource(_migrations);
    }

    private sealed class MigratingIntProvider : ISaveProvider<int>, ISaveMigratable
    {
        private readonly SaveMigrationStep[] _migrations;

        public MigratingIntProvider(string saveKey, int current, SaveMigrationStep[] migrations)
        {
            SaveKey = saveKey;
            Current = current;
            _migrations = migrations;
        }

        public string SaveKey { get; }
        public int SchemaVersion => 2;
        public int LoadPriority => 0;
        public int Current { get; private set; }
        public int CaptureState() => Current;
        public void RestoreState(int state) => Current = state;
        public ISaveMigrationSource CreateMigrationSource() => new MigrationSource(_migrations);
    }

    private sealed class MigrationSource : ISaveMigrationSource
    {
        public MigrationSource(IReadOnlyList<SaveMigrationStep> migrations)
        {
            Migrations = migrations;
        }

        public IReadOnlyList<SaveMigrationStep> Migrations { get; }
    }
}
