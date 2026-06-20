# Workes.SaveSystem

`Workes.SaveSystem` is an engine-neutral .NET save system for registering state providers, capturing save snapshots, writing them to disk, loading them back, rotating backups, and migrating saved data between schema versions.

The package is not tied to Unity, Godot, or any game engine. Engine projects choose the save root path and pass it to the save manager.

## Installation

NuGet publishing is planned after package metadata, package readme wiring, and licensing are finalized. Until then, reference the project directly:

```xml
<ItemGroup>
  <ProjectReference Include="..\Workes.SaveSystem\src\Workes.SaveSystem.csproj" />
</ItemGroup>
```

The main project is `src/Workes.SaveSystem.csproj`.

## Dependency Notes

`Workes.SaveSystem` currently has a direct dependency on `Newtonsoft.Json` 13.0.3.

That dependency is intentional for the current package shape:

- `JsonSaveSerializer` is the built-in serializer.
- JSON schematics wrap provider state in a versioned payload.
- migration data nodes are backed by Newtonsoft `JToken`/`JObject` values.
- save metadata is currently read and written with Newtonsoft.

`System.Text.Json` support may be added later as a separate serializer implementation, but it is not a drop-in replacement for the current migration node model. For now, consumers should treat Newtonsoft as part of the package contract.

## Quick Start

Create a save manager, register providers, then save and load a slot.

```csharp
using Workes.SaveSystem;

var serializer = new JsonSaveSerializer();
var manager = SaveManager<string>.CreateDefault(
    serializer,
    saveRootPath: "Saves");

var playerProvider = new PlayerSaveProvider();
manager.RegisterProvider(playerProvider);
manager.ValidateRegistrations();

manager.SaveToDisk("slot-1");

playerProvider.Current = new PlayerState();
manager.LoadFromDisk("slot-1");
```

```csharp
public sealed class PlayerState
{
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
}

public sealed class PlayerSaveProvider : ISaveProvider<PlayerState>
{
    public string SaveKey => "player";
    public int SchemaVersion => 1;
    public int LoadPriority => 0;

    public PlayerState Current { get; set; } = new PlayerState
    {
        Name = "Rook",
        Level = 5
    };

    public PlayerState CaptureState()
    {
        return Current;
    }

    public void RestoreState(PlayerState state)
    {
        Current = state;
    }
}
```

## Normal Usage

`SaveManager<TIdentity>` is the main coordinator. It owns provider registration, snapshot creation, disk persistence, loading, recovery, backups, and migration coordination.

Use `string` identities for simple slot names unless the application needs a custom identity type.

```csharp
var manager = new SaveManager<string>(
    SaveSystemOptions.Create(
        saveRootPath: "Saves",
        serializer: new JsonSaveSerializer()));
```

`CreateDefault(ISaveSerializer)` is a convenience factory for plain .NET applications and writes under the current user's application data folder. Engine integrations should prefer `CreateDefault(ISaveSerializer, string)`, `SaveSystemOptions.Create(...)`, or the options constructor so the engine owns the persistent data path.

After registering providers, call `ValidateRegistrations()` before disk save/load operations. Registration is intentionally lightweight; validation captures provider state, checks serializer compatibility, validates migration policy, and verifies file-name behavior at the setup point you choose.

Use `ListSaveSlots()` to populate save/load menus or tooling with the saves currently present under the configured save root.

```csharp
IReadOnlyList<string> slots = manager.ListSaveSlots();
```

The returned values are resolved save folder names, not `TIdentity` values, because custom identity resolvers may not be reversible. The list is sorted with ordinal string ordering and ignores backup folders, temp folders, to-delete folders, and directories that do not contain save metadata.

Use `DeleteSave(...)` and `DeleteBackupSlot(...)` for save-menu cleanup or debug tooling.

```csharp
bool saveDeleted = manager.DeleteSave("slot-1");
bool backupDeleted = manager.DeleteBackupSlot("slot-1", slotNumber: 1);
```

`DeleteSave(...)` removes the main save and any temp or to-delete artifacts for the same resolved save name. It does not remove backups. `DeleteBackupSlot(...)` removes only the numbered backup folder and can be used even when backup creation is currently disabled.

Use `SaveExists(...)` and `BackupSlotExists(...)` for lightweight UI checks such as enabling load buttons or showing overwrite prompts.

```csharp
bool canLoad = manager.SaveExists("slot-1");
bool canRestoreBackup = manager.BackupSlotExists("slot-1", slotNumber: 1);
```

