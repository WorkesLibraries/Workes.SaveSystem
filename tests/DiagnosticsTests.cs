using System;
using System.Collections.Generic;
using System.IO;
using Workes.SaveSystem;

namespace Workes.SaveSystem.Tests;

public sealed class DiagnosticsTests
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
    public void Diagnostics_AreSilentByDefault()
    {
        var originalError = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);

        try
        {
            var manager = CreateManager();
            var provider = new TestProvider(schemaVersion: 1, new TestState { Value = 1 });
            manager.RegisterProvider(provider);
            manager.ValidateRegistrations();

            var loaded = manager.LoadBackupSlotFromDisk("slot", slotNumber: 1);

            Assert.That(loaded, Is.False);
            Assert.That(writer.ToString(), Is.Empty);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Test]
    public void WarningSink_ReceivesDisabledBackupLoadWarning()
    {
        var warnings = new List<string>();
        var manager = CreateManager(warningSink: warnings.Add);
        var provider = new TestProvider(schemaVersion: 1, new TestState { Value = 1 });
        manager.RegisterProvider(provider);
        manager.ValidateRegistrations();

        var loaded = manager.LoadBackupSlotFromDisk("slot", slotNumber: 1);

        Assert.That(loaded, Is.False);
        Assert.That(warnings, Has.Count.EqualTo(1));
        Assert.That(warnings[0], Does.Contain("Backup system is disabled"));
    }

    [Test]
    public void WarningSink_ReceivesMigrationWarnings()
    {
        var oldManager = CreateManager();
        var oldProvider = new TestProvider(schemaVersion: 1, new TestState { Value = 1 });
        oldManager.RegisterProvider(oldProvider);
        oldManager.ValidateRegistrations();
        oldManager.SaveToDisk("slot");
        var warnings = new List<string>();
        var newManager = CreateManager(warningSink: warnings.Add);
        var newProvider = new MigratingProvider(schemaVersion: 2, new TestState { Value = 0 });
        newManager.RegisterProvider(newProvider);
        newManager.ValidateRegistrations();

        var ex = Assert.Throws<InvalidOperationException>(() => newManager.LoadFromDisk("slot"));

        Assert.That(ex!.Message, Does.Contain("Failed to migrate save data"));
        Assert.That(warnings, Has.Count.EqualTo(1));
        Assert.That(warnings[0], Does.Contain("no migration steps available"));
    }

    private SaveManager<string> CreateManager(Action<string>? warningSink = null)
    {
        return new SaveManager<string>(
            SaveSystemOptions.Create(
                saveRootPath: _tempRoot,
                serializer: new JsonSaveSerializer(),
                warningSink: warningSink));
    }

    public sealed class TestState
    {
        public int Value { get; set; }
    }

    private class TestProvider : ISaveProvider<TestState>
    {
        public TestProvider(int schemaVersion, TestState current)
        {
            SchemaVersion = schemaVersion;
            Current = current;
        }

        public string SaveKey => "player";
        public int SchemaVersion { get; }
        public int LoadPriority => 0;
        public TestState Current { get; private set; }

        public TestState CaptureState()
        {
            return Current;
        }

        public void RestoreState(TestState state)
        {
            Current = state;
        }
    }

    private sealed class MigratingProvider : TestProvider, ISaveMigratable
    {
        public MigratingProvider(int schemaVersion, TestState current)
            : base(schemaVersion, current)
        {
        }

        public ISaveMigrationSource CreateMigrationSource()
        {
            return new MigrationSource();
        }
    }

    private sealed class MigrationSource : ISaveMigrationSource
    {
        public IReadOnlyList<SaveMigrationStep> Migrations { get; } = Array.Empty<SaveMigrationStep>();
    }
}
