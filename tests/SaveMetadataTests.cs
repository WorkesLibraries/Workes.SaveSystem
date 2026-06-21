using System;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class SaveMetadataTests
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
    public void ReadSaveMetadata_ReturnsMetadataForSavedSlot()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);

        var metadata = manager.ReadSaveMetadata("slot");

        Assert.That(metadata, Is.Not.Null);
        Assert.That(metadata!.SaveId, Is.Not.Empty);
        Assert.That(metadata.CreatedAtUtc, Is.Not.EqualTo(default(DateTimeOffset)));
        Assert.That(metadata.LastWrittenAtUtc, Is.GreaterThanOrEqualTo(metadata.CreatedAtUtc));
    }

    [Test]
    public void ReadSaveMetadata_PreservesSaveIdAndCreatedTimeAcrossOverwrites()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        var firstMetadata = manager.ReadSaveMetadata("slot")!;
        Thread.Sleep(20);

        SaveValue(manager, provider, "slot", 2);
        var secondMetadata = manager.ReadSaveMetadata("slot")!;

        Assert.That(secondMetadata.SaveId, Is.EqualTo(firstMetadata.SaveId));
        Assert.That(secondMetadata.CreatedAtUtc, Is.EqualTo(firstMetadata.CreatedAtUtc));
        Assert.That(secondMetadata.LastWrittenAtUtc, Is.GreaterThan(firstMetadata.LastWrittenAtUtc));
    }

    [Test]
    public void SaveToDisk_ThrowsWhenExistingMetadataIsCorrupt()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        File.WriteAllText(Path.Combine(_tempRoot, "slot", "metadata.json"), "{}");
        provider.Current = new TestState { Value = 2 };

        var ex = Assert.Throws<InvalidOperationException>(() => manager.SaveToDisk("slot"));

        Assert.That(ex!.Message, Does.Contain("Failed to parse JSON save payload"));
        Assert.That(ReadValueFromFolder(Path.Combine(_tempRoot, "slot")), Is.EqualTo(1));
    }

    [Test]
    public void ForceSaveToDisk_OverwritesSaveWithCorruptMetadata()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        File.WriteAllText(Path.Combine(_tempRoot, "slot", "metadata.json"), "{}");
        provider.Current = new TestState { Value = 2 };

        manager.ForceSaveToDisk("slot");

        var metadata = manager.ReadSaveMetadata("slot");
        Assert.That(metadata, Is.Not.Null);
        Assert.That(ReadValueFromFolder(Path.Combine(_tempRoot, "slot")), Is.EqualTo(2));
    }

    [Test]
    public void ForceSaveToDisk_OverwritesSaveWithMissingMetadata()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        File.Delete(Path.Combine(_tempRoot, "slot", "metadata.json"));
        provider.Current = new TestState { Value = 2 };

        manager.ForceSaveToDisk("slot");

        var metadata = manager.ReadSaveMetadata("slot");
        Assert.That(metadata, Is.Not.Null);
        Assert.That(ReadValueFromFolder(Path.Combine(_tempRoot, "slot")), Is.EqualTo(2));
    }

    [Test]
    public void ForceSaveToDisk_ReplacesExistingFolderFilesWithActiveSerializerFiles()
    {
        var slotPath = Path.Combine(_tempRoot, "slot");
        Directory.CreateDirectory(slotPath);
        File.WriteAllText(Path.Combine(slotPath, "metadata.bin"), "old metadata");
        File.WriteAllText(Path.Combine(slotPath, "player.bin"), "old player");

        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        provider.Current = new TestState { Value = 2 };
        manager.ValidateRegistrations();

        manager.ForceSaveToDisk("slot");

        Assert.That(File.Exists(Path.Combine(_tempRoot, "slot", "metadata.json")), Is.True);
        Assert.That(File.Exists(Path.Combine(_tempRoot, "slot", "player.json")), Is.True);
        Assert.That(File.Exists(Path.Combine(_tempRoot, "slot", "metadata.bin")), Is.False);
        Assert.That(File.Exists(Path.Combine(_tempRoot, "slot", "player.bin")), Is.False);
    }

    [Test]
    public void ForceSaveToDisk_CreatesNewMetadataIdentity()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        var firstMetadata = manager.ReadSaveMetadata("slot")!;
        Thread.Sleep(20);
        provider.Current = new TestState { Value = 2 };

        manager.ForceSaveToDisk("slot");

        var secondMetadata = manager.ReadSaveMetadata("slot")!;
        Assert.That(secondMetadata.SaveId, Is.Not.EqualTo(firstMetadata.SaveId));
        Assert.That(secondMetadata.CreatedAtUtc, Is.GreaterThan(firstMetadata.CreatedAtUtc));
    }

    [Test]
    public void ForceSaveToDisk_WithBackupsEnabled_LeavesExistingBackupsUntouched()
    {
        var manager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 2);
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        SaveValue(manager, provider, "slot", 2);
        File.WriteAllText(Path.Combine(_tempRoot, "slot", "metadata.json"), "{}");
        provider.Current = new TestState { Value = 3 };

        manager.ForceSaveToDisk("slot");

        Assert.That(ReadValueFromFolder(Path.Combine(_tempRoot, "slot")), Is.EqualTo(3));
        Assert.That(ReadValueFromFolder(Path.Combine(_tempRoot, "_backup", "slot_0001")), Is.EqualTo(1));
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "_backup", "slot_0002")), Is.False);
    }

    [Test]
    public void ReadSaveMetadata_ReturnsNullWhenMetadataDoesNotExist()
    {
        var manager = CreateManager();

        var metadata = manager.ReadSaveMetadata("missing");

        Assert.That(metadata, Is.Null);
    }

    [Test]
    public void ReadSaveMetadata_ThrowsWhenMetadataIsInvalid()
    {
        var manager = CreateManager();
        var slotPath = Path.Combine(_tempRoot, "slot");
        Directory.CreateDirectory(slotPath);
        File.WriteAllText(Path.Combine(slotPath, "metadata.json"), "{}");

        var ex = Assert.Throws<InvalidOperationException>(() => manager.ReadSaveMetadata("slot"));

        Assert.That(ex!.Message, Does.Contain("Failed to deserialize metadata.json"));
    }

    [Test]
    public void ReadBackupSlotMetadata_ReturnsMetadataForBackupSlot()
    {
        var manager = CreateManager(enableBackupSystem: true, backupSystemMaxBackupCount: 2);
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        var originalMetadata = manager.ReadSaveMetadata("slot")!;
        SaveValue(manager, provider, "slot", 2);

        var backupMetadata = manager.ReadBackupSlotMetadata("slot", slotNumber: 1);

        Assert.That(backupMetadata, Is.Not.Null);
        Assert.That(backupMetadata!.SaveId, Is.EqualTo(originalMetadata.SaveId));
        Assert.That(backupMetadata.CreatedAtUtc, Is.EqualTo(originalMetadata.CreatedAtUtc));
    }

    [Test]
    public void SaveToDisk_WithMetadataAwareSerializer_WritesGlobalAndProviderSerializerMetadata()
    {
        var serializer = new MetadataAwareJsonSerializer();
        var manager = CreateManager(serializer: serializer);
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();

        manager.SaveToDisk("slot");

        var metadata = ReadMetadataData("slot");
        Assert.That(metadata["SerializerMetadata"]?["Global"]?["format"]?.Value<string>(), Is.EqualTo("metadata-aware-json"));
        Assert.That(metadata["SerializerMetadata"]?["Providers"]?["player"]?["schemaVersion"]?.Value<string>(), Is.EqualTo("1"));
        Assert.That(metadata["SerializerMetadata"]?["Providers"]?["player"]?["stateType"]?.Value<string>(), Is.EqualTo(typeof(TestState).FullName));
    }

    [Test]
    public void SaveToDisk_WithMetadataAwareSerializer_TreatsMissingSerializerMetadataAsEmpty()
    {
        var serializer = new MetadataAwareJsonSerializer();
        var manager = CreateManager(serializer: serializer);
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();
        WriteOldMetadataWithoutSerializerMetadata("slot");

        manager.SaveToDisk("slot");

        var metadata = ReadMetadataData("slot");
        Assert.That(metadata["SaveId"]?.Value<string>(), Is.EqualTo("old-save-id"));
        Assert.That(metadata["SerializerMetadata"]?["Global"]?["format"]?.Value<string>(), Is.EqualTo("metadata-aware-json"));
    }

    [Test]
    public void SaveToDisk_WhenSerializerMetadataValidationFails_RemovesTempArtifacts()
    {
        var serializer = new MetadataAwareJsonSerializer { WriteInvalidMetadata = true };
        var manager = CreateManager(serializer: serializer);
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();

        var ex = Assert.Throws<InvalidOperationException>(() => manager.SaveToDisk("slot"));

        Assert.That(ex!.Message, Does.Contain("Serializer metadata format is invalid."));
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "slot_tmp")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_tempRoot, "slot")), Is.False);
    }

    [Test]
    public void RecoverSave_WhenTempSerializerMetadataIsInvalid_RejectsTempCandidate()
    {
        var serializer = new MetadataAwareJsonSerializer();
        var manager = CreateManager(serializer: serializer);
        var provider = new TestProvider(new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        SaveValue(manager, provider, "slot", 1);
        var mainPath = Path.Combine(_tempRoot, "slot");
        var tempPath = Path.Combine(_tempRoot, "slot_tmp");
        CopyDirectory(mainPath, tempPath);
        SetMetadataFormat(tempPath, "invalid");
        provider.Current = new TestState { Value = 2 };

        manager.RecoverSave("slot");

        Assert.That(Directory.Exists(tempPath), Is.False);
        Assert.That(ReadValueFromFolder(mainPath), Is.EqualTo(1));
    }

    [Test]
    public void SaveMetadataInfo_DoesNotExposeSerializerMetadata()
    {
        var properties = typeof(SaveMetadataInfo)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.That(properties, Does.Not.Contain("SerializerMetadata"));
    }

    [Test]
    public void ReadBackupSlotMetadata_ReturnsNullWhenBackupMetadataDoesNotExist()
    {
        var manager = CreateManager();

        var metadata = manager.ReadBackupSlotMetadata("slot", slotNumber: 1);

        Assert.That(metadata, Is.Null);
    }

    [Test]
    public void ReadBackupSlotMetadata_RejectsInvalidSlotNumbers()
    {
        var manager = CreateManager();

        var ex = Assert.Throws<ArgumentException>(() => manager.ReadBackupSlotMetadata("slot", slotNumber: 0));

        Assert.That(ex!.ParamName, Is.EqualTo("slotNumber"));
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

    private static void SaveValue(
        SaveManager<string> manager,
        TestProvider provider,
        string slot,
        int value)
    {
        provider.Current = new TestState { Value = value };
        manager.ValidateRegistrations();
        manager.SaveToDisk(slot);
    }

    private static int ReadValueFromFolder(string folderPath)
    {
        var json = File.ReadAllText(Path.Combine(folderPath, "player.json"));
        var valueMarker = "\"Value\": ";
        var valueIndex = json.IndexOf(valueMarker, StringComparison.Ordinal);
        if (valueIndex < 0)
            throw new InvalidOperationException("Saved value was not found.");

        valueIndex += valueMarker.Length;
        var valueEnd = json.IndexOfAny(new[] { '\r', '\n', ',' }, valueIndex);
        if (valueEnd < 0)
            valueEnd = json.Length;

        return int.Parse(json.Substring(valueIndex, valueEnd - valueIndex).Trim());
    }

    private JObject ReadMetadataData(string slot)
    {
        var metadataPath = Path.Combine(_tempRoot, slot, "metadata.json");
        var envelope = JObject.Parse(File.ReadAllText(metadataPath));
        return (JObject)envelope["Data"]!;
    }

    private void WriteOldMetadataWithoutSerializerMetadata(string slot)
    {
        var slotPath = Path.Combine(_tempRoot, slot);
        Directory.CreateDirectory(slotPath);
        var envelope = new JObject
        {
            ["SchemaVersion"] = 1,
            ["Data"] = new JObject
            {
                ["SaveId"] = "old-save-id",
                ["CreatedAtUtc"] = DateTimeOffset.UtcNow.AddMinutes(-5),
                ["LastWrittenAtUtc"] = DateTimeOffset.UtcNow.AddMinutes(-5)
            }
        };

        File.WriteAllText(Path.Combine(slotPath, "metadata.json"), envelope.ToString());
    }

    private static void SetMetadataFormat(string folderPath, string format)
    {
        var metadataPath = Path.Combine(folderPath, "metadata.json");
        var envelope = JObject.Parse(File.ReadAllText(metadataPath));
        envelope["Data"]!["SerializerMetadata"]!["Global"]!["format"] = format;
        File.WriteAllText(metadataPath, envelope.ToString());
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

        public TestState CaptureState()
        {
            return Current;
        }

        public void RestoreState(TestState state)
        {
            Current = state;
        }
    }

    private sealed class MetadataAwareJsonSerializer : ISaveSerializer, ISaveSerializerMetadataHandler
    {
        private readonly JsonSaveSerializer _inner = new JsonSaveSerializer();

        public bool WriteInvalidMetadata { get; set; }

        public string FileExtension => _inner.FileExtension;

        public ISaveMigrationCapableSerializer? Migration => _inner.Migration;

        public ISaveSerializerMetadataHandler? Metadata => this;

        public ISaveSchematic CreateSchematic(Type stateType)
        {
            return _inner.CreateSchematic(stateType);
        }

        public byte[] Serialize(object data, ISaveSchematic schematic)
        {
            return _inner.Serialize(data, schematic);
        }

        public object Deserialize(byte[] rawData, ISaveSchematic schematic)
        {
            return _inner.Deserialize(rawData, schematic);
        }

        public int ExtractSchemaVersion(byte[] serializedData)
        {
            return _inner.ExtractSchemaVersion(serializedData);
        }

        public void WriteMetadata(SaveSerializerMetadataWriteContext context)
        {
            context.Metadata.Global["format"] = WriteInvalidMetadata ? "invalid" : "metadata-aware-json";

            foreach (var provider in context.Providers)
            {
                var providerMetadata = context.Metadata.GetOrCreateProvider(provider.SaveKey);
                providerMetadata["schemaVersion"] = provider.SchemaVersion.ToString();
                providerMetadata["stateType"] = provider.StateType.FullName ?? provider.StateType.Name;
            }
        }

        public void ValidateMetadata(SaveSerializerMetadataValidationContext context)
        {
            if (!context.Metadata.Global.TryGetValue("format", out var format))
                return;

            if (format != "metadata-aware-json")
                throw new InvalidOperationException("Serializer metadata format is invalid.");
        }
    }
}