Existence checks inspect the raw disk layout without loading provider data, recovering temp folders, or requiring registration validation. A save or backup exists only when its folder contains save metadata.

Use `ReadSaveMetadata(...)` and `ReadBackupSlotMetadata(...)` when a menu or tool needs save-system-owned metadata.

```csharp
SaveMetadataInfo? metadata = manager.ReadSaveMetadata("slot-1");
if (metadata != null)
{
    DateTimeOffset lastWritten = metadata.LastWrittenAtUtc;
}
```

Metadata reads return `null` when no metadata file exists and throw when a metadata file is present but invalid. The current metadata contract exposes the stable save id used for recovery validation, plus created and last-written UTC timestamps. Application-owned display metadata such as character name, playtime, difficulty, or screenshot references should live in a provider for now.

## Providers

Each `ISaveProvider` owns one stable save key and one schema version.

Save keys are persistent identity. Changing a provider key changes the filename and breaks loading of existing provider data unless the application handles that compatibility.

Provider state must be compatible with the serializer and with the provider's `ISaveProvider<TState>` state type.
The built-in JSON serializer does not require a public parameterless constructor during registration; constructor-based DTOs are supported when Newtonsoft.Json can serialize and deserialize the real captured state.

```csharp
manager.RegisterProvider(playerProvider);
manager.ValidateRegistrations();
```

Providers can optionally implement `ISaveLifecycle` to receive `OnBeforeSave()` before capture and `OnAfterLoad()` after a successful restore. Providers can also be registered without a schematic through `RegisterMemoryProvider(provider)` when they should participate in snapshots but not write their state to disk.

Providers can be removed with `UnregisterProvider(provider)` when you still own the registered instance, or with `UnregisterProvider("player")` when removal by key is intentional. The instance overload only removes the provider if it is the same object that was registered; another provider instance with the same key will not remove it. If a provider is removed, call `ValidateRegistrations()` again before the next disk save or load.

When loading from disk, registered persisted providers are strict by default: if a provider is registered and its save file is missing from the save folder or backup folder, load throws and providers are not restored. Unknown extra files are ignored. For deliberate partial-load scenarios, configure `missingProviderFileBehavior: MissingProviderFileBehavior.Skip`; missing providers are skipped and keep their current runtime state.

Diagnostics are silent by default. To receive warning messages for recoverable issues such as disabled backup loads, backup normalization conflicts, or migration paths that cannot be applied, pass a warning sink through options:

```csharp
var options = SaveSystemOptions.Create(
    saveRootPath: "Saves",
    serializer: new JsonSaveSerializer(),
    warningSink: message => logger.Warn(message));
```

`RestoreSnapshot(...)` validates a snapshot before mutating providers. Validation rejects duplicate provider keys, unknown provider keys, schema mismatches, and persisted-provider state that does not match the registered schematic. Registered providers that are absent from the snapshot are skipped. If validation passes but a provider throws from `RestoreState(...)`, earlier providers may already have been restored.

## Backups

Backups are configured through `SaveSystemOptions<TIdentity>`.

```csharp
var options = SaveSystemOptions.CreateWithBackups(
    saveRootPath: "Saves",
    serializer: new JsonSaveSerializer(),
    backupSystemMaxBackupCount: 3);

var manager = new SaveManager<string>(options);
manager.RegisterProvider(playerProvider);
manager.ValidateRegistrations();
```

Backup slot `1` is the most recent previous save. Older backups rotate to higher slot numbers.

```csharp
manager.LoadBackupSlotFromDisk("slot-1", slotNumber: 1);
```

## Migration

Providers that need to load older schema versions implement `ISaveMigratable`.

Each `SaveMigrationStep` migrates from version `x` to version `x + 1`. To migrate from version 1 to version 3, provide a step from 1 to 2 and another from 2 to 3.

```csharp
public sealed class PlayerSaveProvider : ISaveProvider<PlayerStateV2>, ISaveMigratable
{
    public string SaveKey => "player";
    public int SchemaVersion => 2;
    public int LoadPriority => 0;

    public PlayerStateV2 CaptureState() => Current;
    public void RestoreState(PlayerStateV2 state) => Current = state;

    public PlayerStateV2 Current { get; set; } = new PlayerStateV2();

    public ISaveMigrationSource CreateMigrationSource()
    {
        return new PlayerMigrationSource();
    }
}

public sealed class PlayerMigrationSource : ISaveMigrationSource
{
    public IReadOnlyList<SaveMigrationStep> Migrations { get; } =
        new[]
        {
            SaveMigrationStep.AddIntDefault(1, "Level", 1)
        };
}
```

