using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class ContextualSerializerTests
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
    public void SaveToDisk_WritesSerializerMetadataBeforeProviderSerialization()
    {
        var serializer = new ContextualMetadataJsonSerializer();
        var manager = CreateManager(serializer);
        manager.RegisterProvider(new TestProvider(new TestState { Name = "Scout", Level = 7 }));
        manager.ValidateRegistrations();
        serializer.Records.Clear();

        manager.SaveToDisk("slot");

        var serialize = serializer.Records.First(record => record.Operation == "Serialize");
        AssertContext(serialize, "player", 1, typeof(TestState));
        Assert.That(serialize.MetadataReady, Is.True);
        Assert.That(serialize.ProviderMarker, Is.EqualTo("player:1"));
    }

    [Test]
    public void ValidateRegistrations_UsesTransientSerializerMetadataForCompatibilitySerialization()
    {
        var serializer = new ContextualMetadataJsonSerializer();
        var manager = CreateManager(serializer);
        manager.RegisterProvider(new TestProvider(new TestState { Name = "Scout", Level = 7 }));

        manager.ValidateRegistrations();

        var serialize = serializer.Records.First(record => record.Operation == "Serialize");
        Assert.That(serialize.MetadataReady, Is.True);
        Assert.That(serialize.ProviderMarker, Is.EqualTo("player:1"));
    }

    [Test]
    public void LoadAndValidate_PassSavedSerializerMetadataToProviderOperations()
    {
        var writerSerializer = new ContextualMetadataJsonSerializer();
        var writer = CreateManager(writerSerializer);
        writer.RegisterProvider(new TestProvider(new TestState { Name = "Scout", Level = 7 }));
        writer.ValidateRegistrations();
        writer.SaveToDisk("slot");

        var readerSerializer = new ContextualMetadataJsonSerializer();
        var provider = new TestProvider(new TestState { Name = "Unset", Level = 0 });
        var reader = CreateManager(readerSerializer);
        reader.RegisterProvider(provider);
        reader.ValidateRegistrations();
        readerSerializer.Records.Clear();

        var validation = reader.ValidateSave("slot");
        var loaded = reader.LoadFromDisk("slot");

        Assert.That(validation.IsValid, Is.True);
        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Name, Is.EqualTo("Scout"));
        Assert.That(provider.Current.Level, Is.EqualTo(7));

        var extractRecords = readerSerializer.Records.Where(record => record.Operation == "Extract").ToArray();
        var deserializeRecords = readerSerializer.Records.Where(record => record.Operation == "Deserialize").ToArray();
        Assert.That(extractRecords, Is.Not.Empty);
        Assert.That(deserializeRecords, Is.Not.Empty);
        Assert.That(extractRecords.All(record => record.MetadataReady && record.ProviderMarker == "player:1"), Is.True);
        Assert.That(deserializeRecords.All(record => record.MetadataReady && record.ProviderMarker == "player:1"), Is.True);
    }

    [Test]
    public void LoadBackupSlot_PassesSavedSerializerMetadataToProviderOperations()
    {
        var serializer = new ContextualMetadataJsonSerializer();
        var writer = CreateManager(serializer, backups: true);
        var writerProvider = new TestProvider(new TestState { Name = "First", Level = 1 });
        writer.RegisterProvider(writerProvider);
        writer.ValidateRegistrations();
        writer.SaveToDisk("slot");
        writerProvider.Current = new TestState { Name = "Second", Level = 2 };
        writer.SaveToDisk("slot");

        var readerSerializer = new ContextualMetadataJsonSerializer();
        var readerProvider = new TestProvider(new TestState { Name = "Unset", Level = 0 });
        var reader = CreateManager(readerSerializer, backups: true);
        reader.RegisterProvider(readerProvider);
        reader.ValidateRegistrations();
        readerSerializer.Records.Clear();

        var loaded = reader.LoadBackupSlotFromDisk("slot", 1);

        Assert.That(loaded, Is.True);
        Assert.That(readerProvider.Current.Name, Is.EqualTo("First"));
        Assert.That(readerSerializer.Records.Any(record =>
            record.Operation == "Deserialize" &&
            record.MetadataReady &&
            record.ProviderMarker == "player:1"), Is.True);
    }

    [Test]
    public void Migration_PassesSavedSerializerMetadataToContextualMigration()
    {
        var writerSerializer = new ContextualMetadataJsonSerializer();
        var writer = CreateManager(writerSerializer);
        writer.RegisterProvider(new V1Provider(new V1State { Name = "Scout" }));
        writer.ValidateRegistrations();
        writer.SaveToDisk("slot");

        var readerSerializer = new ContextualMetadataJsonSerializer();
        var readerProvider = new V2Provider(
            new V2State { Name = "Unset", Level = 0 },
            new[]
            {
                new SaveMigrationStep(1, (data, factory) => data.Set("Level", factory.CreateInt(12)))
            });
        var reader = CreateManager(readerSerializer);
        reader.RegisterProvider(readerProvider);
        reader.ValidateRegistrations();
        readerSerializer.Records.Clear();

        var loaded = reader.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(readerProvider.Current.Name, Is.EqualTo("Scout"));
        Assert.That(readerProvider.Current.Level, Is.EqualTo(12));

        var readNode = readerSerializer.Records.Single(record => record.Operation == "DeserializeToNode");
        var writeNode = readerSerializer.Records.Single(record => record.Operation == "SerializeFromNode");
        AssertContext(readNode, "player", 1, typeof(V2State));
        AssertContext(writeNode, "player", 2, typeof(V2State));
        Assert.That(readNode.MetadataReady, Is.True);
        Assert.That(writeNode.MetadataReady, Is.True);
    }

    [Test]
    public void TransformedSaveSerializer_ForwardsContextualProviderOperations()
    {
        var inner = new ContextualMetadataJsonSerializer();
        var serializer = new TransformedSaveSerializer(inner, new IdentityTransform());
        var manager = CreateManager(serializer);
        manager.RegisterProvider(new TestProvider(new TestState { Name = "Scout", Level = 7 }));
        manager.ValidateRegistrations();
        inner.Records.Clear();

        manager.SaveToDisk("slot");

        Assert.That(inner.Records.Any(record =>
            record.Operation == "Serialize" &&
            record.MetadataReady &&
            record.ProviderMarker == "player:1"), Is.True);
    }

    [Test]
    public void TransformedSaveSerializer_ForwardsContextualMigrationOperations()
    {
        var writerInner = new ContextualMetadataJsonSerializer();
        var writer = CreateManager(new TransformedSaveSerializer(writerInner, new IdentityTransform()));
        writer.RegisterProvider(new V1Provider(new V1State { Name = "Scout" }));
        writer.ValidateRegistrations();
        writer.SaveToDisk("slot");

        var readerInner = new ContextualMetadataJsonSerializer();
        var reader = CreateManager(new TransformedSaveSerializer(readerInner, new IdentityTransform()));
        reader.RegisterProvider(new V2Provider(
            new V2State { Name = "Unset", Level = 0 },
            new[] { new SaveMigrationStep(1, (data, factory) => data.Set("Level", factory.CreateInt(12))) }));
        reader.ValidateRegistrations();
        readerInner.Records.Clear();

        var loaded = reader.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(readerInner.Records.Any(record =>
            record.Operation == "DeserializeToNode" &&
            record.SchemaVersion == 1 &&
            record.MetadataReady), Is.True);
        Assert.That(readerInner.Records.Any(record =>
            record.Operation == "SerializeFromNode" &&
            record.SchemaVersion == 2 &&
            record.MetadataReady), Is.True);
    }

    [Test]
    public void CompressedSaveSerializer_ForwardsContextualProviderOperations()
    {
        var inner = new ContextualMetadataJsonSerializer();
        var serializer = new CompressedSaveSerializer(inner);
        var manager = CreateManager(serializer);
        manager.RegisterProvider(new TestProvider(new TestState { Name = "Scout", Level = 7 }));
        manager.ValidateRegistrations();
        inner.Records.Clear();

        manager.SaveToDisk("slot");

        Assert.That(inner.Records.Any(record =>
            record.Operation == "Serialize" &&
            record.MetadataReady &&
            record.ProviderMarker == "player:1"), Is.True);
    }

    [Test]
    public void NonContextualSerializers_StillWorkUnchanged()
    {
        var manager = CreateManager(new JsonSaveSerializer());
        var provider = new TestProvider(new TestState { Name = "Scout", Level = 7 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();

        manager.SaveToDisk("slot");
        provider.Current = new TestState { Name = "Changed", Level = 1 };

        var loaded = manager.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Name, Is.EqualTo("Scout"));
        Assert.That(provider.Current.Level, Is.EqualTo(7));
    }

    private SaveManager<string> CreateManager(ISaveSerializer serializer, bool backups = false)
    {
        return new SaveManager<string>(
            new SaveSystemOptions<string>(
                saveRootPath: _tempRoot,
                serializer: serializer,
                tempFolderName: SaveSystemOptions<string>.DefaultTempFolderName(),
                savePathResolver: identity => identity,
                fileNameResolver: SaveSystemOptions<string>.DefaultFileNameResolver,
                enableBackupSystem: backups,
                backupSystemMaxBackupCount: backups ? 1 : 0));
    }

    private static void AssertContext(ContextRecord record, string saveKey, int schemaVersion, Type stateType)
    {
        Assert.That(record.SaveKey, Is.EqualTo(saveKey));
        Assert.That(record.SchemaVersion, Is.EqualTo(schemaVersion));
        Assert.That(record.StateType, Is.EqualTo(stateType));
        Assert.That(record.Schematic, Is.Not.Null);
    }

    public sealed class TestState
    {
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
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

    private sealed class TestProvider : ISaveProvider<TestState>
    {
        public TestProvider(TestState current)
        {
            Current = current;
        }

        public string SaveKey => "player";
        public int SchemaVersion => 1;
        public int LoadPriority => 0;
        public TestState Current { get; set; }
        public TestState CaptureState() => Current;
        public void RestoreState(TestState state) => Current = state;
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
        public V1State CaptureState() => Current;
        public void RestoreState(V1State state) => Current = state;
    }

    private sealed class V2Provider : ISaveProvider<V2State>, ISaveMigratable
    {
        private readonly SaveMigrationStep[] _migrations;

        public V2Provider(V2State current, SaveMigrationStep[] migrations)
        {
            Current = current;
            _migrations = migrations;
        }

        public string SaveKey => "player";
        public int SchemaVersion => 2;
        public int LoadPriority => 0;
        public V2State Current { get; set; }
        public V2State CaptureState() => Current;
        public void RestoreState(V2State state) => Current = state;
        public ISaveMigrationSource CreateMigrationSource() => new MigrationSource(_migrations);
    }

    private sealed class MigrationSource : ISaveMigrationSource
    {
        public MigrationSource(SaveMigrationStep[] migrations)
        {
            Migrations = migrations;
        }

        public IReadOnlyList<SaveMigrationStep> Migrations { get; }
    }

    private sealed class ContextualMetadataJsonSerializer :
        ISaveSerializer,
        IContextualSaveSerializer,
        ISaveSerializerMetadataHandler
    {
        private readonly JsonSaveSerializer _inner = new JsonSaveSerializer();
        private readonly ContextualMigrationAdapter _migration;

        public ContextualMetadataJsonSerializer()
        {
            _migration = new ContextualMigrationAdapter(this, _inner);
        }

        public List<ContextRecord> Records { get; } = new List<ContextRecord>();

        public string FileExtension => _inner.FileExtension;

        public ISaveMigrationCapableSerializer Migration => _migration;

        public ISaveSerializerMetadataHandler Metadata => this;

        public ISaveSchematic CreateSchematic(Type stateType) => _inner.CreateSchematic(stateType);

        public byte[] Serialize(object data, ISaveSchematic schematic) => _inner.Serialize(data, schematic);

        public object Deserialize(byte[] rawData, ISaveSchematic schematic) => _inner.Deserialize(rawData, schematic);

        public int ExtractSchemaVersion(byte[] serializedData) => _inner.ExtractSchemaVersion(serializedData);

        public byte[] Serialize(object data, SaveSerializerContext context)
        {
            Records.Add(ContextRecord.From("Serialize", context));
            return _inner.Serialize(data, context.Schematic);
        }

        public object Deserialize(byte[] rawData, SaveSerializerContext context)
        {
            Records.Add(ContextRecord.From("Deserialize", context));
            return _inner.Deserialize(rawData, context.Schematic);
        }

        public int ExtractSchemaVersion(byte[] serializedData, SaveSerializerContext context)
        {
            Records.Add(ContextRecord.From("Extract", context));
            return _inner.ExtractSchemaVersion(serializedData);
        }

        public void WriteMetadata(SaveSerializerMetadataWriteContext context)
        {
            context.Metadata.Global["ready"] = "true";

            foreach (var provider in context.Providers)
            {
                context.Metadata.GetOrCreateProvider(provider.SaveKey)["marker"] =
                    provider.SaveKey + ":" + provider.SchemaVersion;
            }
        }

        public void ValidateMetadata(SaveSerializerMetadataValidationContext context)
        {
        }

        private sealed class ContextualMigrationAdapter :
            ISaveMigrationCapableSerializer,
            IContextualSaveMigrationCapableSerializer
        {
            private readonly ContextualMetadataJsonSerializer _owner;
            private readonly JsonSaveSerializer _inner;

            public ContextualMigrationAdapter(ContextualMetadataJsonSerializer owner, JsonSaveSerializer inner)
            {
                _owner = owner;
                _inner = inner;
            }

            public ISaveDataNodeFactory NodeFactory => _inner.NodeFactory;

            public ISaveDataNode DeserializeToNode(byte[] data) => _inner.DeserializeToNode(data);

            public byte[] SerializeFromNode(ISaveDataNode node) => _inner.SerializeFromNode(node);

            public ISaveDataNode DeserializeToNode(byte[] data, SaveSerializerContext context)
            {
                _owner.Records.Add(ContextRecord.From("DeserializeToNode", context));
                return ((IContextualSaveMigrationCapableSerializer)_inner).DeserializeToNode(data, context);
            }

            public byte[] SerializeFromNode(ISaveDataNode node, SaveSerializerContext context)
            {
                _owner.Records.Add(ContextRecord.From("SerializeFromNode", context));
                return ((IContextualSaveMigrationCapableSerializer)_inner).SerializeFromNode(node, context);
            }
        }
    }

    private sealed class ContextRecord
    {
        public string Operation { get; private set; } = string.Empty;
        public string SaveKey { get; private set; } = string.Empty;
        public int SchemaVersion { get; private set; }
        public Type StateType { get; private set; } = typeof(object);
        public ISaveSchematic? Schematic { get; private set; }
        public bool MetadataReady { get; private set; }
        public string? ProviderMarker { get; private set; }

        public static ContextRecord From(string operation, SaveSerializerContext context)
        {
            context.SerializerMetadata.Providers.TryGetValue(context.SaveKey, out var providerMetadata);
            string? marker = null;
            providerMetadata?.TryGetValue("marker", out marker);

            return new ContextRecord
            {
                Operation = operation,
                SaveKey = context.SaveKey,
                SchemaVersion = context.SchemaVersion,
                StateType = context.StateType,
                Schematic = context.Schematic,
                MetadataReady = context.SerializerMetadata.Global.TryGetValue("ready", out var ready) && ready == "true",
                ProviderMarker = marker
            };
        }
    }

    private sealed class IdentityTransform : ISavePayloadTransform
    {
        public string FileExtensionSuffix => ".id";
        public byte[] Encode(byte[] data) => data;
        public byte[] Decode(byte[] data) => data;
    }
}
