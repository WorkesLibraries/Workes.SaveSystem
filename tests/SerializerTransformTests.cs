using System;
using System.IO;
using System.Text;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class SerializerTransformTests
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
    public void Wrap_ComposesFileExtensionAndTransformsProviderAndMetadataFiles()
    {
        var serializer = SaveSerializerTransforms.Wrap(new JsonSaveSerializer(), new XorTransform());
        var manager = CreateManager(serializer);
        manager.RegisterProvider(new TestProvider(new TestState { Value = 7 }));
        manager.ValidateRegistrations();

        manager.SaveToDisk("slot");

        var providerPath = Path.Combine(_tempRoot, "slot", "player.json.xor");
        var metadataPath = Path.Combine(_tempRoot, "slot", "metadata.json.xor");
        Assert.That(File.Exists(providerPath), Is.True);
        Assert.That(File.Exists(metadataPath), Is.True);
        Assert.That(Encoding.UTF8.GetString(File.ReadAllBytes(providerPath)), Does.Not.Contain("SchemaVersion"));
        Assert.That(serializer.FileExtension, Is.EqualTo(".json.xor"));
    }

    [Test]
    public void Wrap_RoundTripsSaveAndLoad()
    {
        var serializer = SaveSerializerTransforms.Wrap(new JsonSaveSerializer(), new XorTransform());
        var writer = CreateManager(serializer);
        writer.RegisterProvider(new TestProvider(new TestState { Value = 7 }));
        writer.ValidateRegistrations();
        writer.SaveToDisk("slot");

        var provider = new TestProvider(new TestState { Value = 0 });
        var reader = CreateManager(serializer);
        reader.RegisterProvider(provider);
        reader.ValidateRegistrations();

        var loaded = reader.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Value, Is.EqualTo(7));
    }

    [Test]
    public void Wrap_ExtractSchemaVersion_DecodesBeforeDelegating()
    {
        var serializer = SaveSerializerTransforms.Wrap(new JsonSaveSerializer(), new XorTransform());
        var schematic = serializer.CreateSchematic(typeof(TestState));
        schematic.SchemaVersion = 3;
        var serialized = serializer.Serialize(new TestState { Value = 7 }, schematic);

        var schemaVersion = serializer.ExtractSchemaVersion(serialized);

        Assert.That(schemaVersion, Is.EqualTo(3));
        Assert.That(Encoding.UTF8.GetString(serialized), Does.Not.Contain("SchemaVersion"));
    }

    [Test]
    public void Wrap_WhenInnerIsMigrationCapable_ReturnsMigrationCapableSerializer()
    {
        var serializer = SaveSerializerTransforms.Wrap(new JsonSaveSerializer(), new XorTransform());

        Assert.That(serializer, Is.InstanceOf<ISaveMigrationCapableSerializer>());
    }

    [Test]
    public void Wrap_WhenInnerIsNotMigrationCapable_DoesNotReturnMigrationCapableSerializer()
    {
        var serializer = SaveSerializerTransforms.Wrap(new NonMigrationJsonSerializer(), new XorTransform());

        Assert.That(serializer, Is.Not.InstanceOf<ISaveMigrationCapableSerializer>());
    }

    [Test]
    public void LoadFromDisk_WithTransformedMigrationCapableSerializer_AppliesMigrations()
    {
        var serializer = SaveSerializerTransforms.Wrap(new JsonSaveSerializer(), new XorTransform());
        var oldManager = CreateManager(serializer);
        oldManager.RegisterProvider(new V1Provider(new V1State { Name = "Scout" }));
        oldManager.ValidateRegistrations();
        oldManager.SaveToDisk("slot");

        var provider = new V2Provider(
            new V2State { Name = "Unset", Level = 0 },
            new[]
            {
                new SaveMigrationStep(1, (data, factory) => data.Set("Level", factory.CreateInt(12)))
            });
        var newManager = CreateManager(serializer);
        newManager.RegisterProvider(provider);
        newManager.ValidateRegistrations();

        var loaded = newManager.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Name, Is.EqualTo("Scout"));
        Assert.That(provider.Current.Level, Is.EqualTo(12));
    }

    [Test]
    public void Wrap_WhenInnerSupportsMetadata_DelegatesMetadataCallbacks()
    {
        var serializer = SaveSerializerTransforms.Wrap(new MetadataJsonSerializer(), new XorTransform());
        var manager = CreateManager(serializer);
        manager.RegisterProvider(new TestProvider(new TestState { Value = 7 }));
        manager.ValidateRegistrations();

        manager.SaveToDisk("slot");

        var metadataBytes = File.ReadAllBytes(Path.Combine(_tempRoot, "slot", "metadata.json.xor"));
        var decodedMetadata = Encoding.UTF8.GetString(new XorTransform().Decode(metadataBytes));
        Assert.That(serializer, Is.InstanceOf<ISaveSerializerMetadataHandler>());
        Assert.That(decodedMetadata, Does.Contain("\"transform-metadata\""));
    }

    [Test]
    public void Wrap_WhenInnerDoesNotSupportMetadata_DoesNotReturnMetadataHandler()
    {
        var serializer = SaveSerializerTransforms.Wrap(new JsonSaveSerializer(), new XorTransform());

        Assert.That(serializer, Is.Not.InstanceOf<ISaveSerializerMetadataHandler>());
    }

    [Test]
    public void Wrap_RejectsInvalidTransformSuffix()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            SaveSerializerTransforms.Wrap(new JsonSaveSerializer(), new InvalidSuffixTransform()));

        Assert.That(ex!.Message, Does.Contain("must start with"));
    }

    [Test]
    public void LoadFromDisk_WhenTransformDecodeFails_Throws()
    {
        var serializer = SaveSerializerTransforms.Wrap(new JsonSaveSerializer(), new ThrowingDecodeTransform());
        var manager = CreateManager(serializer);
        manager.RegisterProvider(new TestProvider(new TestState { Value = 7 }));
        manager.ValidateRegistrations();
        Directory.CreateDirectory(Path.Combine(_tempRoot, "slot"));
        File.WriteAllBytes(Path.Combine(_tempRoot, "slot", "metadata.json.bad"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(_tempRoot, "slot", "player.json.bad"), new byte[] { 1, 2, 3 });

        var ex = Assert.Throws<InvalidOperationException>(() => manager.LoadFromDisk("slot"));

        Assert.That(ex!.ToString(), Does.Contain("Decode failed"));
    }

    private SaveManager<string> CreateManager(ISaveSerializer serializer)
    {
        return new SaveManager<string>(
            new SaveSystemOptions<string>(
                saveRootPath: _tempRoot,
                serializer: serializer,
                tempFolderName: SaveSystemOptions<string>.DefaultTempFolderName(),
                savePathResolver: identity => identity,
                fileNameResolver: SaveSystemOptions<string>.DefaultFileNameResolver));
    }

    public sealed class TestState
    {
        public int Value { get; set; }
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

        public System.Collections.Generic.IReadOnlyList<SaveMigrationStep> Migrations { get; }
    }

    private sealed class XorTransform : ISavePayloadTransform
    {
        public string FileExtensionSuffix => ".xor";

        public byte[] Encode(byte[] data) => Transform(data);

        public byte[] Decode(byte[] data) => Transform(data);

        private static byte[] Transform(byte[] data)
        {
            var transformed = new byte[data.Length];
            for (var i = 0; i < data.Length; i++)
                transformed[i] = (byte)(data[i] ^ 0x5A);
            return transformed;
        }
    }

    private sealed class InvalidSuffixTransform : ISavePayloadTransform
    {
        public string FileExtensionSuffix => "bad";
        public byte[] Encode(byte[] data) => data;
        public byte[] Decode(byte[] data) => data;
    }

    private sealed class ThrowingDecodeTransform : ISavePayloadTransform
    {
        public string FileExtensionSuffix => ".bad";
        public byte[] Encode(byte[] data) => data;
        public byte[] Decode(byte[] data) => throw new InvalidOperationException("Decode failed.");
    }

    private sealed class NonMigrationJsonSerializer : ISaveSerializer
    {
        private readonly JsonSaveSerializer _inner = new JsonSaveSerializer();

        public string FileExtension => _inner.FileExtension;
        public ISaveSchematic CreateSchematic(Type stateType) => _inner.CreateSchematic(stateType);
        public byte[] Serialize(object data, ISaveSchematic schematic) => _inner.Serialize(data, schematic);
        public object Deserialize(byte[] rawData, ISaveSchematic schematic) => _inner.Deserialize(rawData, schematic);
        public int ExtractSchemaVersion(byte[] serializedData) => _inner.ExtractSchemaVersion(serializedData);
    }

    private sealed class MetadataJsonSerializer : ISaveSerializer, ISaveSerializerMetadataHandler
    {
        private readonly JsonSaveSerializer _inner = new JsonSaveSerializer();

        public string FileExtension => _inner.FileExtension;
        public ISaveSchematic CreateSchematic(Type stateType) => _inner.CreateSchematic(stateType);
        public byte[] Serialize(object data, ISaveSchematic schematic) => _inner.Serialize(data, schematic);
        public object Deserialize(byte[] rawData, ISaveSchematic schematic) => _inner.Deserialize(rawData, schematic);
        public int ExtractSchemaVersion(byte[] serializedData) => _inner.ExtractSchemaVersion(serializedData);

        public void WriteMetadata(SaveSerializerMetadataWriteContext context)
        {
            context.Metadata.Global["kind"] = "transform-metadata";
        }

        public void ValidateMetadata(SaveSerializerMetadataValidationContext context)
        {
        }
    }
}
