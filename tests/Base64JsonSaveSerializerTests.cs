using System;
using System.Collections.Generic;
using System.IO;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class Base64JsonSaveSerializerTests
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
    public void Serialize_ProducesBase64JsonPayloadAndExtractsSchemaVersion()
    {
        var serializer = new Base64JsonSaveSerializer();
        var schematic = serializer.CreateSchematic(typeof(TestState));
        schematic.SchemaVersion = 3;

        var serialized = serializer.Serialize(new TestState { Name = "Scout", Level = 12 }, schematic);
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(serialized));

        Assert.That(serialized, Does.Not.StartWith("{"));
        Assert.That(decoded.TrimStart(), Does.StartWith("{"));
        Assert.That(decoded, Does.Contain("\"SchemaVersion\": 3"));
        Assert.That(decoded, Does.Contain("\"Name\": \"Scout\""));
        Assert.That(serializer.ExtractSchemaVersion(serialized), Is.EqualTo(3));
    }

    [Test]
    public void SaveManager_WithBase64JsonSerializer_SavesAndLoadsProviderState()
    {
        var manager = CreateManager();
        var provider = new TestProvider(new TestState { Name = "Scout", Level = 12 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();
        manager.SaveToDisk("slot");
        provider.Current = new TestState { Name = "Changed", Level = 1 };

        var loaded = manager.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(provider.Current.Name, Is.EqualTo("Scout"));
        Assert.That(provider.Current.Level, Is.EqualTo(12));
        Assert.That(File.Exists(Path.Combine(_tempRoot, "slot", "player.bin")), Is.True);
        Assert.That(File.Exists(Path.Combine(_tempRoot, "slot", "metadata.bin")), Is.True);
        Assert.That(File.Exists(Path.Combine(_tempRoot, "slot", "metadata.json")), Is.False);
    }

    [Test]
    public void LoadFromDisk_WithBase64JsonSerializer_AppliesMigrationHelpers()
    {
        var oldManager = CreateManager();
        oldManager.RegisterProvider(new V1Provider(new V1State { Name = "Scout" }));
        oldManager.ValidateRegistrations();
        oldManager.SaveToDisk("slot");

        var newProvider = new MigratingProvider(new V2State { Name = "Unset", Level = 0 });
        var newManager = CreateManager();
        newManager.RegisterProvider(newProvider);
        newManager.ValidateRegistrations();

        var loaded = newManager.LoadFromDisk("slot");

        Assert.That(loaded, Is.True);
        Assert.That(newProvider.Current.Name, Is.EqualTo("Scout"));
        Assert.That(newProvider.Current.Level, Is.EqualTo(12));
    }

    [Test]
    public void ExtractSchemaVersion_RejectsInvalidBase64JsonPayload()
    {
        var serializer = new Base64JsonSaveSerializer();

        var ex = Assert.Throws<InvalidOperationException>(() => serializer.ExtractSchemaVersion("not-base64"));

        Assert.That(ex!.Message, Does.Contain("Failed to extract schema version"));
    }

    [Test]
    public void Deserialize_RejectsNullPayloadData()
    {
        var serializer = new Base64JsonSaveSerializer();
        var schematic = serializer.CreateSchematic(typeof(TestState));
        schematic.SchemaVersion = 1;
        var payload = serializer.NodeFactory.CreateObject();
        payload.Set("SchemaVersion", serializer.NodeFactory.CreateInt(1));
        payload.Set("Data", serializer.NodeFactory.CreateNull());
        var serialized = serializer.SerializeFromNode(payload);

        var ex = Assert.Throws<InvalidOperationException>(() => serializer.Deserialize(serialized, schematic));

        Assert.That(ex!.Message, Does.Contain("Failed to parse Base64 JSON save payload"));
        Assert.That(ex.InnerException!.Message, Does.Contain("payload data was null"));
    }

    private SaveManager<string> CreateManager()
    {
        return new SaveManager<string>(
            SaveSystemOptions.Create(
                saveRootPath: _tempRoot,
                serializer: new Base64JsonSaveSerializer()));
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

        public TestState CaptureState()
        {
            return Current;
        }

        public void RestoreState(TestState state)
        {
            Current = state;
        }
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
        public MigratingProvider(V2State current)
        {
            Current = current;
        }

        public string SaveKey => "player";
        public int SchemaVersion => 2;
        public int LoadPriority => 0;
        public V2State Current { get; private set; }

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
            return new StaticMigrationSource();
        }
    }

    private sealed class StaticMigrationSource : ISaveMigrationSource
    {
        public IReadOnlyList<SaveMigrationStep> Migrations { get; } =
            new[]
            {
                SaveMigrationStep.AddIntDefault(fromVersion: 1, key: "Level", value: 12)
            };
    }
}
