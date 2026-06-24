# Workes.SaveSystem

`Workes.SaveSystem` is an engine-neutral .NET save system for registering state providers, capturing save snapshots, writing them to disk, loading them back, rotating backups, and migrating saved data between schema versions.

The package is not tied to Unity, Godot, or any game engine. Engine projects choose the save root path and pass it to the save manager.

## Installation

Install the package from NuGet:

```xml
<ItemGroup>
  <PackageReference Include="Workes.SaveSystem" Version="1.0.0" />
</ItemGroup>
```

For compact MessagePack saves, see the optional companion package repository: [WorkesLibraries/Workes.SaveSystem.MessagePack](https://github.com/WorkesLibraries/Workes.SaveSystem.MessagePack).

When working from source, reference the project directly:

```xml
<ItemGroup>
  <ProjectReference Include="..\Workes.SaveSystem\src\Workes.SaveSystem.csproj" />
</ItemGroup>
```

The main project is `src/Workes.SaveSystem.csproj`. The project already includes NuGet package metadata and README packaging for `Workes.SaveSystem`.

## Dependency Notes

`Workes.SaveSystem` currently has a direct dependency on `Newtonsoft.Json` 13.0.3.

That dependency is intentional for the current package shape:

- `JsonSaveSerializer` is the built-in serializer.
- JSON schematics wrap provider state in a versioned payload.
- JSON migration support parses and writes JSON through Newtonsoft while exposing package-owned migration data nodes.
- save metadata is serialized through the active serializer.

`System.Text.Json` is not part of the core package. Under the current `netstandard2.1` target, using it requires an additional package reference, so adding a parallel `System.Text.Json` serializer would not make the package dependency-free. Replacing Newtonsoft would still require replacing the built-in JSON serializer and metadata persistence behavior together. For now, consumers should treat Newtonsoft as part of the package contract.

GZip compression is available through the .NET platform libraries and does not require another NuGet dependency. MessagePack support is available through the optional companion package, `Workes.SaveSystem.MessagePack`, because it brings its own serializer dependency. The package shape is:

```text
Workes.SaveSystem
Workes.SaveSystem.MessagePack
```

The core `Workes.SaveSystem` package does not reference MessagePack and does not ship a MessagePack serializer. It provides contextual serializer APIs so metadata-backed companion serializers can use field maps during payload reads, writes, validation, and migration. If the companion package is not installed, use JSON or compressed JSON:

```csharp
var serializer = new CompressedSaveSerializer(
    new JsonSaveSerializer(JsonSaveFormatting.Compact));
```

MessagePack is intended for compact production saves through the companion package. JSON remains the built-in readable serializer and the recommended default.

```csharp
using Workes.SaveSystem;
using Workes.SaveSystem.MessagePack;

var serializer = new MessagePackSaveSerializer();
var compressedSerializer = new CompressedSaveSerializer(new MessagePackSaveSerializer());
```

## Quick Start

Create a save manager, register providers, then save and load a slot.

```csharp
using Workes.SaveSystem;

var serializer = new JsonSaveSerializer();
var manager = SaveManager<string>.CreateDefault( // SaveManager is generic over identity type
    serializer,
    saveRootPath: "Saves");

var playerProvider = new PlayerSaveProvider();
manager.RegisterProvider(playerProvider);
manager.ValidateRegistrations(); // Necessary for using the system after provider registration or unregistration

manager.SaveToDisk("slot-1"); // "slot-1" is simply an identifier. A string is used because var manager is SaveManager<string>

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

Use `SaveSystemOptions.Create(...)` when the manager needs custom path, file, warning, or missing-file behavior.

```csharp
var options = SaveSystemOptions.Create<ProfileSlotIdentity>(
    saveRootPath: "Saves",
    serializer: new JsonSaveSerializer(JsonSaveFormatting.Compact),
    savePathResolver: identity => Path.Combine(identity.ProfileId, identity.SlotId),
    fileNameResolver: context => context.SaveKey,
    missingProviderFileBehavior: MissingProviderFileBehavior.Throw,
    warningSink: message => logger.Warn(message));

var manager = new SaveManager<ProfileSlotIdentity>(options);
```

For simple string slots, omit `savePathResolver`; string identities resolve directly to relative save paths. Keep custom `fileNameResolver` output stable over time, and do not include `SchemaVersion` in file names unless intentionally breaking old saves. Provider schema versions belong inside the provider payload so the migration system can find older files.

Use `JsonSaveSerializer` for directly readable save files. The default constructor writes indented JSON, and `JsonSaveFormatting.Compact` writes the same `.json` payload shape without formatting whitespace.

```csharp
var manager = new SaveManager<string>(
    SaveSystemOptions.Create(
        saveRootPath: "Saves",
        serializer: new JsonSaveSerializer()));
```

```csharp
var compactManager = new SaveManager<string>(
    SaveSystemOptions.Create(
        saveRootPath: "Saves",
        serializer: new JsonSaveSerializer(JsonSaveFormatting.Compact)));
```

Save metadata uses the active serializer too. JSON saves write `metadata.json`.

Use `CompressedSaveSerializer` when you want smaller files without adding a NuGet dependency:

```csharp
var compressedManager = new SaveManager<string>(
    SaveSystemOptions.Create(
        saveRootPath: "Saves",
        serializer: new CompressedSaveSerializer(
            new JsonSaveSerializer(JsonSaveFormatting.Compact))));
```

Compressed JSON files use composed extensions such as `player.json.gz` and `metadata.json.gz`.

Payload transforms wrap any serializer with reversible byte encoding. Use this extension point for custom obfuscation or encryption.

```csharp
var transformedSerializer = new TransformedSaveSerializer(
    new JsonSaveSerializer(JsonSaveFormatting.Compact),
    new XorToyTransform());
```

```csharp
public sealed class XorToyTransform : ISavePayloadTransform
{
    public string FileExtensionSuffix => ".xor";

    public byte[] Encode(byte[] data) => Apply(data);
    public byte[] Decode(byte[] data) => Apply(data);

    private static byte[] Apply(byte[] data)
    {
        var copy = new byte[data.Length];
        for (var i = 0; i < data.Length; i++)
            copy[i] = (byte)(data[i] ^ 0x5A);
        return copy;
    }
}
```

The test suite includes `SerializerOutputExampleTests`, which writes pretty JSON, compact JSON, and compressed compact JSON example saves to `tests/obj/SerializerOutputExamples` for inspection.

After registering providers, call `ValidateRegistrations()` before disk save/load operations. Registration is intentionally lightweight; validation captures provider state, checks serializer write compatibility, validates migration policy, verifies file-name behavior, and rejects provider file-name collisions at the setup point you choose. Validation is an early compatibility check, not a full future-load proof: issues that only appear while deserializing real saved data can still surface during load.

`TryLoad...` statuses are the stable result contract for load failures. Exception messages are diagnostics for humans and should not be parsed by callers.

Use `ListSaveSlots()` to populate save/load menus or tooling with the saves currently present under the configured save root.

```csharp
IReadOnlyList<string> slots = manager.ListSaveSlots();
```

The returned values are resolved relative save paths, not `TIdentity` values, because custom identity resolvers may not be reversible. The list is sorted with ordinal string ordering, uses `/` separators, and ignores backup folders, temp folders, to-delete folders, and directories that do not contain save metadata.

Use `DeleteSave(...)`, `DeleteBackupSlot(...)`, and `DeleteAllBackupSlots(...)` for save-menu cleanup or debug tooling.

```csharp
bool saveDeleted = manager.DeleteSave("slot-1");
bool backupDeleted = manager.DeleteBackupSlot("slot-1", slotNumber: 1);
int backupsDeleted = manager.DeleteAllBackupSlots("slot-1");
```

`DeleteSave(...)` removes the main save and any temp or to-delete artifacts for the same resolved save path. It does not remove backups. `DeleteBackupSlot(...)` removes only the requested numbered backup folder, while `DeleteAllBackupSlots(...)` removes all numbered backup folders for the save identity and returns the number deleted. Backup deletion can be used even when backup creation is currently disabled.

Use `SaveExists(...)` and `BackupSlotExists(...)` for lightweight UI checks such as enabling load buttons or showing overwrite prompts.

```csharp
bool canLoad = manager.SaveExists("slot-1");
bool canRestoreBackup = manager.BackupSlotExists("slot-1", slotNumber: 1);
```

Existence checks inspect the raw disk layout without loading provider data, recovering temp folders, or requiring registration validation. A save or backup exists only when its folder contains save metadata.

Use `ValidateSave(...)` or `ValidateBackupSlot(...)` when a save menu needs to check loadability without restoring providers or mutating disk. Validation requires validated registrations, reads save-system and serializer metadata, checks provider files, validates schema extraction and migration paths, and deserializes provider data in memory. It does not run recovery, call lifecycle hooks, write migrated data, or restore provider state.

```csharp
SaveValidationResult validation = manager.ValidateSave("slot-1");
if (validation.IsValid)
{
    ShowLastWritten(validation.Metadata!.LastWrittenAtUtc);
}
```

## Save Metadata

Use `ReadSaveMetadata(...)` and `ReadBackupSlotMetadata(...)` when a menu or tool needs save-system-owned metadata.

```csharp
SaveMetadataInfo? metadata = manager.ReadSaveMetadata("slot-1");
if (metadata != null)
{
    DateTimeOffset lastWritten = metadata.LastWrittenAtUtc;
}
```

Metadata reads return `null` when no metadata file exists and throw when a metadata file is present but invalid. `SaveMetadataInfo` exposes:

| Property | Meaning |
|---|---|
| `SaveId` | Stable id for this save folder. It is used internally to validate recovery candidates and should not be treated as application display data. |
| `CreatedAtUtc` | UTC timestamp for when this save identity was first written, preserved by normal saves. |
| `LastWrittenAtUtc` | UTC timestamp for the most recent successful write. |
| `HasApplicationMetadata` | Whether this save contains application-owned metadata. |
| `ApplicationMetadataSchemaVersion` | The schema version of the stored application metadata, or `null` when none exists. |

Serializers that need format metadata can implement `ISaveSerializerMetadataHandler`. That metadata is stored inside the save-system metadata file as serializer-owned string key/value data, with both global and per-provider buckets. This is intended for serializer implementation details such as field maps or codec settings; it is not exposed through `SaveMetadataInfo` and should not be used for game/application display metadata.

Serializers whose provider payload format depends on that metadata can also implement contextual extension interfaces such as `IContextualSaveSerializer` and, for migration support, `IContextualSaveMigrationCapableSerializer`. The manager passes a `SaveSerializerContext` containing the provider save key, schema version, state type, schematic, and saved serializer metadata when serializing, extracting schema versions, deserializing, validating, and migrating provider payloads. When these interfaces are present, `SaveManager` uses the contextual path for provider payloads. Direct calls to the base `ISaveSerializer` methods may not produce the same provider payload shape as manager-managed saves. Plain JSON-style serializers can ignore these optional interfaces.

Use `RegisterMetadataProvider(...)` when a save menu needs application-owned display metadata such as character name, playtime, difficulty, or screenshot references without loading provider files. Exactly one application metadata provider can be registered per manager. Application metadata is optional; when no metadata provider is registered, saves behave as before and no application metadata section is written.

```csharp
public sealed class SaveMenuMetadataProvider : ISaveMetadataProvider<SaveMenuMetadata>
{
    public int MetadataSchemaVersion => 1;

    public SaveMenuMetadata CaptureMetadata() => CurrentMenuMetadata;

    public void RestoreMetadata(SaveMenuMetadata metadata)
    {
        CurrentMenuMetadata = metadata;
    }
}

manager.RegisterMetadataProvider(new SaveMenuMetadataProvider());
manager.ValidateRegistrations();

SaveMenuMetadata? menu = manager.ReadApplicationMetadata<SaveMenuMetadata>("slot-1");
```

Application metadata has its own schema version and can implement `ISaveMetadataMigratable` to reuse the same `SaveMigrationStep` and root-node migration model as normal providers. Missing application metadata is valid for older saves and leaves the registered metadata provider untouched during load. During recovery validation, application metadata follows provider recovery rules: current-schema data is validated, but migrations are not run.

JSON stores application metadata inline in the metadata file using the serializer's normal readable data shape:

```json
"ApplicationMetadata": {
  "SchemaVersion": 1,
  "Data": {
    "CharacterName": "Scout",
    "PlaytimeSeconds": 90
  }
}
```

Saves also include a core-owned provider manifest. The manifest records which persisted provider files were written with the save, including each provider save key, schema version, and resolved file name. This lets the loader distinguish old saves written before a provider existed from current saves whose provider file was deleted or corrupted.

When a provider is present in the manifest but its file is missing, load and validation fail with `SaveLoadStatus.MissingProviderFile` even when `MissingProviderFileBehavior.Skip` is enabled. `Skip` is preserved only for legacy saves that have no provider manifest. When a registered provider is absent from a non-empty manifest, it is treated as newly added after the save was written. By default that provider is left unchanged during load. If the provider implements `ISaveDefaultStateProvider<TState>`, the manager restores `CreateDefaultStateForMissingSave()` instead.

```csharp
public sealed class QuestProvider :
    ISaveProvider<QuestState>,
    ISaveDefaultStateProvider<QuestState>
{
    public string SaveKey => "quests";
    public int SchemaVersion => 1;
    public int LoadPriority => 0;

    public QuestState CaptureState() => Current;
    public void RestoreState(QuestState state) => Current = state;

    public QuestState CreateDefaultStateForMissingSave()
    {
        return QuestState.CreateEmpty();
    }
}
```

Advanced custom serializers must support the public `SaveMetadata` payload type because save-system metadata is serialized through the active serializer. `SaveMetadata` is a property-based serializer contract with stable names for `SaveId`, `CreatedAtUtc`, `LastWrittenAtUtc`, `SerializerMetadata`, `ProviderManifest`, and `ApplicationMetadata`; manager-owned creation and timestamp update helpers are intentionally not public. Serializers that want to support registered application metadata must also implement `ISaveApplicationMetadataSerializer`, which converts typed metadata to/from serializer-native inline `ApplicationMetadata.Data` and migration nodes. Application code that only wants to read core menu metadata should use `SaveMetadataInfo` from `ReadSaveMetadata(...)`, `ReadBackupSlotMetadata(...)`, or successful `ValidateSave(...)` results. Application code that wants typed display metadata should use `ReadApplicationMetadata(...)` or `ReadBackupApplicationMetadata(...)`.

Use `ForceSaveToDisk(...)` only when intentionally repairing or replacing a save whose existing metadata or serializer format cannot be trusted. Normal `SaveToDisk(...)` preserves readable core metadata and rotates backups when enabled. `ForceSaveToDisk(...)` writes a fresh main save with a new save id and timestamps, ignores unreadable existing metadata, does not rotate the replaced main folder into backups, and leaves existing backup folders untouched.

Use `TryLoadFromDisk(...)` or `TryLoadBackupSlotFromDisk(...)` when a UI or repair tool needs a structured outcome instead of exceptions.

```csharp
SaveLoadResult result = manager.TryLoadFromDisk("slot-1");
if (!result.Succeeded)
{
    switch (result.Status)
    {
        case SaveLoadStatus.NotFound:
            break;
        case SaveLoadStatus.CorruptData:
        case SaveLoadStatus.MigrationFailed:
            logger.Warn(result.Message);
            break;
    }
}
```

The try-load APIs use the same load path as `LoadFromDisk(...)` and `LoadBackupSlotFromDisk(...)`. Successful loads restore providers normally. Missing saves, disabled backups, registration validation failures, missing provider files, migration failures, recovery failures, corrupt data, and other load failures are reported through `SaveLoadResult.Status`; failed error cases keep the captured exception on `SaveLoadResult.Exception`. Provider payloads with a valid envelope but null `Data` are valid only when the registered provider state type can accept null; null data for a non-nullable value-type provider is treated as corrupt data.

For `TryLoadBackupSlotFromDisk(...)`, disabled backups are reported as `SaveLoadStatus.BackupSystemDisabled` before request or registration validation. This makes backup-disabled UI checks cheap and non-throwing even when the caller has not prepared provider registrations.

## Scopes And Provider Sets

The core package does not have a provider-group API. Scope is modeled through save identities, save roots, and manager composition.

Use a scoped identity when the same registered providers are saved per profile, character, world, or slot.

```csharp
public readonly struct ProfileSlotIdentity
{
    public ProfileSlotIdentity(string profileId, string slotId)
    {
        ProfileId = profileId;
        SlotId = slotId;
    }

    public string ProfileId { get; }
    public string SlotId { get; }
}

var profileSaves = new SaveManager<ProfileSlotIdentity>(
    SaveSystemOptions.Create<ProfileSlotIdentity>(
        saveRootPath: "ProfileSaves",
        serializer: new JsonSaveSerializer(),
        savePathResolver: identity => Path.Combine(identity.ProfileId, identity.SlotId)));

profileSaves.SaveToDisk(new ProfileSlotIdentity("profile-a", "slot-1"));
```

Resolved save paths are safe relative paths under the manager's save root. They may contain path separators for hierarchy, but they cannot be absolute paths, contain `.` or `..` segments, use empty segments, or collide with save-system artifact folders.

Use separate managers when different provider sets have different lifecycles.

```csharp
var settingsSaves = new SaveManager<string>(
    SaveSystemOptions.Create(
        saveRootPath: "SettingsSaves",
        serializer: new JsonSaveSerializer()));
settingsSaves.RegisterProvider(settingsProvider);

var gameplaySaves = new SaveManager<ProfileSlotIdentity>(
    SaveSystemOptions.Create<ProfileSlotIdentity>(
        saveRootPath: "GameplaySaves",
        serializer: new JsonSaveSerializer(),
        savePathResolver: identity => Path.Combine(identity.ProfileId, identity.SlotId)));
gameplaySaves.RegisterProvider(playerProvider);
gameplaySaves.RegisterProvider(inventoryProvider);
```

This keeps provider ownership explicit: settings can be saved globally while gameplay providers are saved per profile slot. If a future application repeatedly needs partial-save domains inside one manager, that can be revisited as a separate provider-group feature.

## Providers

Each `ISaveProvider` owns one stable save key and one schema version.

Save keys are persistent identity. Changing a provider key changes the filename and breaks loading of existing provider data unless the application handles that compatibility.

`SchemaVersion` is the version of that provider's persisted state shape. It is stored inside the provider payload, not in the file name, so the manager can read an old payload, see its saved version, run migration steps, and only then deserialize it into the current state type.

`SaveKey` must remain stable after provider registration. `SchemaVersion` must remain stable after registration validation. The manager checks these values before disk save/load operations and throws a clear error if a provider changes its persistence contract after setup. If you intentionally change a provider schema during setup, call `ValidateRegistrations()` again before saving or loading.

Bump `SchemaVersion` when old payloads need help to become the current state shape. Good examples include adding required data, renaming a persisted property, changing a property's type or meaning, or splitting/collapsing fields. You usually do not need a bump for runtime-only code changes, adding optional nullable data that deserializes safely, or refactoring code while the serialized state shape remains compatible.

Custom `FileNameResolver` values must also resolve every persisted provider to a unique file name. The default resolver uses `SaveKey`, so uniqueness follows from unique provider keys. A custom resolver that maps multiple providers to the same file is rejected during registration validation. The provider file base name `metadata` is reserved for save-system metadata, so providers must not resolve to `metadata.json`, `metadata.json.gz`, or the active serializer's equivalent metadata file.

Provider state must be compatible with the provider's `ISaveProvider<TState>` state type. Null provider state is supported for reference-type state and `Nullable<T>` state. Null is rejected for non-nullable value-type providers so JSON null cannot silently restore as a default value such as `0` or `false`. Persisted providers must also be compatible with the serializer.
The built-in JSON serializer does not require a public parameterless constructor during registration; constructor-based DTOs are supported when Newtonsoft.Json can serialize and deserialize the real captured state.

```csharp
manager.RegisterProvider(playerProvider);
manager.ValidateRegistrations();
```

Use `TryRegisterProvider(...)` when setup code wants to add one provider and immediately reject it without throwing if registration validation fails.

```csharp
if (!manager.TryRegisterProvider(playerProvider, out var registrationError))
{
    logger.Warn(registrationError);
}
```

`TryRegisterProvider(...)` and `TryRegisterMemoryProvider(...)` tentatively register the provider, run the same global `ValidateRegistrations()` path, and remove the provider again if registration or validation fails. Memory-only providers are also captured during validation so incompatible state is rejected before the first snapshot or disk operation. Successful try-registration leaves the manager validated for disk save/load operations.

Providers can optionally implement `ISaveLifecycle` to receive `OnBeforeSave()` before capture and `OnAfterLoad()` after a successful restore. Providers can also be registered without a schematic through `RegisterMemoryProvider(provider)` when they should participate in snapshots but not write their state to disk.

Providers can be removed with `UnregisterProvider(provider)` when you still own the registered instance, or with `UnregisterProvider("player")` when removal by key is intentional. The instance overload removes only the same object that was registered, using the original registration key even if the provider key has since drifted. Another provider instance with the same key will not remove it. If a provider is removed, call `ValidateRegistrations()` again before the next disk save or load.

When loading from disk, registered persisted providers are strict by default: if a provider is registered and its save file is missing from the save folder or backup folder, load throws and providers are not restored. Unknown extra files are ignored. For deliberate partial-load scenarios with legacy saves that do not have a provider manifest, configure `missingProviderFileBehavior: MissingProviderFileBehavior.Skip`; missing providers are skipped and keep their current runtime state. Manifest-backed saves remain strict for providers recorded in the manifest.

Diagnostics are silent by default. To receive warning messages for recoverable issues such as disabled backup loads, backup normalization conflicts, or migration paths that cannot be applied, pass a warning sink through options:

```csharp
var options = SaveSystemOptions.Create(
    saveRootPath: "Saves",
    serializer: new JsonSaveSerializer(),
    warningSink: message => logger.Warn(message));
```

`RestoreSnapshot(...)` validates a snapshot before mutating providers. Validation rejects duplicate provider keys, unknown provider keys, schema mismatches, and persisted-provider state that does not match the registered schematic. Registered providers that are absent from the snapshot are skipped. If validation passes but a provider throws from `RestoreState(...)`, earlier providers may already have been restored.

## Default Save-State Model

The default save-state model is the portable contract that built-in serializers and serializer companion packages are expected to support. It is also the shape that works best with migration, validation, and serializer swaps.

Supported by default:

- provider state should be a concrete, non-polymorphic type;
- public properties are the primary serialization surface;
- nested concrete POCO/object state is supported;
- common scalar values are expected to work, such as strings, bools, integers, floating-point numbers, decimals, enums, and serializer-supported values such as `DateTime`, `DateTimeOffset`, `Guid`, and byte arrays;
- collections are expected to work when they are ordinary arrays or lists of supported element types;
- dictionaries are expected to work when they use string keys and supported value types.

Example of supported nested state:

```csharp
public sealed class PlayerState
{
    public string Name { get; set; } = "";
    public WeaponState Weapon { get; set; } = new();
}

public sealed class WeaponState
{
    public int Damage { get; set; }
    public bool IsMagic { get; set; }
}
```

Out of scope for the default model:

- runtime polymorphism;
- interface-typed state members, such as `IQuestState CurrentQuest`;
- abstract or base-type members where the runtime value may be a derived type;
- `object`-typed members intended to hold arbitrary runtime values;
- type-name metadata or automatic concrete-type restoration;
- private fields or private properties as the default serialization surface;
- arbitrary object graphs with cycles;
- serializer-specific custom converters or resolvers as part of the default contract.

These are general serialization and migration constraints, not limitations of one specific serializer. A custom serializer, converter, or resolver may support broader shapes, but those shapes are outside the default save-system contract and should be documented and tested by the application or serializer package that enables them. Companion serializers, such as MessagePack, should aim to match this default model unless their README documents a specific difference.

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

`CreateWithBackups(...)` accepts the same resolver, warning, and missing-provider-file options as `Create(...)`, plus `backupSystemMaxBackupCount`.

Backup slot `1` is the most recent previous save. Older backups rotate to higher slot numbers.

```csharp
manager.LoadBackupSlotFromDisk("slot-1", slotNumber: 1);
```

## Recovery

`LoadFromDisk(...)` automatically calls `RecoverSave(...)` before loading provider data. Recovery handles interrupted atomic save swaps involving the main save folder, the temp folder, and the to-delete folder for the same resolved save path.

Direct calls to `RecoverSave(...)` require the same successful `ValidateRegistrations()` setup as disk load operations.

Recovery validates candidate save folders without running provider migrations. It checks metadata, required provider files, schema-version extraction, current schema-version compatibility, and current-schema deserialization. A temp or `_toDelete` recovery candidate written with an older or newer provider schema is rejected rather than migrated during recovery; migrations still run during normal loads after a current-schema save folder has been recovered.

Recovery candidate validation is stricter than deliberate partial-load behavior. Even when `missingProviderFileBehavior: MissingProviderFileBehavior.Skip` is configured for normal loads, recovery will not promote a temp or `_toDelete` candidate that is missing a registered persisted provider file.

If both a temp folder and a previous `_toDelete` folder exist while the main save is missing, the valid temp save is preferred. If the temp save is invalid but the previous save is valid, recovery falls back to the previous save and emits a warning through the configured warning sink. If neither candidate is valid, recovery fails and preserves the recovery artifacts for inspection or manual repair.

## Migration

Providers that need to load older schema versions implement `ISaveMigratable`. Migration is provider-local: each provider owns its own `SchemaVersion`, migration source, and migration steps.

Provider files may be written as a serializer-owned envelope containing `SchemaVersion` and provider data. Migration reads the saved schema version from the serialized payload, converts the provider data root into editable save data nodes, mutates that root node, and then asks the serializer to write the current schema version back into its payload format. Migration steps never edit serializer envelopes directly.

```json
{
  "SchemaVersion": 1,
  "Data": {
    "Name": "Rook"
  }
}
```

Migration steps edit the serialized provider data root before the serializer deserializes it into the current state type. For an object-root provider, that root is an object node. For a list-root provider, it is an array node. For a string-key dictionary root, it is an object/map node whose keys are data keys. For a primitive root, it is a primitive node. That means application code should normally keep only the current DTO shape in runtime code. Old shapes can live in tests or documentation fixtures, but they do not need to remain as production state classes just so migration can work.

For example, imagine version 1 of `PlayerState` only had `Name`. Version 2 adds required `Level` data. The current code can simply use the current shape:

```csharp
public sealed class PlayerState
{
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
}
```

The provider reports the current schema version and supplies a migration that adds `Level` to older serialized payloads:

```csharp
public sealed class PlayerSaveProvider : ISaveProvider<PlayerState>, ISaveMigratable
{
    public string SaveKey => "player";
    public int SchemaVersion => 2;
    public int LoadPriority => 0;

    public PlayerState CaptureState() => Current;
    public void RestoreState(PlayerState state) => Current = state;

    public PlayerState Current { get; set; } = new PlayerState();

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

`SaveMigrationStep.AddIntDefault(1, "Level", 1)` means: when loading a version 1 payload, add a top-level `Level` value before deserializing it as the current `PlayerState`.

### Migration Flow

The load path applies migrations only when the saved provider schema version differs from the registered provider schema version.

1. The manager extracts `SchemaVersion` from the provider file.
2. If the provider implements `ISaveMigratable`, the manager asks it for an `ISaveMigrationSource`.
3. The migration engine requires one step for every version gap, such as `1 -> 2` and `2 -> 3`.
4. The active serializer's `Migration` adapter parses bytes into an editable `ISaveDataNode` provider root.
5. Each migration step mutates that provider root node, not the full serializer envelope.
6. The serializer converts the edited root node back to bytes using the current provider schema version.
7. The serializer deserializes those bytes into the current provider state type.

Downgrades are not supported. A save written with a newer schema version than the current provider expects fails to load.

### Migration API

When one schema-version step needs several simple edits, compose them into one step. This is still one migration from version 1 to version 2:

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

The migration API is intentionally split into provider-facing contracts, helper steps, and direct data-node editing.

| API | Purpose |
|---|---|
| `ISaveMigratable.CreateMigrationSource()` | Implement on a provider that can load older schema versions. |
| `ISaveMigrationSource.Migrations` | Returns the ordered or unordered list of available `SaveMigrationStep` entries. |
| `SaveMigrationStep.FromVersion` | The schema version this step migrates from. Each step always migrates to `FromVersion + 1`. |
| `SaveMigrationStep.Migrate` | The action that mutates the provider data root node. |
| `new SaveMigrationStep(fromVersion, action)` | Creates a custom migration step with full data-node access. |
| `SaveMigrationStep.From(fromVersion, actions...)` | Combines several migration actions into one version step. |

Helper methods come in two forms. Methods with a `fromVersion` return a complete `SaveMigrationStep`. Methods without `fromVersion` return a reusable action for `SaveMigrationStep.From(...)`.

| Helper family | Complete step example | Action example | Behavior |
|---|---|---|---|
| `AddDefault` | `AddIntDefault(1, "Level", 1)` | `AddIntDefault("Level", 1)` | Adds a field only when the field is missing. |
| `Set` | `SetString(1, "Name", "Rook")` | `SetString("Name", "Rook")` | Writes or replaces a field. |
| `Remove` | `Remove(1, "LegacyName")` | `Remove("LegacyName")` | Removes a field if it exists. |
| `Rename` | `Rename(1, "XP", "Experience")` | `Rename("XP", "Experience")` | Moves a field to a new key and fails if the target exists unless `overwrite: true`. |
| `Move` | `Move(1, "OldName", "NewName")` | `Move("OldName", "NewName")` | Alias for `Rename(...)`, useful when the intent is moving data. |

Primitive helper variants are available for common values:

```csharp
SaveMigrationStep.AddIntDefault(1, "Level", 1);
SaveMigrationStep.AddLongDefault(1, "TotalGold", 9000000000L);
SaveMigrationStep.AddFloatDefault(1, "Speed", 4.5f);
SaveMigrationStep.AddDoubleDefault(1, "Precision", 4.56789d);
SaveMigrationStep.AddDecimalDefault(1, "Cost", 123.45m);
SaveMigrationStep.AddStringDefault(1, "Title", "Unknown");
SaveMigrationStep.AddBoolDefault(1, "Unlocked", false);
SaveMigrationStep.AddBytesDefault(1, "Thumbnail", bytes);
SaveMigrationStep.AddDateTimeDefault(1, "LastSeenAt", DateTime.UtcNow);
SaveMigrationStep.AddNullDefault(1, "DeletedAt");

SaveMigrationStep.SetInt(1, "Level", 1);
SaveMigrationStep.SetLong(1, "TotalGold", 9000000000L);
SaveMigrationStep.SetFloat(1, "Speed", 4.5f);
SaveMigrationStep.SetDouble(1, "Precision", 4.56789d);
SaveMigrationStep.SetDecimal(1, "Cost", 123.45m);
SaveMigrationStep.SetString(1, "Title", "Unknown");
SaveMigrationStep.SetBool(1, "Unlocked", false);
SaveMigrationStep.SetBytes(1, "Thumbnail", bytes);
SaveMigrationStep.SetDateTime(1, "LastSeenAt", DateTime.UtcNow);
SaveMigrationStep.SetNull(1, "DeletedAt");
```

Use helper steps for simple top-level object edits. Use direct data-node access when a migration needs nested objects, arrays, root collections, conditionals, value conversion, or multi-field logic.

```csharp
new SaveMigrationStep(2, (data, factory) =>
{
    var inventory = data.Get("Inventory");
    inventory.Add(factory.CreateString("starter-sword"));
});
```

The `data` parameter is an `ISaveDataNode` for the provider data root. The factory parameter creates new nodes that belong to the same serializer-owned node tree. Do not create nodes with another serializer or factory instance and insert them here.

```csharp
new SaveMigrationStep(2, (data, factory) =>
{
    if (!data.Has("Inventory"))
        data.Set("Inventory", factory.CreateArray());

    data.Get("Inventory").Add(factory.CreateString("starter-sword"));
});
```

Root collection and primitive migrations use the same node model:

```csharp
new SaveMigrationStep(1, (root, factory) =>
{
    for (var i = 0; i < root.Count; i++)
    {
        var item = root.GetAt(i);
        if (!item.Has("Count"))
            item.Set("Count", factory.CreateInt(1));
    }
});

new SaveMigrationStep(2, (root, factory) =>
{
    root.ReplaceWith(factory.CreateString("level-" + root.AsInt()));
});
```

### Save Data Nodes

`ISaveDataNode` is a package-owned, format-neutral edit tree for migration. It is not a normal provider state DTO, and it is not Newtonsoft.Json's `JToken` exposed through the public API. The built-in JSON serializer converts JSON into package-owned data nodes before migration, then converts the edited nodes back into JSON afterward.

Supported node types are `Object`, `Array`, `Int`, `Long`, `Float`, `Double`, `Decimal`, `String`, `Bool`, `Bytes`, `DateTime`, and `Null`.

| Node API | Applies to | Purpose |
|---|---|---|
| `NodeType` | all nodes | Gets the current `SaveDataNodeType`. |
| `IsObject()`, `IsArray()`, `IsNull()` | all nodes | Checks the current node shape. |
| `Count` | object/array nodes | Gets the child count. |
| `Keys` | object nodes | Enumerates object keys. |
| `Has(key)`, `Get(key)`, `Set(key, value)`, `Remove(key)` | object nodes | Reads and mutates object fields. |
| `GetAt(index)`, `SetAt(index, value)`, `InsertAt(index, value)`, `RemoveAt(index)`, `Add(value)` | array nodes | Reads and mutates array entries. |
| `AsInt()`, `AsLong()`, `AsFloat()`, `AsDouble()`, `AsDecimal()` | numeric nodes | Reads numeric values. |
| `AsString()`, `AsBool()`, `AsBytes()`, `AsDateTime()` | primitive nodes | Reads primitive values. |
| `SetInt(value)`, `SetLong(value)`, `SetFloat(value)`, `SetDouble(value)`, `SetDecimal(value)` | existing nodes | Replaces the current node with a numeric value. |
| `SetString(value)`, `SetBool(value)`, `SetBytes(value)`, `SetDateTime(value)`, `SetNull()` | existing nodes | Replaces the current node value. |
| `ReplaceWith(value)` | existing nodes | Replaces the current node with another node shape/value created by the same factory. |

Use `ISaveDataNodeFactory` to create new nodes:

```csharp
factory.CreateObject();
factory.CreateArray();
factory.CreateInt(1);
factory.CreateLong(9000000000L);
factory.CreateFloat(4.5f);
factory.CreateDouble(4.56789d);
factory.CreateDecimal(123.45m);
factory.CreateString("Rook");
factory.CreateBool(true);
factory.CreateBytes(bytes);
factory.CreateDateTime(DateTime.UtcNow);
factory.CreateNull();
```

For the built-in JSON serializer, `Long` writes a JSON integer and `Double` writes a JSON number. `Decimal` writes an invariant-culture string, `Bytes` writes a Base64 string, and `DateTime` writes an `"O"` round-trip string. When reading migration nodes from JSON, date-looking strings remain strings until migration code asks for `AsDateTime()`, and Base64 strings remain strings until migration code asks for `AsBytes()`.

Wrong-shape operations fail clearly. For example, calling `Get("Name")` on an array node or `Add(...)` on an integer node throws. Nodes also track factory ownership so migrations cannot accidentally mix nodes from different serializer instances.

Migration steps should be deterministic. Avoid reading live game state, random values, current time, engine services, or other providers while mutating old payloads. A migration should be able to transform the same old payload into the same new payload every time.

## Extending The System

Most application code should use `SaveManager<TIdentity>`, `ISaveProvider<TState>`, `JsonSaveSerializer`, and plain state DTOs. The interfaces below are extension contracts for projects that need custom persistence formats, migration behavior, or serializer-backed data nodes.

### Provider Contracts

Implement `ISaveProvider<TState>` for each subsystem that owns saveable state.

| Member | Contract |
|---|---|
| `SaveKey` | Stable provider identity. It must be unique within a manager and must not change after provider registration. |
| `SchemaVersion` | Stable integer version for the provider state shape. Increase it when older payloads need migration. It must not change after registration validation. |
| `LoadPriority` | Lower values restore first. Use it when one provider must exist before another restores. |
| `CaptureState()` | Return a serializer-compatible state object of type `TState`. Null is allowed for reference-type and `Nullable<T>` state. The manager calls lifecycle `OnBeforeSave()` before capture. |
| `RestoreState(TState)` | Accept the object shape produced by the registered schematic. |

Use `RegisterProvider(provider)` for disk persistence. The provider's `ISaveProvider<TState>` implementation supplies the state type.

Use `RegisterMemoryProvider(provider)` only for memory-only providers that should participate in snapshots but not be written to disk.

Implement `ISaveLifecycle` when a provider needs deterministic setup around save/load:

- `OnBeforeSave()` runs before each provider capture, including manual `CaptureSnapshot()`.
- `OnAfterLoad()` runs after all snapshot entries have been restored.
- `OnAfterLoad()` is not called when `LoadFromDisk(...)` returns false for a missing save.

### Migration Contracts

Implement `ISaveMigratable` only when the provider needs to load older schema versions. See the `Migration` section for the full provider-facing workflow and helper API.

Migration rules:

- one `SaveMigrationStep` migrates from `FromVersion` to `FromVersion + 1`;
- every version gap must have exactly one step;
- duplicate `FromVersion` steps and null migration entries are rejected during registration validation;
- migration steps mutate the provider data root node, not the full serializer envelope;
- use `SaveMigrationStep.From(...)` to compose several helper actions into one schema-version step;
- after successful migration, the serializer writes the current schema version before deserializing;
- downgrades are rejected.

Missing migration gaps are detected during load and are reported by `TryLoad...` as `SaveLoadStatus.MigrationFailed`.

### Serializer Contracts

Implement `ISaveSerializer` only when the built-in JSON serializer does not fit the application's persistence format.

Serializer methods are primarily extension points for the save-system pipeline. Application code should usually interact with `SaveManager` instead of calling serializers directly. Use manager APIs such as `SaveToDisk(...)`, `LoadFromDisk(...)`, `TryLoadFromDisk(...)`, and `ValidateSave(...)` for normal save/load flows. `ISaveSerializer.Serialize(...)`, `Deserialize(...)`, and `CreateSchematic(...)` are intended for `SaveManager`, serializer wrappers, and advanced integration code.

Choose the smallest serializer surface that fits the format:

| Need | Implement |
|---|---|
| Basic provider save/load for a self-describing format | `ISaveSerializer` and `ISaveSchematic` |
| Type-safe schematic implementation | Derive from `SaveSchematic<T>` |
| Provider payloads need save key, state type, schema version, or serializer metadata | `IContextualSaveSerializer` |
| Serializer-owned save metadata such as field maps or codec settings | `ISaveSerializerMetadataHandler` |
| Provider migrations | `ISaveMigrationCapableSerializer` from `ISaveSerializer.Migration` |
| Context-dependent provider migrations | `IContextualSaveMigrationCapableSerializer` |
| Typed application metadata providers | `ISaveApplicationMetadataSerializer` |
| Encoding, encryption, compression, or another byte wrapper around an existing serializer | `ISavePayloadTransform` with `TransformedSaveSerializer` |

A custom serializer must provide these pieces as one coherent format:

- a file extension, including the leading dot;
- schematic creation for provider state types and for the public `SaveMetadata` type;
- byte-based serialization and deserialization through those schematics;
- a provider payload shape with an extractable schema version;
- clear failures when payload bytes, schema versions, metadata, or state shapes are invalid.

The manager sets `ISaveSchematic.SchemaVersion` from the registered provider before writing provider files. Do not require applications to set schema versions on schematics manually. Schematic creation should be lightweight where possible. Provider state write compatibility is validated through real provider state during `ValidateRegistrations()`. Read compatibility is validated when real save data is deserialized during load, so custom serializers should still fail clearly for deserialize-only problems.

The save metadata file is serialized through the active serializer. Custom serializers must support `SaveMetadata` as normal serializer-facing data:

- `SaveId`, `CreatedAtUtc`, and `LastWrittenAtUtc` are core save metadata.
- `SerializerMetadata` is serializer-owned string key/value metadata.
- `ProviderManifest` is core-owned compatibility metadata and should be serialized normally.
- `ApplicationMetadata` is present only when an application metadata provider is registered.
- `SaveApplicationMetadata.Data` is serializer-native inline data when `ISaveApplicationMetadataSerializer` is implemented.

Existing metadata files that deserialize to `null` or to another type are treated as corrupt; use `ForceSaveToDisk(...)` when intentionally replacing a corrupt or incompatible save.

The manager call flow is:

1. `ValidateRegistrations()` creates provider schematics, sets schema versions, validates provider state serialization, writes transient serializer metadata, and validates migration policy.
2. `SaveToDisk()` captures providers, writes serializer metadata, serializes application metadata when present, serializes provider files, writes `ProviderManifest`, and writes the save metadata file.
3. `LoadFromDisk()` reads save metadata, validates serializer metadata, reads manifest-listed provider files, extracts saved provider schema versions, migrates when needed, deserializes provider states, validates snapshot compatibility, and restores providers.
4. `ValidateSave()` follows the loadability path without restoring providers or mutating disk.

Direct calls to base serializer methods are low-level integration or diagnostic operations. When a serializer implements contextual interfaces, manager-managed provider payloads may contain metadata-backed shapes that direct `ISaveSerializer` calls do not reproduce.

#### Minimal Serializer Skeleton

This skeleton shows the shape of a simple self-describing serializer. It is intentionally small; production serializers should add robust parsing, type handling, and error messages.

```csharp
public sealed class SimpleTextSaveSerializer : ISaveSerializer
{
    public string FileExtension => ".txtsave";

    public ISaveMigrationCapableSerializer? Migration => null;
    public ISaveSerializerMetadataHandler? Metadata => null;

    public ISaveSchematic CreateSchematic(Type stateType)
    {
        var schematicType = typeof(SimpleTextSaveSchematic<>).MakeGenericType(stateType);
        return (ISaveSchematic)Activator.CreateInstance(schematicType)!;
    }

    public byte[] Serialize(object data, ISaveSchematic schematic)
    {
        return schematic.SerializeUntyped(data);
    }

    public object? Deserialize(byte[] rawData, ISaveSchematic schematic)
    {
        return schematic.DeserializeUntyped(rawData);
    }

    public int ExtractSchemaVersion(byte[] serializedData)
    {
        // Read the version from the payload envelope without fully restoring Data.
        return SimpleTextEnvelope.ReadSchemaVersion(serializedData);
    }
}

public sealed class SimpleTextSaveSchematic<T> : SaveSchematic<T>
{
    public SimpleTextSaveSchematic() : base(schemaVersion: 1)
    {
    }

    public override byte[] Serialize(T? state)
    {
        return SimpleTextEnvelope.Write(new SimpleTextEnvelope<T>
        {
            SchemaVersion = SchemaVersion,
            Data = state
        });
    }

    public override T? Deserialize(byte[] serialized)
    {
        var envelope = SimpleTextEnvelope.Read<T>(serialized);
        if (envelope.SchemaVersion != SchemaVersion)
            throw new InvalidOperationException("Schema version mismatch.");

        return envelope.Data;
    }
}
```

The important part is the envelope contract: provider payload bytes must contain the provider schema version and the provider data root. The root may be an object, array/list, string-key map, primitive, or null when the registered state type can accept null.

#### Contextual Serialization

Implement `IContextualSaveSerializer` when provider payload serialization depends on save metadata, provider key, state type, or another manager-owned value.

```csharp
public byte[] Serialize(object data, SaveSerializerContext context)
{
    var providerMetadata = context.SerializerMetadata.GetOrCreateProvider(context.SaveKey);
    var fieldMap = providerMetadata["field-map"];

    return WriteProviderPayload(
        data,
        context.StateType,
        context.SchemaVersion,
        context.Schematic,
        fieldMap);
}

public object? Deserialize(byte[] rawData, SaveSerializerContext context)
{
    var providerMetadata = context.SerializerMetadata.Providers[context.SaveKey];
    return ReadProviderPayload(rawData, context.StateType, context.Schematic, providerMetadata);
}
```

When `IContextualSaveSerializer` is implemented, `SaveManager` prefers it for provider serialization, deserialization, and schema-version extraction. Plain JSON-style serializers that are self-describing usually do not need it.

#### Serializer Metadata

Implement `ISaveSerializerMetadataHandler` when the serializer needs metadata stored once per save, such as field maps or codec settings.

```csharp
public sealed class FieldMapSerializer :
    ISaveSerializer,
    ISaveSerializerMetadataHandler
{
    public ISaveSerializerMetadataHandler Metadata => this;

    public void WriteMetadata(SaveSerializerMetadataWriteContext context)
    {
        foreach (var provider in context.Providers)
        {
            var metadata = context.Metadata.GetOrCreateProvider(provider.SaveKey);
            metadata["state-type"] = provider.StateType.AssemblyQualifiedName!;
            metadata["schema-version"] = provider.SchemaVersion.ToString();
        }
    }

    public void ValidateMetadata(SaveSerializerMetadataValidationContext context)
    {
        foreach (var provider in context.Providers)
        {
            if (!context.Metadata.Providers.ContainsKey(provider.SaveKey))
                throw new InvalidOperationException($"Missing serializer metadata for '{provider.SaveKey}'.");
        }
    }
}
```

`WriteMetadata(...)` runs before provider files and the metadata file are written. `ValidateMetadata(...)` runs when metadata is read for temp-save validation, load, validation, and recovery candidates. Missing serializer metadata is normalized as empty metadata for compatibility with older saves, so serializers that require metadata should validate it explicitly.

#### Migration-Capable Serializers

If providers using the serializer implement `ISaveMigratable`, the serializer must return an `ISaveMigrationCapableSerializer` from `ISaveSerializer.Migration`. That adapter parses serialized payload bytes into editable provider-root `ISaveDataNode` trees, then serializes the edited root back to the payload format.

```csharp
public sealed class SimpleMigrationAdapter :
    ISaveMigrationCapableSerializer,
    IContextualSaveMigrationCapableSerializer
{
    public ISaveDataNodeFactory NodeFactory { get; } = new SimpleNodeFactory();

    public ISaveDataNode DeserializeToNode(byte[] data)
    {
        var envelope = SimpleTextEnvelope.ReadUntyped(data);
        return SimpleNodeConverter.ToNode(envelope.Data, NodeFactory);
    }

    public byte[] SerializeFromNode(ISaveDataNode node)
    {
        var data = SimpleNodeConverter.FromNode(node);
        return SimpleTextEnvelope.Write(schemaVersion: 1, data);
    }

    public ISaveDataNode DeserializeToNode(byte[] data, SaveSerializerContext context)
    {
        var envelope = SimpleTextEnvelope.ReadUntyped(data);
        return SimpleNodeConverter.ToNode(envelope.Data, NodeFactory);
    }

    public byte[] SerializeFromNode(ISaveDataNode node, SaveSerializerContext context)
    {
        var data = SimpleNodeConverter.FromNode(node);
        return SimpleTextEnvelope.Write(context.SchemaVersion, data);
    }
}
```

Migration nodes represent the provider data root, not the full serializer envelope. Object-root providers receive an object node. List-root providers receive an array node. String-key dictionary providers receive an object/map node. Primitive and null roots are valid migration nodes. `ReplaceWith(...)` lets a migration replace the root shape entirely, as long as the replacement node came from the same factory.

The migration-capable serializer, its `NodeFactory`, and its data-node trees are coupled. Do not mix data nodes from different serializer or factory instances.

#### Application Metadata Support

Application metadata is optional. Normal provider save/load works without `ISaveApplicationMetadataSerializer`. Implement it only when the serializer should support `RegisterMetadataProvider(...)`.

The manager passes a `SaveSerializerContext` with the reserved synthetic save key `__workes_application_metadata`. Metadata-backed serializers can treat this like a provider-like payload for field-map or codec metadata.

```csharp
public object? SerializeApplicationMetadata(object? metadata, SaveSerializerContext context)
{
    return ToInlineSerializerData(metadata, context.StateType, context.SerializerMetadata);
}

public object? DeserializeApplicationMetadata(object? data, SaveSerializerContext context)
{
    return FromInlineSerializerData(data, context.StateType, context.SerializerMetadata);
}

public ISaveDataNode DeserializeApplicationMetadataToNode(object? data, SaveSerializerContext context)
{
    return SimpleNodeConverter.ToNode(data, Migration!.NodeFactory);
}

public object? SerializeApplicationMetadataFromNode(ISaveDataNode node, SaveSerializerContext context)
{
    return SimpleNodeConverter.FromNode(node);
}
```

`SaveApplicationMetadata.SchemaVersion` is owned by core. `SaveApplicationMetadata.Data` is owned by the serializer and should use the serializer's native inline data model. For JSON that means readable JSON values. For compact serializers it may be a compact object model that the serializer can later read from `SaveMetadata`.

#### Payload Transforms

Use `TransformedSaveSerializer` with `ISavePayloadTransform` when you only need to encode bytes produced by an existing serializer.

```csharp
public sealed class ToyEncryptionTransform : ISavePayloadTransform
{
    public string FileExtensionSuffix => ".enc";

    public byte[] Encode(byte[] data)
    {
        return ToyCipher.Encrypt(data);
    }

    public byte[] Decode(byte[] data)
    {
        return ToyCipher.Decrypt(data);
    }
}

var serializer = new TransformedSaveSerializer(
    new JsonSaveSerializer(JsonSaveFormatting.Compact),
    new ToyEncryptionTransform());
```

The transform suffix must be a file-extension suffix such as `.enc`. `TransformedSaveSerializer` forwards contextual serialization, migration, serializer metadata, and application metadata support to the inner serializer when available. `CompressedSaveSerializer` is the built-in GZip wrapper and is the intended public compression API.

#### Companion Serializer Packages

MessagePack support lives in the optional `Workes.SaveSystem.MessagePack` companion package for dependency reasons. The core package does not reference MessagePack directly and does not implement field maps itself; it provides the contextual serializer, provider manifest metadata, and inline application metadata plumbing the companion uses with `SaveMetadata.SerializerMetadata`.

The companion package supports the same default migratable root model as JSON: object roots, array/list roots, string-key dictionary/map roots, primitive roots, and null roots. Provider migrations use provider-root `ISaveDataNode` trees, not serializer envelopes. Field-map metadata applies to object-root DTOs only; root arrays/lists, string-key dictionaries, primitives, and nil/null are provider root values, not fake object field maps. The companion also supports nullable provider state, inline application metadata through the reserved synthetic payload key `__workes_application_metadata`, and core-owned provider manifests serialized as normal save metadata.

### Data Node Contracts

`ISaveDataNode` is the format-neutral edit surface used by migrations. Normal application code should not need to implement or manipulate data nodes outside migration steps.

Concrete data-node implementations and built-in data-node factories are implementation details. Migration code should use the `ISaveMigrationCapableSerializer.NodeFactory` supplied by the serializer that is reading or writing the migrated payload.

Data-node implementations should:

- report node type consistently;
- preserve object keys and array order;
- fail clearly when callers use object operations on arrays or primitive operations on objects;
- keep mutations local to the represented serialized tree;
- support the primitive and null node types exposed by `ISaveDataNodeFactory`;
- expose null values through `IsNull()` and allow existing nodes to be replaced with null through `SetNull()`;
- allow existing nodes, including root nodes, to be replaced with same-factory nodes through `ReplaceWith(...)`;
- reject attempts to combine nodes created by another serializer or factory instance.

For the built-in JSON serializer, JSON payloads are converted into package-owned migration nodes before migration steps run, then converted back to JSON after migration. The Newtonsoft JSON model remains an implementation detail of JSON parsing and writing, not the migration edit surface.

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

### Long-Lived Save Checklist

For saves expected to survive package and application updates:

- keep provider keys stable and human-readable;
- use the default save path and file-name resolvers unless there is a strong reason not to;
- keep state DTOs simple and serializer-friendly;
- bump provider schema versions deliberately;
- keep migration steps deterministic;
- add tests that load representative old payloads;
- avoid using current time, random values, engine state, or other providers during migrations;
- document intentionally breaking save-format changes in application release notes.
