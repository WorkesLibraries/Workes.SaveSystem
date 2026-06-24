using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class ApplicationMetadataTests
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
    public void SaveToDisk_WritesApplicationMetadataAndReadApisExposeIt()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        var metadataProvider = new MenuMetadataProvider(new MenuMetadata { CharacterName = "Scout", PlaytimeSeconds = 90 });
        manager.RegisterProvider(provider);
        manager.RegisterMetadataProvider(metadataProvider);
        manager.ValidateRegistrations();

        manager.SaveToDisk("slot");

        var info = manager.ReadSaveMetadata("slot")!;
        var metadata = manager.ReadApplicationMetadata<MenuMetadata>("slot");
        var rawMetadata = ReadMetadataData("slot");

        Assert.That(info.HasApplicationMetadata, Is.True);
        Assert.That(info.ApplicationMetadataSchemaVersion, Is.EqualTo(1));
        Assert.That(metadata, Is.Not.Null);
        Assert.That(metadata!.CharacterName, Is.EqualTo("Scout"));
        Assert.That(metadata.PlaytimeSeconds, Is.EqualTo(90));
        var applicationMetadata = rawMetadata["ApplicationMetadata"]!;
        Assert.That(applicationMetadata, Is.Not.Null);
        Assert.That(applicationMetadata["SchemaVersion"]!.Value<int>(), Is.EqualTo(1));
        Assert.That(applicationMetadata["Data"]!.Type, Is.EqualTo(JTokenType.Object));
        Assert.That(applicationMetadata["Data"]!["CharacterName"]!.Value<string>(), Is.EqualTo("Scout"));
        Assert.That(applicationMetadata["Data"]!["PlaytimeSeconds"]!.Value<int>(), Is.EqualTo(90));
    }

    [Test]
    public void LoadFromDisk_RestoresApplicationMetadataAfterProviders()
    {
        var writer = CreateManager();
        writer.RegisterProvider(new TestProvider(new TestState { Value = 7 }));
        writer.RegisterMetadataProvider(new MenuMetadataProvider(new MenuMetadata { CharacterName = "Scout", PlaytimeSeconds = 12 }));
        writer.ValidateRegistrations();
        writer.SaveToDisk("slot");

        var readerMetadata = new MenuMetadataProvider(new MenuMetadata { CharacterName = "Before", PlaytimeSeconds = 0 });
        var reader = CreateManager();
        reader.RegisterProvider(new TestProvider(new TestState { Value = 0 }));
        reader.RegisterMetadataProvider(readerMetadata);
        reader.ValidateRegistrations();

        var loaded = reader.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(readerMetadata.Current.CharacterName, Is.EqualTo("Scout"));
        Assert.That(readerMetadata.Current.PlaytimeSeconds, Is.EqualTo(12));
    }

    [Test]
    public void SaveToDisk_WithoutMetadataProvider_OmitsApplicationMetadataAndClearsStaleSection()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        manager.RegisterMetadataProvider(new MenuMetadataProvider(new MenuMetadata { CharacterName = "Scout", PlaytimeSeconds = 1 }));
        manager.ValidateRegistrations();
        manager.SaveToDisk("slot");

        Assert.That(manager.UnregisterMetadataProvider(), Is.True);
        manager.ValidateRegistrations();
        manager.SaveToDisk("slot");

        var info = manager.ReadSaveMetadata("slot")!;
        var rawMetadata = ReadMetadataData("slot");
        Assert.That(info.HasApplicationMetadata, Is.False);
        Assert.That(info.ApplicationMetadataSchemaVersion, Is.Null);
        Assert.That(rawMetadata["ApplicationMetadata"]!.Type, Is.EqualTo(JTokenType.Null));
    }

    [Test]
    public void ReadBackupApplicationMetadata_ReturnsTypedMetadata()
    {
        var manager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 2);
        var provider = new TestProvider(new TestState { Value = 1 });
        var metadataProvider = new MenuMetadataProvider(new MenuMetadata { CharacterName = "First", PlaytimeSeconds = 1 });
        manager.RegisterProvider(provider);
        manager.RegisterMetadataProvider(metadataProvider);
        manager.ValidateRegistrations();
        manager.SaveToDisk("slot");
        metadataProvider.Current = new MenuMetadata { CharacterName = "Second", PlaytimeSeconds = 2 };
        manager.SaveToDisk("slot");

        var backupMetadata = manager.ReadBackupApplicationMetadata<MenuMetadata>("slot", slotNumber: 1);

        Assert.That(backupMetadata, Is.Not.Null);
        Assert.That(backupMetadata!.CharacterName, Is.EqualTo("First"));
        Assert.That(backupMetadata.PlaytimeSeconds, Is.EqualTo(1));
    }

    [Test]
    public void ValidateRegistrations_RejectsDuplicateMetadataProvider()
    {
        var manager = CreateManager();
        manager.RegisterMetadataProvider(new MenuMetadataProvider(new MenuMetadata()));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            manager.RegisterMetadataProvider(new MenuMetadataProvider(new MenuMetadata())));

        Assert.That(ex!.Message, Does.Contain("already registered"));
    }

    [Test]
    public void ValidateRegistrations_RejectsInvalidMetadataSchemaVersion()
    {
        var manager = CreateManager();
        manager.RegisterMetadataProvider(new MenuMetadataProvider(new MenuMetadata(), schemaVersion: 0));

        var ex = Assert.Throws<InvalidOperationException>(() => manager.ValidateRegistrations());

        Assert.That(ex!.Message, Does.Contain("MetadataSchemaVersion"));
    }

    [Test]
    public void RegisterProvider_RejectsReservedApplicationMetadataSaveKey()
    {
        var manager = CreateManager();

        var ex = Assert.Throws<ArgumentException>(() =>
            manager.RegisterProvider(new TestProvider(new TestState { Value = 1 }, "__workes_application_metadata")));

        Assert.That(ex!.Message, Does.Contain("reserved"));
    }

    [Test]
    public void ValidateSave_ReturnsCorruptDataWhenApplicationMetadataPayloadIsCorrupt()
    {
        var manager = CreateManager();
        manager.RegisterProvider(new TestProvider(new TestState { Value = 1 }));
        manager.RegisterMetadataProvider(new MenuMetadataProvider(new MenuMetadata { CharacterName = "Scout", PlaytimeSeconds = 1 }));
        manager.ValidateRegistrations();
        manager.SaveToDisk("slot");
        SetApplicationMetadataData("slot", "not-valid-base64");

        var result = manager.ValidateSave("slot");

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.CorruptData));
    }

    [Test]
    public void ValidateRegistrations_RejectsApplicationMetadataWhenSerializerDoesNotSupportInlineMetadata()
    {
        var manager = CreateManager(serializer: new MetadataBlindJsonSerializer());
        manager.RegisterProvider(new TestProvider(new TestState { Value = 1 }));
        manager.RegisterMetadataProvider(new MenuMetadataProvider(new MenuMetadata { CharacterName = "Scout", PlaytimeSeconds = 1 }));

        var ex = Assert.Throws<InvalidOperationException>(() => manager.ValidateRegistrations());

        Assert.That(ex!.Message, Does.Contain(nameof(ISaveApplicationMetadataSerializer)));
    }

    [Test]
    public void CompressedSerializer_ForwardsInlineApplicationMetadataSupport()
    {
        var manager = CreateManager(serializer: new CompressedSaveSerializer(new JsonSaveSerializer()));
        var metadataProvider = new MenuMetadataProvider(new MenuMetadata { CharacterName = "Scout", PlaytimeSeconds = 1 });
        manager.RegisterProvider(new TestProvider(new TestState { Value = 1 }));
        manager.RegisterMetadataProvider(metadataProvider);
        manager.ValidateRegistrations();

        manager.SaveToDisk("slot");

        var metadata = manager.ReadApplicationMetadata<MenuMetadata>("slot");
        Assert.That(metadata, Is.Not.Null);
        Assert.That(metadata!.CharacterName, Is.EqualTo("Scout"));
    }

    [Test]
    public void TransformedSerializer_ForwardsInlineApplicationMetadataSupport()
    {
        var manager = CreateManager(serializer: new TransformedSaveSerializer(new JsonSaveSerializer(), new NoOpPayloadTransform()));
        var metadataProvider = new MenuMetadataProvider(new MenuMetadata { CharacterName = "Scout", PlaytimeSeconds = 1 });
        manager.RegisterProvider(new TestProvider(new TestState { Value = 1 }));
        manager.RegisterMetadataProvider(metadataProvider);
        manager.ValidateRegistrations();

        manager.SaveToDisk("slot");

        var metadata = manager.ReadApplicationMetadata<MenuMetadata>("slot");
        Assert.That(metadata, Is.Not.Null);
        Assert.That(metadata!.CharacterName, Is.EqualTo("Scout"));
    }

    [Test]
    public void ApplicationMetadata_RoundTripsRootShapesAsInlineJsonData()
    {
        AssertInlineMetadataData(
            new ListMetadataProvider<MenuItem>(
                schemaVersion: 1,
                current: new List<MenuItem> { new MenuItem { Name = "Potion", Count = 2 } }),
            JTokenType.Array);
        AssertInlineMetadataData(
            new DictionaryMetadataProvider(
                schemaVersion: 1,
                current: new Dictionary<string, MenuItem>
                {
                    ["potion"] = new MenuItem { Name = "Potion", Count = 2 }
                }),
            JTokenType.Object);
        AssertInlineMetadataData(
            new StringMetadataProvider(schemaVersion: 1, current: "menu-label"),
            JTokenType.String);
        AssertInlineMetadataData(
            new StringMetadataProvider(schemaVersion: 1, current: null),
            JTokenType.Null);
    }

    [Test]
    public void ApplicationMetadata_MigratesObjectRoot()
    {
        var writer = CreateManager();
        writer.RegisterProvider(new TestProvider(new TestState { Value = 1 }));
        writer.RegisterMetadataProvider(new LegacyMenuMetadataProvider(new LegacyMenuMetadata { CharacterName = "Scout" }));
        writer.ValidateRegistrations();
        writer.SaveToDisk("slot");

        var readerMetadata = new MigratingMenuMetadataProvider(
            schemaVersion: 2,
            current: new MenuMetadata(),
            migrations: new[]
            {
                new SaveMigrationStep(1, (root, factory) => root.Set("PlaytimeSeconds", factory.CreateInt(60)))
            });
        var reader = CreateManager();
        reader.RegisterProvider(new TestProvider(new TestState { Value = 0 }));
        reader.RegisterMetadataProvider(readerMetadata);
        reader.ValidateRegistrations();

        var loaded = reader.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(readerMetadata.Current.CharacterName, Is.EqualTo("Scout"));
        Assert.That(readerMetadata.Current.PlaytimeSeconds, Is.EqualTo(60));
    }

    [Test]
    public void ApplicationMetadata_MigratesListRoot()
    {
        var writer = CreateManager();
        writer.RegisterProvider(new TestProvider(new TestState { Value = 1 }));
        writer.RegisterMetadataProvider(new ListMetadataProvider<LegacyMenuItem>(
            schemaVersion: 1,
            current: new List<LegacyMenuItem> { new LegacyMenuItem { Name = "Potion" } }));
        writer.ValidateRegistrations();
        writer.SaveToDisk("slot");

        var readerMetadata = new MigratingListMetadataProvider<MenuItem>(
            schemaVersion: 2,
            current: new List<MenuItem>(),
            migrations: new[]
            {
                new SaveMigrationStep(1, (root, factory) =>
                {
                    for (var i = 0; i < root.Count; i++)
                    {
                        var item = root.GetAt(i);
                        item.Set("Count", factory.CreateInt(1));
                    }
                })
            });
        var reader = CreateManager();
        reader.RegisterProvider(new TestProvider(new TestState { Value = 0 }));
        reader.RegisterMetadataProvider(readerMetadata);
        reader.ValidateRegistrations();

        reader.LoadFromDisk("slot");

        Assert.That(readerMetadata.Current, Has.Count.EqualTo(1));
        Assert.That(readerMetadata.Current[0].Name, Is.EqualTo("Potion"));
        Assert.That(readerMetadata.Current[0].Count, Is.EqualTo(1));
    }

    [Test]
    public void ApplicationMetadata_MigratesDictionaryRoot()
    {
        var writer = CreateManager();
        writer.RegisterProvider(new TestProvider(new TestState { Value = 1 }));
        writer.RegisterMetadataProvider(new DictionaryMetadataProvider(
            schemaVersion: 1,
            current: new Dictionary<string, MenuItem>
            {
                ["old-key"] = new MenuItem { Name = "Potion", Count = 1 }
            }));
        writer.ValidateRegistrations();
        writer.SaveToDisk("slot");

        var readerMetadata = new MigratingDictionaryMetadataProvider(
            schemaVersion: 2,
            current: new Dictionary<string, MenuItem>(),
            migrations: new[]
            {
                new SaveMigrationStep(1, (root, _) =>
                {
                    var value = root.Get("old-key");
                    root.Remove("old-key");
                    root.Set("new-key", value);
                })
            });
        var reader = CreateManager();
        reader.RegisterProvider(new TestProvider(new TestState { Value = 0 }));
        reader.RegisterMetadataProvider(readerMetadata);
        reader.ValidateRegistrations();

        reader.LoadFromDisk("slot");

        Assert.That(readerMetadata.Current.ContainsKey("new-key"), Is.True);
        Assert.That(readerMetadata.Current["new-key"].Name, Is.EqualTo("Potion"));
    }

    [Test]
    public void ApplicationMetadata_MigratesPrimitiveRootToNull()
    {
        var writer = CreateManager();
        writer.RegisterProvider(new TestProvider(new TestState { Value = 1 }));
        writer.RegisterMetadataProvider(new StringMetadataProvider(schemaVersion: 1, current: "legacy"));
        writer.ValidateRegistrations();
        writer.SaveToDisk("slot");

        var readerMetadata = new MigratingStringMetadataProvider(
            schemaVersion: 2,
            current: "before",
            migrations: new[]
            {
                new SaveMigrationStep(1, (root, factory) => root.ReplaceWith(factory.CreateNull()))
            });
        var reader = CreateManager();
        reader.RegisterProvider(new TestProvider(new TestState { Value = 0 }));
        reader.RegisterMetadataProvider(readerMetadata);
        reader.ValidateRegistrations();

        reader.LoadFromDisk("slot");

        Assert.That(readerMetadata.Current, Is.Null);
    }

    [Test]
    public void TryLoadFromDisk_ReturnsMigrationFailedWhenApplicationMetadataMigrationIsMissing()
    {
        var writer = CreateManager();
        writer.RegisterProvider(new TestProvider(new TestState { Value = 1 }));
        writer.RegisterMetadataProvider(new LegacyMenuMetadataProvider(new LegacyMenuMetadata { CharacterName = "Scout" }));
        writer.ValidateRegistrations();
        writer.SaveToDisk("slot");

        var reader = CreateManager();
        reader.RegisterProvider(new TestProvider(new TestState { Value = 0 }));
        reader.RegisterMetadataProvider(new MenuMetadataProvider(new MenuMetadata(), schemaVersion: 2));
        reader.ValidateRegistrations();

        var result = reader.TryLoadFromDisk("slot");

        Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.MigrationFailed));
    }

    private SaveManager<string> CreateManager(
        bool enableBackupSystem = false,
        int backupSystemMaxBackupCount = 0,
        ISaveSerializer? serializer = null)
    {
        return new SaveManager<string>(
            new SaveSystemOptions<string>(
                saveRootPath: _tempRoot,
                serializer: serializer ?? new JsonSaveSerializer(),
                tempFolderName: SaveSystemOptions<string>.DefaultTempFolderName(),
                savePathResolver: identity => identity,
                fileNameResolver: SaveSystemOptions<string>.DefaultFileNameResolver,
                enableBackupSystem: enableBackupSystem,
                backupSystemMaxBackupCount: backupSystemMaxBackupCount));
    }

    private void AssertInlineMetadataData<TMetadata>(
        ISaveMetadataProvider<TMetadata> metadataProvider,
        JTokenType expectedTokenType)
    {
        var slot = Guid.NewGuid().ToString("N");
        var manager = CreateManager();
        manager.RegisterProvider(new TestProvider(new TestState { Value = 1 }));
        manager.RegisterMetadataProvider(metadataProvider);
        manager.ValidateRegistrations();

        manager.SaveToDisk(slot);

        var rawMetadata = ReadMetadataData(slot);
        Assert.That(rawMetadata["ApplicationMetadata"]!["Data"]!.Type, Is.EqualTo(expectedTokenType));
    }

    private JObject ReadMetadataData(string slot)
    {
        var metadataPath = Path.Combine(_tempRoot, slot, "metadata.json");
        var envelope = JObject.Parse(File.ReadAllText(metadataPath));
        return (JObject)envelope["Data"]!;
    }

    private void SetApplicationMetadataData(string slot, string data)
    {
        var metadataPath = Path.Combine(_tempRoot, slot, "metadata.json");
        var envelope = JObject.Parse(File.ReadAllText(metadataPath));
        envelope["Data"]!["ApplicationMetadata"]!["Data"] = data;
        File.WriteAllText(metadataPath, envelope.ToString());
    }

    private sealed class NoOpPayloadTransform : ISavePayloadTransform
    {
        public string FileExtensionSuffix => ".noop";
        public byte[] Encode(byte[] data) => data;
        public byte[] Decode(byte[] data) => data;
    }

    private sealed class MetadataBlindJsonSerializer : ISaveSerializer
    {
        private readonly JsonSaveSerializer _inner = new JsonSaveSerializer();

        public string FileExtension => _inner.FileExtension;
        public ISaveMigrationCapableSerializer? Migration => _inner.Migration;
        public ISaveSerializerMetadataHandler? Metadata => _inner.Metadata;
        public ISaveSchematic CreateSchematic(Type stateType) => _inner.CreateSchematic(stateType);
        public byte[] Serialize(object data, ISaveSchematic schematic) => _inner.Serialize(data, schematic);
        public object? Deserialize(byte[] rawData, ISaveSchematic schematic) => _inner.Deserialize(rawData, schematic);
        public int ExtractSchemaVersion(byte[] serializedData) => _inner.ExtractSchemaVersion(serializedData);
    }

    private sealed class TestState
    {
        public int Value { get; set; }
    }

    private sealed class TestProvider : ISaveProvider<TestState>
    {
        public TestProvider(TestState current, string saveKey = "player")
        {
            Current = current;
            SaveKey = saveKey;
        }

        public string SaveKey { get; }
        public int SchemaVersion => 1;
        public int LoadPriority => 0;
        public TestState Current { get; set; }
        public TestState CaptureState() => Current;
        public void RestoreState(TestState state) => Current = state;
    }

    private sealed class MenuMetadata
    {
        public string CharacterName { get; set; } = string.Empty;
        public int PlaytimeSeconds { get; set; }
    }

    private sealed class LegacyMenuMetadata
    {
        public string CharacterName { get; set; } = string.Empty;
    }

    private sealed class LegacyMenuItem
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class MenuItem
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    private sealed class MenuMetadataProvider : ISaveMetadataProvider<MenuMetadata>
    {
        public MenuMetadataProvider(MenuMetadata current, int schemaVersion = 1)
        {
            Current = current;
            MetadataSchemaVersion = schemaVersion;
        }

        public int MetadataSchemaVersion { get; }
        public MenuMetadata Current { get; set; }
        public MenuMetadata CaptureMetadata() => Current;
        public void RestoreMetadata(MenuMetadata metadata) => Current = metadata;
    }

    private sealed class LegacyMenuMetadataProvider : ISaveMetadataProvider<LegacyMenuMetadata>
    {
        public LegacyMenuMetadataProvider(LegacyMenuMetadata current)
        {
            Current = current;
        }

        public int MetadataSchemaVersion => 1;
        public LegacyMenuMetadata Current { get; set; }
        public LegacyMenuMetadata CaptureMetadata() => Current;
        public void RestoreMetadata(LegacyMenuMetadata metadata) => Current = metadata;
    }

    private sealed class MigratingMenuMetadataProvider : ISaveMetadataProvider<MenuMetadata>, ISaveMetadataMigratable
    {
        private readonly IReadOnlyList<SaveMigrationStep> _migrations;

        public MigratingMenuMetadataProvider(int schemaVersion, MenuMetadata current, IReadOnlyList<SaveMigrationStep> migrations)
        {
            MetadataSchemaVersion = schemaVersion;
            Current = current;
            _migrations = migrations;
        }

        public int MetadataSchemaVersion { get; }
        public MenuMetadata Current { get; set; }
        public MenuMetadata CaptureMetadata() => Current;
        public void RestoreMetadata(MenuMetadata metadata) => Current = metadata;
        public ISaveMigrationSource CreateMetadataMigrationSource() => new StaticMigrationSource(_migrations);
    }

    private sealed class ListMetadataProvider<TItem> : ISaveMetadataProvider<List<TItem>>
    {
        public ListMetadataProvider(int schemaVersion, List<TItem> current)
        {
            MetadataSchemaVersion = schemaVersion;
            Current = current;
        }

        public int MetadataSchemaVersion { get; }
        public List<TItem> Current { get; set; }
        public List<TItem> CaptureMetadata() => Current;
        public void RestoreMetadata(List<TItem> metadata) => Current = metadata;
    }

    private sealed class MigratingListMetadataProvider<TItem> : ISaveMetadataProvider<List<TItem>>, ISaveMetadataMigratable
    {
        private readonly IReadOnlyList<SaveMigrationStep> _migrations;

        public MigratingListMetadataProvider(int schemaVersion, List<TItem> current, IReadOnlyList<SaveMigrationStep> migrations)
        {
            MetadataSchemaVersion = schemaVersion;
            Current = current;
            _migrations = migrations;
        }

        public int MetadataSchemaVersion { get; }
        public List<TItem> Current { get; set; }
        public List<TItem> CaptureMetadata() => Current;
        public void RestoreMetadata(List<TItem> metadata) => Current = metadata;
        public ISaveMigrationSource CreateMetadataMigrationSource() => new StaticMigrationSource(_migrations);
    }

    private sealed class DictionaryMetadataProvider : ISaveMetadataProvider<Dictionary<string, MenuItem>>
    {
        public DictionaryMetadataProvider(int schemaVersion, Dictionary<string, MenuItem> current)
        {
            MetadataSchemaVersion = schemaVersion;
            Current = current;
        }

        public int MetadataSchemaVersion { get; }
        public Dictionary<string, MenuItem> Current { get; set; }
        public Dictionary<string, MenuItem> CaptureMetadata() => Current;
        public void RestoreMetadata(Dictionary<string, MenuItem> metadata) => Current = metadata;
    }

    private sealed class MigratingDictionaryMetadataProvider :
        ISaveMetadataProvider<Dictionary<string, MenuItem>>,
        ISaveMetadataMigratable
    {
        private readonly IReadOnlyList<SaveMigrationStep> _migrations;

        public MigratingDictionaryMetadataProvider(
            int schemaVersion,
            Dictionary<string, MenuItem> current,
            IReadOnlyList<SaveMigrationStep> migrations)
        {
            MetadataSchemaVersion = schemaVersion;
            Current = current;
            _migrations = migrations;
        }

        public int MetadataSchemaVersion { get; }
        public Dictionary<string, MenuItem> Current { get; set; }
        public Dictionary<string, MenuItem> CaptureMetadata() => Current;
        public void RestoreMetadata(Dictionary<string, MenuItem> metadata) => Current = metadata;
        public ISaveMigrationSource CreateMetadataMigrationSource() => new StaticMigrationSource(_migrations);
    }

    private sealed class StringMetadataProvider : ISaveMetadataProvider<string?>
    {
        public StringMetadataProvider(int schemaVersion, string? current)
        {
            MetadataSchemaVersion = schemaVersion;
            Current = current;
        }

        public int MetadataSchemaVersion { get; }
        public string? Current { get; set; }
        public string? CaptureMetadata() => Current;
        public void RestoreMetadata(string? metadata) => Current = metadata;
    }

    private sealed class MigratingStringMetadataProvider : ISaveMetadataProvider<string?>, ISaveMetadataMigratable
    {
        private readonly IReadOnlyList<SaveMigrationStep> _migrations;

        public MigratingStringMetadataProvider(int schemaVersion, string? current, IReadOnlyList<SaveMigrationStep> migrations)
        {
            MetadataSchemaVersion = schemaVersion;
            Current = current;
            _migrations = migrations;
        }

        public int MetadataSchemaVersion { get; }
        public string? Current { get; set; }
        public string? CaptureMetadata() => Current;
        public void RestoreMetadata(string? metadata) => Current = metadata;
        public ISaveMigrationSource CreateMetadataMigrationSource() => new StaticMigrationSource(_migrations);
    }

    private sealed class StaticMigrationSource : ISaveMigrationSource
    {
        public StaticMigrationSource(IReadOnlyList<SaveMigrationStep> migrations)
        {
            Migrations = migrations;
        }

        public IReadOnlyList<SaveMigrationStep> Migrations { get; }
    }
}