When one schema-version step needs several simple edits, compose them into one step:

```csharp
public IReadOnlyList<SaveMigrationStep> Migrations { get; } =
    new[]
    {
        SaveMigrationStep.From(
            1,
            SaveMigrationStep.Rename("XP", "Experience"),
            SaveMigrationStep.AddIntDefault("Level", 1),
            SaveMigrationStep.Remove("LegacyName"))
    };
```

For advanced changes, use the full data-node action:

```csharp
new SaveMigrationStep(2, (data, factory) =>
{
    var inventory = data.Get("Inventory");
    inventory.Add(factory.CreateString("starter-sword"));
});
```

Downgrades are not supported. A save written with a newer schema version than the current provider expects fails to load.

## Extending The System

Most application code should use `SaveManager<TIdentity>`, `ISaveProvider<TState>`, `JsonSaveSerializer`, and plain state DTOs. The interfaces below are extension contracts for projects that need custom persistence formats, migration behavior, or serializer-backed data nodes.

### Provider Contracts

Implement `ISaveProvider<TState>` for each subsystem that owns saveable state.

| Member | Contract |
|---|---|
| `SaveKey` | Stable provider identity. It must be unique within a manager and should not change after saves exist. |
| `SchemaVersion` | Stable integer version for the provider state shape. Increase it when older payloads need migration. |
| `LoadPriority` | Lower values restore first. Use it when one provider must exist before another restores. |
| `CaptureState()` | Return a serializer-compatible state object of type `TState`. The manager calls lifecycle `OnBeforeSave()` before capture. |
| `RestoreState(TState)` | Accept the object shape produced by the registered schematic. |

Use `RegisterProvider(provider)` for disk persistence. The provider's `ISaveProvider<TState>` implementation supplies the state type.

Use `RegisterMemoryProvider(provider)` only for memory-only providers that should participate in snapshots but not be written to disk.

Implement `ISaveLifecycle` when a provider needs deterministic setup around save/load:

- `OnBeforeSave()` runs before each provider capture, including manual `CaptureSnapshot()`.
- `OnAfterLoad()` runs after all snapshot entries have been restored.
- `OnAfterLoad()` is not called when `LoadFromDisk(...)` returns false for a missing save.

### Migration Contracts

Implement `ISaveMigratable` only when the provider needs to load older schema versions.

Migration rules:

- one `SaveMigrationStep` migrates from `FromVersion` to `FromVersion + 1`;
- every version gap must have exactly one step;
- duplicate `FromVersion` steps are rejected during registration validation;
- migration steps mutate the provider payload's `Data` node, not the full envelope;
- simple helper methods such as `AddIntDefault`, `Rename`, `Move`, `Remove`, and `SetString` cover common top-level field edits;
- use `SaveMigrationStep.From(...)` to compose several helper actions into one schema-version step;
- after successful migration, the manager updates the envelope schema version before deserializing;
- downgrades are rejected.

Migration steps should be deterministic. Avoid reading live game state, random values, current time, engine services, or other providers while mutating old payloads. A migration should be able to transform the same old payload into the same new payload every time.

### Serializer Contracts

Implement `ISaveSerializer` only when the built-in JSON serializer does not fit the application's persistence format.

A custom serializer must provide these pieces as one coherent format:

- a file extension, including the leading dot;
- schematic creation for provider state types;
- serialization and deserialization through those schematics;
- schema-version extraction without fully restoring provider state.

Schematic creation should be lightweight where possible. Provider state compatibility is validated through real provider state during `ValidateRegistrations()` and through deserialization during load.

If providers using the serializer implement `ISaveMigratable`, the serializer must also implement `ISaveMigrationCapableSerializer`. That means it must parse serialized payloads into editable `ISaveDataNode` trees, serialize edited node trees back to the payload format, and create new object, array, and primitive nodes for migration steps.

The migration-capable serializer and its data-node implementation are coupled. Do not mix data nodes from different serializer implementations.

### Data Node Contracts

`ISaveDataNode` is the format-neutral edit surface used by migrations. Normal application code should not need to implement or manipulate data nodes outside migration steps.

Data-node implementations should:

- report node type consistently;
- preserve object keys and array order;
- fail clearly when callers use object operations on arrays or primitive operations on objects;
- keep mutations local to the represented serialized tree;
- support the primitive and null node types exposed by `ISaveDataNodeFactory`;
- reject attempts to combine nodes created by another serializer's data-node implementation.

For the built-in JSON serializer, data nodes wrap Newtonsoft `JToken` values.

## Unity And Godot

