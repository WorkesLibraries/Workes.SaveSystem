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
        var serialized = serializer.Serialize(new V1State { Name = "Scout" }, schematic);
        var envelope = serializer.DeserializeToNode(serialized);

        envelope.Get("Data").Set("Level", serializer.NodeFactory.CreateInt(12));
        envelope.Get("SchemaVersion").SetInt(2);

        var json = Encoding.UTF8.GetString(serializer.SerializeFromNode(envelope));

        Assert.That(json, Does.Not.Contain(Environment.NewLine));
        Assert.That(json, Does.Contain("\"SchemaVersion\":2"));
        Assert.That(json, Does.Contain("\"Level\":12"));
    }

    [Test]
    public void JsonSerializer_SerializeFromNode_WritesMutatedNullNodesAsJsonNull()
    {
        var serializer = new JsonSaveSerializer(JsonSaveFormatting.Compact);
        var schematic = serializer.CreateSchematic(typeof(V1State));
        var serialized = serializer.Serialize(new V1State { Name = "Scout" }, schematic);
        var envelope = serializer.DeserializeToNode(serialized);

        envelope.Get("Data").Get("Name").SetNull();

        var json = Encoding.UTF8.GetString(serializer.SerializeFromNode(envelope));

        Assert.That(json, Does.Contain("\"Name\":null"));
    }

    [Test]
    public void JsonSerializer_MigrationNodes_RoundTripAdditionalValueTypes()
    {
        var serializer = new JsonSaveSerializer(JsonSaveFormatting.Compact);
        var data = serializer.NodeFactory.CreateObject();
        var dateTime = new DateTime(2026, 6, 22, 10, 11, 12, DateTimeKind.Utc);
        var bytes = new byte[] { 1, 2, 3, 4 };

        data.Set("SchemaVersion", serializer.NodeFactory.CreateInt(1));
        var payload = serializer.NodeFactory.CreateObject();
        payload.Set("Long", serializer.NodeFactory.CreateLong(9_000_000_000L));
        payload.Set("Double", serializer.NodeFactory.CreateDouble(123.456789d));
        payload.Set("Decimal", serializer.NodeFactory.CreateDecimal(1234567890.123456789m));
        payload.Set("Bytes", serializer.NodeFactory.CreateBytes(bytes));
        payload.Set("DateTime", serializer.NodeFactory.CreateDateTime(dateTime));
        data.Set("Data", payload);

        var serialized = serializer.SerializeFromNode(data);
        var json = Encoding.UTF8.GetString(serialized);
        var deserialized = serializer.DeserializeToNode(serialized);
        var deserializedPayload = deserialized.Get("Data");

        Assert.That(json, Does.Contain("\"Long\":9000000000"));
        Assert.That(json, Does.Contain("\"Double\":123.456789"));
        Assert.That(json, Does.Contain("\"Decimal\":\"1234567890.123456789\""));
        Assert.That(json, Does.Contain("\"Bytes\":\"AQIDBA==\""));
        Assert.That(json, Does.Contain("\"DateTime\":\"2026-06-22T10:11:12.0000000Z\""));
        Assert.That(deserializedPayload.Get("Long").NodeType, Is.EqualTo(SaveDataNodeType.Long));
        Assert.That(deserializedPayload.Get("Long").AsLong(), Is.EqualTo(9_000_000_000L));
        Assert.That(deserializedPayload.Get("Double").NodeType, Is.EqualTo(SaveDataNodeType.Double));
        Assert.That(deserializedPayload.Get("Double").AsDouble(), Is.EqualTo(123.456789d));
        Assert.That(deserializedPayload.Get("Decimal").NodeType, Is.EqualTo(SaveDataNodeType.String));
        Assert.That(deserializedPayload.Get("Decimal").AsDecimal(), Is.EqualTo(1234567890.123456789m));
        Assert.That(deserializedPayload.Get("Bytes").NodeType, Is.EqualTo(SaveDataNodeType.String));
        Assert.That(deserializedPayload.Get("Bytes").AsBytes(), Is.EqualTo(bytes));
        Assert.That(deserializedPayload.Get("DateTime").NodeType, Is.EqualTo(SaveDataNodeType.String));
        Assert.That(deserializedPayload.Get("DateTime").AsDateTime(), Is.EqualTo(dateTime));
    }

    [Test]
    public void JsonSerializer_MigrationNodes_ParseIntegersAsIntWhenPossibleAndLongWhenNeeded()
    {
        var serializer = new JsonSaveSerializer(JsonSaveFormatting.Compact);
        var json = Encoding.UTF8.GetBytes("""{"SchemaVersion":1,"Data":{"Small":12,"Large":9000000000}}""");

        var node = serializer.DeserializeToNode(json).Get("Data");

        Assert.That(node.Get("Small").NodeType, Is.EqualTo(SaveDataNodeType.Int));
        Assert.That(node.Get("Small").AsInt(), Is.EqualTo(12));
        Assert.That(node.Get("Large").NodeType, Is.EqualTo(SaveDataNodeType.Long));
        Assert.That(node.Get("Large").AsLong(), Is.EqualTo(9_000_000_000L));
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
}
