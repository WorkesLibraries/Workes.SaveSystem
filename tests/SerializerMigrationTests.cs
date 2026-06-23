using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class SerializerMigrationTests
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
    public void JsonSerializer_ExtractSchemaVersion_ReadsEnvelopeVersion()
    {
        var serializer = new JsonSaveSerializer();
        var schematic = serializer.CreateSchematic(typeof(V1State));
        schematic.SchemaVersion = 4;
        var serialized = serializer.Serialize(new V1State { Name = "Rook" }, schematic);
        var decoded = Encoding.UTF8.GetString(serialized);

        var version = serializer.ExtractSchemaVersion(serialized);

        Assert.That(decoded.TrimStart(), Does.StartWith("{"));
        Assert.That(decoded, Does.Contain("\"SchemaVersion\": 4"));
        Assert.That(decoded, Does.Contain("\"Name\": \"Rook\""));
        Assert.That(version, Is.EqualTo(4));
    }

    [Test]
    public void JsonSerializer_DefaultConstructorWritesPrettyJson()
    {
        var serializer = new JsonSaveSerializer();
        var schematic = serializer.CreateSchematic(typeof(V1State));

        var json = Encoding.UTF8.GetString(serializer.Serialize(new V1State { Name = "Rook" }, schematic));

        Assert.That(serializer.Formatting, Is.EqualTo(JsonSaveFormatting.Pretty));
        Assert.That(json, Does.Contain(Environment.NewLine));
        Assert.That(json, Does.Contain("  \"SchemaVersion\""));
    }

    [Test]
    public void JsonSerializer_PrettyFormattingWritesPrettyJson()
    {
        var serializer = new JsonSaveSerializer(JsonSaveFormatting.Pretty);
        var schematic = serializer.CreateSchematic(typeof(V1State));

        var json = Encoding.UTF8.GetString(serializer.Serialize(new V1State { Name = "Rook" }, schematic));

        Assert.That(serializer.Formatting, Is.EqualTo(JsonSaveFormatting.Pretty));
        Assert.That(json, Does.Contain(Environment.NewLine));
        Assert.That(json, Does.Contain("  \"SchemaVersion\""));
    }

    [Test]
    public void JsonSerializer_CompactFormattingWritesCompactJsonAndStillDeserializes()
    {
        var serializer = new JsonSaveSerializer(JsonSaveFormatting.Compact);
        var schematic = serializer.CreateSchematic(typeof(V1State));
        schematic.SchemaVersion = 4;

        var serialized = serializer.Serialize(new V1State { Name = "Rook" }, schematic);
        var json = Encoding.UTF8.GetString(serialized);
        var deserialized = (V1State)serializer.Deserialize(serialized, schematic);

        Assert.That(serializer.Formatting, Is.EqualTo(JsonSaveFormatting.Compact));
        Assert.That(json, Does.Not.Contain(Environment.NewLine));
        Assert.That(json, Does.Not.Contain("  \"SchemaVersion\""));
        Assert.That(json, Does.Contain("\"SchemaVersion\":4"));
        Assert.That(json, Does.Contain("\"Name\":\"Rook\""));
        Assert.That(serializer.ExtractSchemaVersion(serialized), Is.EqualTo(4));
        Assert.That(deserialized.Name, Is.EqualTo("Rook"));
    }

    [Test]
    public void JsonSerializer_ExtractSchemaVersion_RejectsMissingEnvelopeVersion()
    {
        var serializer = new JsonSaveSerializer();

        var ex = Assert.Throws<InvalidOperationException>(() => serializer.ExtractSchemaVersion(Encoding.UTF8.GetBytes("""{"Data":{}}""")));

        Assert.That(ex!.Message, Does.Contain("Failed to extract schema version"));
    }

    [Test]
    public void RegisterProvider_RejectsDuplicateMigrationSteps()
    {
        var manager = new SaveManager<string>(CreateOptions());
        var provider = new MigratingProvider(
            schemaVersion: 2,
            current: new V2State { Name = "Current", Level = 1 },
            migrations: new[]
            {
                new SaveMigrationStep(1, (_, _) => { }),
                new SaveMigrationStep(1, (_, _) => { })
            });

        manager.RegisterProvider(provider);

        var ex = Assert.Throws<InvalidOperationException>(() => manager.ValidateRegistrations());

        Assert.That(ex!.Message, Does.Contain("multiple migration steps"));
    }

    [Test]
    public void RegisterProvider_RejectsNullMigrationSteps()
    {
        var manager = new SaveManager<string>(CreateOptions());
        var provider = new MigratingProvider(
            schemaVersion: 2,
            current: new V2State { Name = "Current", Level = 1 },
            migrations: new SaveMigrationStep?[]
            {
                null
            }!);

        manager.RegisterProvider(provider);

        var ex = Assert.Throws<InvalidOperationException>(() => manager.ValidateRegistrations());

        Assert.That(ex!.Message, Does.Contain("contains null entries"));
    }

    [Test]
    public void LoadFromDisk_AppliesMigrationStepsSequentially()
    {
        var oldManager = new SaveManager<string>(CreateOptions());
        var oldProvider = new V1Provider(new V1State { Name = "Scout" });
        oldManager.RegisterProvider(oldProvider);
        oldManager.ValidateRegistrations();
        oldManager.SaveToDisk("slot");

        var newManager = new SaveManager<string>(CreateOptions());
        var newProvider = new MigratingProvider(
            schemaVersion: 2,
            current: new V2State { Name = "Unset", Level = 0 },
            migrations: new[]
            {
                new SaveMigrationStep(1, (data, factory) => data.Set("Level", factory.CreateInt(12)))
            });
        newManager.RegisterProvider(newProvider);
        newManager.ValidateRegistrations();

        var loaded = newManager.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(newProvider.Current.Name, Is.EqualTo("Scout"));
        Assert.That(newProvider.Current.Level, Is.EqualTo(12));
    }

    [Test]
    public void LoadFromDisk_AppliesSimpleMigrationHelpers()
    {
        var oldManager = new SaveManager<string>(CreateOptions());
        var oldProvider = new V1Provider(new V1State { Name = "Scout" });
        oldManager.RegisterProvider(oldProvider);
        oldManager.ValidateRegistrations();
        oldManager.SaveToDisk("slot");

        var newManager = new SaveManager<string>(CreateOptions());
        var newProvider = new MigratingProvider(
            schemaVersion: 2,
            current: new V2State { Name = "Unset", Level = 0 },
            migrations: new[]
            {
                SaveMigrationStep.From(
                    1,
                    SaveMigrationStep.Rename("Name", "DisplayName"),
                    SaveMigrationStep.SetString("Name", "Scout"),
                    SaveMigrationStep.AddIntDefault("Level", 12))
            });
        newManager.RegisterProvider(newProvider);
        newManager.ValidateRegistrations();

        var loaded = newManager.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(newProvider.Current.Name, Is.EqualTo("Scout"));
        Assert.That(newProvider.Current.Level, Is.EqualTo(12));
    }

    [Test]
    public void JsonSerializer_CompactFormattingPreservesCompactOutputWhenSerializingMigratedNodes()
    {
        var serializer = new JsonSaveSerializer(JsonSaveFormatting.Compact);
        var schematic = serializer.CreateSchematic(typeof(V1State));
        schematic.SchemaVersion = 4;
        var serialized = serializer.Serialize(new V1State { Name = "Scout" }, schematic);
        var data = serializer.DeserializeToNode(serialized);

        data.Set("Level", serializer.NodeFactory.CreateInt(12));

        var json = Encoding.UTF8.GetString(serializer.SerializeFromNode(data));

        Assert.That(json, Does.Not.Contain(Environment.NewLine));
        Assert.That(json, Does.Contain("\"SchemaVersion\":4"));
        Assert.That(json, Does.Contain("\"Level\":12"));
    }

    [Test]
    public void JsonSerializer_SerializeFromNode_WritesMutatedNullNodesAsJsonNull()
    {
        var serializer = new JsonSaveSerializer(JsonSaveFormatting.Compact);
        var schematic = serializer.CreateSchematic(typeof(V1State));
        var serialized = serializer.Serialize(new V1State { Name = "Scout" }, schematic);
        var data = serializer.DeserializeToNode(serialized);

        data.Get("Name").SetNull();

        var json = Encoding.UTF8.GetString(serializer.SerializeFromNode(data));

        Assert.That(json, Does.Contain("\"Name\":null"));
    }

    [Test]
    public void JsonSerializer_MigrationNodes_RoundTripAdditionalValueTypes()
    {
        var serializer = new JsonSaveSerializer(JsonSaveFormatting.Compact);
        var data = serializer.NodeFactory.CreateObject();
        var dateTime = new DateTime(2026, 6, 22, 10, 11, 12, DateTimeKind.Utc);
        var bytes = new byte[] { 1, 2, 3, 4 };

        data.Set("Long", serializer.NodeFactory.CreateLong(9_000_000_000L));
        data.Set("Double", serializer.NodeFactory.CreateDouble(123.456789d));
        data.Set("Decimal", serializer.NodeFactory.CreateDecimal(1234567890.123456789m));
        data.Set("Bytes", serializer.NodeFactory.CreateBytes(bytes));
        data.Set("DateTime", serializer.NodeFactory.CreateDateTime(dateTime));

        var serialized = serializer.SerializeFromNode(data);
        var json = Encoding.UTF8.GetString(serialized);
        var deserialized = serializer.DeserializeToNode(serialized);

        Assert.That(json, Does.Contain("\"Long\":9000000000"));
        Assert.That(json, Does.Contain("\"Double\":123.456789"));
        Assert.That(json, Does.Contain("\"Decimal\":\"1234567890.123456789\""));
        Assert.That(json, Does.Contain("\"Bytes\":\"AQIDBA==\""));
        Assert.That(json, Does.Contain("\"DateTime\":\"2026-06-22T10:11:12.0000000Z\""));
        Assert.That(deserialized.Get("Long").NodeType, Is.EqualTo(SaveDataNodeType.Long));
        Assert.That(deserialized.Get("Long").AsLong(), Is.EqualTo(9_000_000_000L));
        Assert.That(deserialized.Get("Double").NodeType, Is.EqualTo(SaveDataNodeType.Double));
        Assert.That(deserialized.Get("Double").AsDouble(), Is.EqualTo(123.456789d));
        Assert.That(deserialized.Get("Decimal").NodeType, Is.EqualTo(SaveDataNodeType.String));
        Assert.That(deserialized.Get("Decimal").AsDecimal(), Is.EqualTo(1234567890.123456789m));
        Assert.That(deserialized.Get("Bytes").NodeType, Is.EqualTo(SaveDataNodeType.String));
        Assert.That(deserialized.Get("Bytes").AsBytes(), Is.EqualTo(bytes));
        Assert.That(deserialized.Get("DateTime").NodeType, Is.EqualTo(SaveDataNodeType.String));
        Assert.That(deserialized.Get("DateTime").AsDateTime(), Is.EqualTo(dateTime));
    }

    [Test]
    public void JsonSerializer_MigrationNodes_ParseIntegersAsIntWhenPossibleAndLongWhenNeeded()
    {
        var serializer = new JsonSaveSerializer(JsonSaveFormatting.Compact);
        var json = Encoding.UTF8.GetBytes("""{"SchemaVersion":1,"Data":{"Small":12,"Large":9000000000}}""");

        var node = serializer.DeserializeToNode(json);

        Assert.That(node.Get("Small").NodeType, Is.EqualTo(SaveDataNodeType.Int));
        Assert.That(node.Get("Small").AsInt(), Is.EqualTo(12));
        Assert.That(node.Get("Large").NodeType, Is.EqualTo(SaveDataNodeType.Long));
        Assert.That(node.Get("Large").AsLong(), Is.EqualTo(9_000_000_000L));
    }

    [Test]
    public void JsonSerializer_MigrationNodes_ExposeProviderDataRoot()
    {
        var serializer = new JsonSaveSerializer(JsonSaveFormatting.Compact);
        var schematic = serializer.CreateSchematic(typeof(List<ItemState>));
        var serialized = serializer.Serialize(new List<ItemState> { new ItemState { Id = "potion" } }, schematic);

        var root = serializer.DeserializeToNode(serialized);

        Assert.That(root.NodeType, Is.EqualTo(SaveDataNodeType.Array));
        Assert.That(root.Count, Is.EqualTo(1));
        Assert.That(root.GetAt(0).Get("Id").AsString(), Is.EqualTo("potion"));
    }

    [Test]
    public void JsonSerializer_ContextualSerializeFromNode_UsesContextSchemaVersion()
    {
        var serializer = new JsonSaveSerializer(JsonSaveFormatting.Compact);
        var schematic = serializer.CreateSchematic(typeof(V1State));
        schematic.SchemaVersion = 1;
        var context = CreateContext("player", schemaVersion: 7, typeof(V1State), schematic);
        var root = serializer.NodeFactory.CreateObject();
        root.Set("Name", serializer.NodeFactory.CreateString("Scout"));

        var json = Encoding.UTF8.GetString(((IContextualSaveMigrationCapableSerializer)serializer).SerializeFromNode(root, context));

        Assert.That(json, Does.Contain("\"SchemaVersion\":7"));
        Assert.That(json, Does.Contain("\"Data\""));
    }

    [Test]
    public void LoadFromDisk_MigratesRootListProvider()
    {
        var oldManager = new SaveManager<string>(CreateOptions());
        oldManager.RegisterProvider(new LegacyListProvider(schemaVersion: 1, new List<LegacyItemState> { new LegacyItemState { Id = "potion" } }));
        oldManager.ValidateRegistrations();
        oldManager.SaveToDisk("slot");

        var newManager = new SaveManager<string>(CreateOptions());
        var provider = new MigratingListProvider(
            schemaVersion: 2,
            current: new List<ItemState>(),
            migrations: new[]
            {
                new SaveMigrationStep(1, (root, factory) =>
                {
                    for (var i = 0; i < root.Count; i++)
                    {
                        var item = root.GetAt(i);
                        if (!item.Has("Count"))
                            item.Set("Count", factory.CreateInt(1));
                    }
                })
            });
        newManager.RegisterProvider(provider);
        newManager.ValidateRegistrations();

        var loaded = newManager.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current, Has.Count.EqualTo(1));
        Assert.That(provider.Current[0].Id, Is.EqualTo("potion"));
        Assert.That(provider.Current[0].Count, Is.EqualTo(1));
    }

    [Test]
    public void LoadFromDisk_MigratesRootDictionaryProvider()
    {
        var oldManager = new SaveManager<string>(CreateOptions());
        oldManager.RegisterProvider(new DictionaryProvider(
            schemaVersion: 1,
            new Dictionary<string, ItemState> { ["old-key"] = new ItemState { Id = "potion", Count = 2 } }));
        oldManager.ValidateRegistrations();
        oldManager.SaveToDisk("slot");

        var newManager = new SaveManager<string>(CreateOptions());
        var provider = new MigratingDictionaryProvider(
            schemaVersion: 2,
            current: new Dictionary<string, ItemState>(),
            migrations: new[]
            {
                new SaveMigrationStep(1, (root, _) =>
                {
                    if (!root.Has("old-key"))
                        return;

                    var value = root.Get("old-key");
                    root.Remove("old-key");
                    root.Set("new-key", value);
                })
            });
        newManager.RegisterProvider(provider);
        newManager.ValidateRegistrations();

        var loaded = newManager.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.ContainsKey("old-key"), Is.False);
        Assert.That(provider.Current["new-key"].Id, Is.EqualTo("potion"));
        Assert.That(provider.Current["new-key"].Count, Is.EqualTo(2));
    }

    [Test]
    public void LoadFromDisk_MigratesRootPrimitiveProviderWithReplaceWith()
    {
        var oldManager = new SaveManager<string>(CreateOptions());
        oldManager.RegisterProvider(new IntProvider(schemaVersion: 1, current: 4));
        oldManager.ValidateRegistrations();
        oldManager.SaveToDisk("slot");

        var newManager = new SaveManager<string>(CreateOptions());
        var provider = new MigratingStringProvider(
            schemaVersion: 2,
            current: string.Empty,
            migrations: new[]
            {
                new SaveMigrationStep(1, (root, factory) =>
                    root.ReplaceWith(factory.CreateString("level-" + root.AsInt())))
            });
        newManager.RegisterProvider(provider);
        newManager.ValidateRegistrations();

        var loaded = newManager.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current, Is.EqualTo("level-4"));
    }

    [Test]
    public void JsonSerializer_DeserializesDocumentedStringConventionsThroughSchematics()
    {
        var serializer = new JsonSaveSerializer(JsonSaveFormatting.Compact);
        var schematic = serializer.CreateSchematic(typeof(AdditionalValueTypesState));
        schematic.SchemaVersion = 1;
        var bytes = new byte[] { 1, 2, 3, 4 };
        var dateTime = new DateTime(2026, 6, 22, 10, 11, 12, DateTimeKind.Utc);
        var json = Encoding.UTF8.GetBytes(
            """{"SchemaVersion":1,"Data":{"Bytes":"AQIDBA==","DateTime":"2026-06-22T10:11:12.0000000Z"}}""");

        var state = (AdditionalValueTypesState)serializer.Deserialize(json, schematic);

        Assert.That(state.Bytes, Is.EqualTo(bytes));
        Assert.That(state.DateTime, Is.EqualTo(dateTime));
    }

    [Test]
    public void LoadFromDisk_ThrowsWhenMigrationPathIsMissing()
    {
        var oldManager = new SaveManager<string>(CreateOptions());
        oldManager.RegisterProvider(new V1Provider(new V1State { Name = "Scout" }));
        oldManager.ValidateRegistrations();
        oldManager.SaveToDisk("slot");

        var newManager = new SaveManager<string>(CreateOptions());
        newManager.RegisterProvider(
            new MigratingProvider(
                schemaVersion: 2,
                current: new V2State { Name = "Unset", Level = 0 },
                migrations: Array.Empty<SaveMigrationStep>()));
        newManager.ValidateRegistrations();

        var ex = Assert.Throws<InvalidOperationException>(() => newManager.LoadFromDisk("slot"));

        Assert.That(ex!.Message, Does.Contain("Failed to migrate save data"));
    }

    [Test]
    public void LoadFromDisk_ThrowsWhenSavedVersionIsNewerThanProviderVersion()
    {
        var newerManager = new SaveManager<string>(CreateOptions());
        newerManager.RegisterProvider(
            new MigratingProvider(
                schemaVersion: 2,
                current: new V2State { Name = "Scout", Level = 12 },
                migrations: new[]
                {
                    new SaveMigrationStep(1, (data, factory) => data.Set("Level", factory.CreateInt(1)))
                }));
        newerManager.ValidateRegistrations();
        newerManager.SaveToDisk("slot");

        var olderManager = new SaveManager<string>(CreateOptions());
        olderManager.RegisterProvider(
            new MigratingProvider(
                schemaVersion: 1,
                current: new V2State { Name = "Unset", Level = 0 },
                migrations: new[]
                {
                    new SaveMigrationStep(1, (data, factory) => data.Set("Level", factory.CreateInt(1)))
                }));
        olderManager.ValidateRegistrations();

        var ex = Assert.Throws<InvalidOperationException>(() => olderManager.LoadFromDisk("slot"));

        Assert.That(ex!.Message, Does.Contain("Failed to migrate save data"));
        Assert.That(ex.Message, Does.Contain("from schema version 2 to 1"));
    }

    [Test]
    public void LoadFromDisk_ThrowsWhenMigrationPayloadIsMissingDataEnvelope()
    {
        var oldManager = new SaveManager<string>(CreateOptions());
        oldManager.RegisterProvider(new V1Provider(new V1State { Name = "Scout" }));
        oldManager.ValidateRegistrations();
        oldManager.SaveToDisk("slot");
        File.WriteAllText(Path.Combine(_tempRoot, "slot", "player.json"), """{"SchemaVersion":1}""");

        var newManager = new SaveManager<string>(CreateOptions());
        newManager.RegisterProvider(
            new MigratingProvider(
                schemaVersion: 2,
                current: new V2State { Name = "Unset", Level = 0 },
                migrations: new[]
                {
                    new SaveMigrationStep(1, (data, factory) => data.Set("Level", factory.CreateInt(12)))
                }));
        newManager.ValidateRegistrations();

        var ex = Assert.Throws<InvalidOperationException>(() => newManager.LoadFromDisk("slot"));

        Assert.That(ex!.Message, Does.Contain("Failed to migrate save data"));
    }

    [Test]
    public void LoadFromDisk_ThrowsWhenMigrationStepFails()
    {
        var oldManager = new SaveManager<string>(CreateOptions());
        oldManager.RegisterProvider(new V1Provider(new V1State { Name = "Scout" }));
        oldManager.ValidateRegistrations();
        oldManager.SaveToDisk("slot");

        var newManager = new SaveManager<string>(CreateOptions());
        newManager.RegisterProvider(
            new MigratingProvider(
                schemaVersion: 2,
                current: new V2State { Name = "Unset", Level = 0 },
                migrations: new[]
                {
                    new SaveMigrationStep(1, (_, _) => throw new InvalidOperationException("broken migration"))
                }));
        newManager.ValidateRegistrations();

        var ex = Assert.Throws<InvalidOperationException>(() => newManager.LoadFromDisk("slot"));

        Assert.That(ex!.Message, Does.Contain("Failed to migrate save data"));
    }

    private SaveSystemOptions<string> CreateOptions(ISaveSerializer? serializer = null)
    {
        return new SaveSystemOptions<string>(
            saveRootPath: _tempRoot,
            serializer: serializer ?? new JsonSaveSerializer(),
            tempFolderName: SaveSystemOptions<string>.DefaultTempFolderName(),
            savePathResolver: identity => identity,
            fileNameResolver: SaveSystemOptions<string>.DefaultFileNameResolver);
    }

    private static SaveSerializerContext CreateContext(
        string saveKey,
        int schemaVersion,
        Type stateType,
        ISaveSchematic schematic)
    {
        return new SaveSerializerContext(
            saveKey,
            schemaVersion,
            stateType,
            schematic,
            new SaveSerializerMetadata());
    }

    public sealed class V1State
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class V2State
    {
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
    }

    public sealed class AdditionalValueTypesState
    {
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public DateTime DateTime { get; set; }
    }

    public sealed class ItemState
    {
        public string Id { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public sealed class LegacyItemState
    {
        public string Id { get; set; } = string.Empty;
    }

    private sealed class V1Provider : ISaveProvider<V1State>
    {
        public V1Provider(V1State current)
        {
            Current = current;
        }

        public string SaveKey => "player";
        public int SchemaVersion => 1;
        public int LoadPriority => 0;
        public V1State Current { get; set; }

        public V1State CaptureState()
        {
            return Current;
        }

        public void RestoreState(V1State state)
        {
            Current = state;
        }
    }

    private sealed class MigratingProvider : ISaveProvider<V2State>, ISaveMigratable
    {
        private readonly IReadOnlyList<SaveMigrationStep> _migrations;

        public MigratingProvider(int schemaVersion, V2State current, IReadOnlyList<SaveMigrationStep> migrations)
        {
            SchemaVersion = schemaVersion;
            Current = current;
            _migrations = migrations;
        }

        public string SaveKey => "player";
        public int SchemaVersion { get; }
        public int LoadPriority => 0;
        public V2State Current { get; set; }

        public V2State CaptureState()
        {
            return Current;
        }

        public void RestoreState(V2State state)
        {
            Current = state;
        }

        public ISaveMigrationSource CreateMigrationSource()
        {
            return new MigrationSource(_migrations);
        }
    }

    private sealed class MigrationSource : ISaveMigrationSource
    {
        public MigrationSource(IReadOnlyList<SaveMigrationStep> migrations)
        {
            Migrations = migrations;
        }

        public IReadOnlyList<SaveMigrationStep> Migrations { get; }
    }

    private sealed class LegacyListProvider : ISaveProvider<List<LegacyItemState>>
    {
        public LegacyListProvider(int schemaVersion, List<LegacyItemState> current)
        {
            SchemaVersion = schemaVersion;
            Current = current;
        }

        public string SaveKey => "player";
        public int SchemaVersion { get; }
        public int LoadPriority => 0;
        public List<LegacyItemState> Current { get; set; }
        public List<LegacyItemState> CaptureState() => Current;
        public void RestoreState(List<LegacyItemState> state) => Current = state;
    }

    private sealed class MigratingListProvider : ISaveProvider<List<ItemState>>, ISaveMigratable
    {
        private readonly IReadOnlyList<SaveMigrationStep> _migrations;

        public MigratingListProvider(int schemaVersion, List<ItemState> current, IReadOnlyList<SaveMigrationStep> migrations)
        {
            SchemaVersion = schemaVersion;
            Current = current;
            _migrations = migrations;
        }

        public string SaveKey => "player";
        public int SchemaVersion { get; }
        public int LoadPriority => 0;
        public List<ItemState> Current { get; set; }
        public List<ItemState> CaptureState() => Current;
        public void RestoreState(List<ItemState> state) => Current = state;
        public ISaveMigrationSource CreateMigrationSource() => new MigrationSource(_migrations);
    }

    private sealed class DictionaryProvider : ISaveProvider<Dictionary<string, ItemState>>
    {
        public DictionaryProvider(int schemaVersion, Dictionary<string, ItemState> current)
        {
            SchemaVersion = schemaVersion;
            Current = current;
        }

        public string SaveKey => "player";
        public int SchemaVersion { get; }
        public int LoadPriority => 0;
        public Dictionary<string, ItemState> Current { get; set; }
        public Dictionary<string, ItemState> CaptureState() => Current;
        public void RestoreState(Dictionary<string, ItemState> state) => Current = state;
    }

    private sealed class MigratingDictionaryProvider : ISaveProvider<Dictionary<string, ItemState>>, ISaveMigratable
    {
        private readonly IReadOnlyList<SaveMigrationStep> _migrations;

        public MigratingDictionaryProvider(
            int schemaVersion,
            Dictionary<string, ItemState> current,
            IReadOnlyList<SaveMigrationStep> migrations)
        {
            SchemaVersion = schemaVersion;
            Current = current;
            _migrations = migrations;
        }

        public string SaveKey => "player";
        public int SchemaVersion { get; }
        public int LoadPriority => 0;
        public Dictionary<string, ItemState> Current { get; set; }
        public Dictionary<string, ItemState> CaptureState() => Current;
        public void RestoreState(Dictionary<string, ItemState> state) => Current = state;
        public ISaveMigrationSource CreateMigrationSource() => new MigrationSource(_migrations);
    }

    private sealed class IntProvider : ISaveProvider<int>
    {
        public IntProvider(int schemaVersion, int current)
        {
            SchemaVersion = schemaVersion;
            Current = current;
        }

        public string SaveKey => "player";
        public int SchemaVersion { get; }
        public int LoadPriority => 0;
        public int Current { get; set; }
        public int CaptureState() => Current;
        public void RestoreState(int state) => Current = state;
    }

    private sealed class MigratingStringProvider : ISaveProvider<string>, ISaveMigratable
    {
        private readonly IReadOnlyList<SaveMigrationStep> _migrations;

        public MigratingStringProvider(int schemaVersion, string current, IReadOnlyList<SaveMigrationStep> migrations)
        {
            SchemaVersion = schemaVersion;
            Current = current;
            _migrations = migrations;
        }

        public string SaveKey => "player";
        public int SchemaVersion { get; }
        public int LoadPriority => 0;
        public string Current { get; set; }
        public string CaptureState() => Current;
        public void RestoreState(string state) => Current = state;
        public ISaveMigrationSource CreateMigrationSource() => new MigrationSource(_migrations);
    }
}
