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

    private SaveSystemOptions<string> CreateOptions()
    {
        return new SaveSystemOptions<string>(
            saveRootPath: _tempRoot,
            serializer: new JsonSaveSerializer(),
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