The package targets `netstandard2.1`, which is suitable for modern Unity and Godot C# projects that can consume compatible .NET libraries.

Unity projects should make sure Newtonsoft.Json is available through the project's package setup or assembly references. Use Unity's persistent data path when constructing the manager:

```csharp
var manager = SaveManager<string>.CreateDefault(
    new JsonSaveSerializer(),
    saveRootPath: UnityEngine.Application.persistentDataPath);
```

Godot C# projects should restore the package dependency through normal .NET/NuGet restore. Use a Godot-owned user data path for saves:

```csharp
var manager = SaveManager<string>.CreateDefault(
    new JsonSaveSerializer(),
    saveRootPath: ProjectSettings.GlobalizePath("user://saves"));
```

The core package does not reference Unity or Godot assemblies. Engine-specific setup should stay in the application or in a future adapter package.

## Compatibility Notes

There are two different versioning concerns:

- package versions describe the `Workes.SaveSystem` library API and implementation;
- provider schema versions describe an application's persisted provider payload shape.

Do not use the package version as a replacement for provider `SchemaVersion`. A package update may contain no save-format changes, and a provider DTO change may require a schema bump even when the package version stays the same.

Existing saves depend on stable provider keys, schema versions, file-name resolver behavior, serializer behavior, and state DTO shape.

Before changing any of these, decide whether the change is intentionally breaking or whether a migration path is needed:

- provider key changes affect provider filenames;
- schema version changes require migration steps for older saves;
- file-name resolver changes affect where provider files are found;
- serializer changes affect payload shape and migration node compatibility;
- state DTO changes may need schema version bumps.

### Provider Keys And File Names

`SaveKey` is persistent identity for a provider. With the default file-name resolver, a provider with key `player` is stored as `player.json`.

Changing `SaveKey` is effectively a persisted file rename. If an application must rename a provider, it should either keep reading the old key during a transition or perform an external save-folder migration before loading with the new key.

The default `FileNameResolver` uses only `SaveKey`. This is intentional. Including `SchemaVersion` in file names is not recommended because a v2 provider would look for a different filename than the v1 save wrote, preventing the migration system from seeing the old payload.

### Schema Versions

Provider schema versions should increase only when the provider payload shape changes in a way that existing saves cannot load directly.

Good reasons to increase `SchemaVersion`:

- a required state property was added;
- a property changed meaning or type;
- old payload data needs transformation before deserialization;
- multiple old fields were collapsed into one new field.

Usually safe without a schema bump:

- adding optional nullable data that deserializes with a valid default;
- changing runtime-only provider logic without changing captured state;
- refactoring code while the serialized payload shape stays the same.

When in doubt, write a load test with an old payload. If the current provider cannot load the old payload without special handling, bump the provider schema and add migration steps.

### Package Versions

Package versioning should communicate API and behavior compatibility for consumers of `Workes.SaveSystem`.

Recommended package-version expectations:

- patch version: bug fixes and documentation that do not intentionally change public behavior;
- minor version: additive APIs or compatible behavior improvements;
- major version: breaking public API changes or persistence behavior changes that may require application action.

Package version changes do not migrate saves by themselves. Save compatibility stays owned by provider `SchemaVersion`, migration steps, stable keys, and stable serializer behavior.

### Serializer Swaps

Changing serializers is a save-format change. Even if two serializers both write JSON, their envelope shape, type handling, formatting, and migration node model may differ.

Before swapping serializers for existing saves, decide on one of these strategies:

- keep the old serializer for existing saves and use the new serializer only for new save roots;
- write an external conversion tool that reads old files and writes the new format;
- create a serializer that can read the old format during a transition;
- intentionally break old saves and document the break.

The current built-in migration system migrates provider payload schema inside one serializer format. It is not a general cross-serializer conversion tool.

### Long-Lived Save Checklist

For saves expected to survive package and application updates:

- keep provider keys stable and human-readable;
- use the default file-name resolver unless there is a strong reason not to;
- keep state DTOs simple and serializer-friendly;
- bump provider schema versions deliberately;
- keep migration steps deterministic;
- add tests that load representative old payloads;
- avoid using current time, random values, engine state, or other providers during migrations;
- document intentionally breaking save-format changes in application release notes.

## Suggested Next Steps

1. Add NuGet package metadata once naming, README wording, and license are final.
2. Decide whether a future `System.Text.Json` serializer is worth maintaining alongside the Newtonsoft implementation.
3. Add engine-specific adapter packages only if repeated Unity or Godot setup code becomes noisy enough to justify them.
