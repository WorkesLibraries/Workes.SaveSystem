using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class SerializerCompressionTests
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
    public void CompressedSaveSerializer_ComposesFileExtensionAndCompressesProviderAndMetadataFiles()
    {
        var serializer = new CompressedSaveSerializer(new JsonSaveSerializer(JsonSaveFormatting.Compact));
        var manager = CreateManager(serializer);
        manager.RegisterProvider(new TestProvider(new TestState { Value = 7 }));
        manager.ValidateRegistrations();

        manager.SaveToDisk("slot");

        var providerPath = Path.Combine(_tempRoot, "slot", "player.json.gz");
        var metadataPath = Path.Combine(_tempRoot, "slot", "metadata.json.gz");
        Assert.That(File.Exists(providerPath), Is.True);
        Assert.That(File.Exists(metadataPath), Is.True);
        Assert.That(serializer.FileExtension, Is.EqualTo(".json.gz"));
        Assert.That(Encoding.UTF8.GetString(File.ReadAllBytes(providerPath)), Does.Not.Contain("SchemaVersion"));
        Assert.That(DecompressToText(providerPath), Does.Contain("\"SchemaVersion\":1"));
        Assert.That(DecompressToText(metadataPath), Does.Contain("\"SaveId\""));
    }

    [Test]
    public void CompressedSaveSerializer_RoundTripsSaveAndLoad()
    {
        var serializer = new CompressedSaveSerializer(new JsonSaveSerializer(JsonSaveFormatting.Compact));
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
    public void CompressedSaveSerializer_ExtractSchemaVersion_DecompressesBeforeDelegating()
    {
        var serializer = new CompressedSaveSerializer(new JsonSaveSerializer(JsonSaveFormatting.Compact));
        var schematic = serializer.CreateSchematic(typeof(TestState));
        schematic.SchemaVersion = 3;
        var serialized = serializer.Serialize(new TestState { Value = 7 }, schematic);

        var schemaVersion = serializer.ExtractSchemaVersion(serialized);

        Assert.That(schemaVersion, Is.EqualTo(3));
        Assert.That(Encoding.UTF8.GetString(serialized), Does.Not.Contain("SchemaVersion"));
    }

    [Test]
    public void CompressedSaveSerializer_WhenInnerIsMigrationCapable_ExposesMigration()
    {
        var serializer = new CompressedSaveSerializer(new JsonSaveSerializer());

        Assert.That(serializer.Migration, Is.Not.Null);
    }

    [Test]
    public void CompressedSaveSerializer_WhenInnerIsNotMigrationCapable_HasNoMigration()
    {
        var serializer = new CompressedSaveSerializer(new NonMigrationJsonSerializer());

        Assert.That(serializer.Migration, Is.Null);
    }

    [Test]
    public void CompressedSaveSerializer_WhenInnerSupportsMetadata_DelegatesMetadata()
    {
        var serializer = new CompressedSaveSerializer(new MetadataJsonSerializer());
        var manager = CreateManager(serializer);
        manager.RegisterProvider(new TestProvider(new TestState { Value = 7 }));
        manager.ValidateRegistrations();

        manager.SaveToDisk("slot");

        var metadataText = DecompressToText(Path.Combine(_tempRoot, "slot", "metadata.json.gz"));
        Assert.That(serializer.Metadata, Is.Not.Null);
        Assert.That(metadataText, Does.Contain("\"compressed-metadata\""));
    }

    [Test]
    public void CompressedSaveSerializer_WhenInnerDoesNotSupportMetadata_HasNoMetadata()
    {
        var serializer = new CompressedSaveSerializer(new JsonSaveSerializer());

        Assert.That(serializer.Metadata, Is.Null);
    }

    [Test]
    public void LoadFromDisk_WithCompressedMigrationCapableSerializer_AppliesMigrations()
    {
        var serializer = new CompressedSaveSerializer(new JsonSaveSerializer(JsonSaveFormatting.Compact));
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
    public void RecoverSave_WithCompressedTempSave_ValidatesAndPromotesTemp()
    {
        var serializer = new CompressedSaveSerializer(new JsonSaveSerializer(JsonSaveFormatting.Compact));
        var manager = CreateManager(serializer);
        var provider = new TestProvider(new TestState { Value = 7 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();
        manager.SaveToDisk("slot");
        CopyDirectory(Path.Combine(_tempRoot, "slot"), Path.Combine(_tempRoot, "slot_tmp"));

        manager.RecoverSave("slot");

        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "slot_tmp")), Is.False);
        Assert.That(File.Exists(Path.Combine(_tempRoot, "slot", "player.json.gz")), Is.True);
        Assert.That(DecompressToText(Path.Combine(_tempRoot, "slot", "player.json.gz")), Does.Contain("\"Value\":7"));
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

    private static string DecompressToText(string path)
    {
        using var input = File.OpenRead(path);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static void CopyDirectory(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);

        foreach (var filePath in Directory.GetFiles(sourcePath))
        {
            File.Copy(filePath, Path.Combine(destinationPath, Path.GetFileName(filePath)));
        }
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

    private sealed class NonMigrationJsonSerializer : ISaveSerializer
    {
        private readonly JsonSaveSerializer _inner = new JsonSaveSerializer();

        public string FileExtension => _inner.FileExtension;
        public ISaveMigrationCapableSerializer? Migration => null;
        public ISaveSerializerMetadataHandler? Metadata => null;
        public ISaveSchematic CreateSchematic(Type stateType) => _inner.CreateSchematic(stateType);
        public byte[] Serialize(object data, ISaveSchematic schematic) => _inner.Serialize(data, schematic);
        public object Deserialize(byte[] rawData, ISaveSchematic schematic) => _inner.Deserialize(rawData, schematic);
        public int ExtractSchemaVersion(byte[] serializedData) => _inner.ExtractSchemaVersion(serializedData);
    }

    private sealed class MetadataJsonSerializer : ISaveSerializer, ISaveSerializerMetadataHandler
    {
        private readonly JsonSaveSerializer _inner = new JsonSaveSerializer();

        public string FileExtension => _inner.FileExtension;
        public ISaveMigrationCapableSerializer? Migration => null;
        public ISaveSerializerMetadataHandler? Metadata => this;
        public ISaveSchematic CreateSchematic(Type stateType) => _inner.CreateSchematic(stateType);
        public byte[] Serialize(object data, ISaveSchematic schematic) => _inner.Serialize(data, schematic);
        public object Deserialize(byte[] rawData, ISaveSchematic schematic) => _inner.Deserialize(rawData, schematic);
        public int ExtractSchemaVersion(byte[] serializedData) => _inner.ExtractSchemaVersion(serializedData);

        public void WriteMetadata(SaveSerializerMetadataWriteContext context)
        {
            context.Metadata.Global["kind"] = "compressed-metadata";
        }

        public void ValidateMetadata(SaveSerializerMetadataValidationContext context)
        {
        }
    }
}
