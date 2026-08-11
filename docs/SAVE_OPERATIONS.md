# Save Operations

This guide covers the day-to-day `SaveManager<TIdentity>` APIs after a manager and providers have been configured.

For first setup, start with [Quick Start](QUICK_START.md), [Configuration](CONFIGURATION.md), and [Providers](PROVIDERS.md).

## Manager Lifecycle

A typical manager flow is:

1. create the manager;
2. register providers and optional application metadata;
3. call `ValidateRegistrations()`;
4. save, load, validate, list, read metadata, or delete slots.

```csharp
var manager = SaveManager<string>.CreateDefault(
    new JsonSaveSerializer(),
    saveRootPath: "Saves");

manager.RegisterProvider(playerProvider);
manager.RegisterProvider(worldProvider);
manager.RegisterMetadataProvider(saveMenuMetadataProvider);
manager.ValidateRegistrations();
```

Call `ValidateRegistrations()` again after registration changes. Disk save/load operations require successful validation.

## Registration Helpers

Use `RegisterProvider(...)` for persisted providers:

```csharp
manager.RegisterProvider(playerProvider);
```

Use `RegisterMemoryProvider(...)` for providers that participate in in-memory snapshots but should not be written to disk:

```csharp
manager.RegisterMemoryProvider(sessionCacheProvider);
```

Memory-only providers are included in `CaptureSnapshot()` and `RestoreSnapshot(...)`. They are not included in provider files, provider manifests, save validation, disk load, or disk save output.

Use try-register helpers when tooling or dynamic setup should report errors without throwing:

```csharp
if (!manager.TryRegisterProvider(playerProvider, out var error))
{
    logger.Warn(error);
}
```

Available helpers:

- `TryRegisterProvider(...)`
- `TryRegisterMemoryProvider(...)`
- `TryRegisterMetadataProvider(...)`

Each helper registers the provider, runs validation, and rolls the registration back if registration or validation fails.

## Unregistering

Providers can be removed by instance or by save key:

```csharp
manager.UnregisterProvider(playerProvider);
manager.UnregisterProvider("player");
```

Application metadata can be removed with:

```csharp
manager.UnregisterMetadataProvider();
```

If an unregister method returns `true`, call `ValidateRegistrations()` again before the next disk operation.

Changing the registered provider set for an existing save root affects future saves and compatibility with old saves. For long-lived saves, treat provider removal, renaming, splitting, and moving as compatibility decisions.

## Saving

Use `SaveToDisk(...)` for normal saves:

```csharp
manager.SaveToDisk("slot-1");
```

The manager captures persisted providers, writes provider files, writes core metadata, writes application metadata when registered, updates provider manifests, and rotates backups when backups are enabled.

Use `ForceSaveToDisk(...)` only for deliberate repair or replacement workflows:

```csharp
manager.ForceSaveToDisk("slot-1");
```

Normal saves protect existing metadata and recovery paths. Force-save is the explicit escape hatch when the caller intentionally wants current runtime state to replace a save that may be invalid or incompatible.

## Loading

Use `LoadFromDisk(...)` when failed loads should throw:

```csharp
var loaded = manager.LoadFromDisk("slot-1");
```

`LoadFromDisk(...)` returns `false` when the save does not exist. Other failures throw `InvalidOperationException`.

Use `TryLoadFromDisk(...)` when UI or tooling needs structured failure information:

```csharp
var result = manager.TryLoadFromDisk("slot-1");

if (!result.Succeeded)
{
    ShowLoadFailure(result.Status, result.Message);
}
```

`SaveLoadResult` exposes:

| Property | Meaning |
|---|---|
| `Succeeded` | `true` when `Status` is `Success`. |
| `Status` | Stable machine-readable outcome. |
| `Message` | Human-readable diagnostic text. |
| `Exception` | Captured exception for error cases, when available. |

Branch on `Status`, not on exception messages.

## Load Statuses

`SaveLoadStatus` values are shared by try-load and validation results:

| Status | Meaning |
|---|---|
| `Success` | The save or backup loaded or validated successfully. |
| `NotFound` | No save or backup metadata folder exists for the request. |
| `BackupSystemDisabled` | A backup load or validation was requested while backups are disabled. |
| `InvalidRequest` | The identity, path, or backup slot request was invalid. |
| `RegistrationsNotValidated` | `ValidateRegistrations()` has not succeeded for the current registration set. |
| `MissingProviderFile` | A required provider file was missing. |
| `MigrationFailed` | Saved data could not migrate to the registered schema version. |
| `RecoveryFailed` | Recovery from an incomplete save operation failed. |
| `CorruptData` | Save data or metadata was present but invalid or unreadable. |
| `LoadFailed` | Loading failed for another reason. |

## Validating Without Restoring

Use `ValidateSave(...)` to check loadability without mutating providers or disk:

```csharp
var validation = manager.ValidateSave("slot-1");

if (validation.IsValid)
{
    var lastWritten = validation.Metadata!.LastWrittenAtUtc;
}
```

Use backup validation for backup slots:

```csharp
var validation = manager.ValidateBackupSlot("slot-1", slotNumber: 1);
```

`SaveValidationResult` exposes:

| Property | Meaning |
|---|---|
| `IsValid` | `true` when `Status` is `Success`. |
| `Status` | Stable machine-readable outcome. |
| `Message` | Human-readable diagnostic text. |
| `Exception` | Captured exception for error cases, when available. |
| `Metadata` | Core save metadata when validation succeeds. |

Validation reads metadata, checks serializer metadata, checks provider files, extracts schema versions, verifies migration paths, and deserializes provider data in memory. It does not restore provider state, run lifecycle callbacks, write migrated files, or perform user-facing repair.

## Backups

Load a numbered backup with the throwing API:

```csharp
var loaded = manager.LoadBackupSlotFromDisk("slot-1", slotNumber: 1);
```

Use the structured backup load API for UI flows:

```csharp
var result = manager.TryLoadBackupSlotFromDisk("slot-1", slotNumber: 1);
```

Backup slot numbers are 1-based. Backup folders use numbered suffixes such as `_0001` and `_0002`.

Backup APIs are covered in more detail in [Backups And Recovery](BACKUPS_AND_RECOVERY.md).

## Existence Checks

Use existence checks for lightweight menu state:

```csharp
bool canLoad = manager.SaveExists("slot-1");
bool hasBackup = manager.BackupSlotExists("slot-1", slotNumber: 1);
```

Existence checks are raw disk checks. They do not recover incomplete saves, load providers, validate registrations, or inspect provider payloads. A save or backup exists only when its folder contains save metadata.

`BackupSlotExists(...)` can be used even when backup creation is currently disabled.

## Listing Slots

Use `ListSaveSlots()` for save/load menus:

```csharp
IReadOnlyList<string> slots = manager.ListSaveSlots();
```

The returned values are resolved relative save paths, not `TIdentity` values, because custom identity resolvers may not be reversible. Results are sorted with ordinal string ordering and use `/` separators. Backup folders, temp folders, to-delete folders, and metadata-less folders are ignored.

## Reading Metadata

Use core metadata reads when a menu needs timestamps or core save information without loading provider files:

```csharp
SaveMetadataInfo? metadata = manager.ReadSaveMetadata("slot-1");
SaveMetadataInfo? backupMetadata = manager.ReadBackupSlotMetadata("slot-1", slotNumber: 1);
```

These methods return `null` when metadata does not exist. They throw when a metadata file exists but cannot be read as valid save metadata.

Use typed application metadata reads when an application metadata provider is registered:

```csharp
SaveMenuMetadata? menu = manager.ReadApplicationMetadata<SaveMenuMetadata>("slot-1");
SaveMenuMetadata? backupMenu = manager.ReadBackupApplicationMetadata<SaveMenuMetadata>(
    "slot-1",
    slotNumber: 1);
```

Application metadata is covered in detail in [Save Metadata](METADATA.md).

## Deleting Saves And Backups

Use delete helpers for save-menu cleanup and tooling:

```csharp
bool saveDeleted = manager.DeleteSave("slot-1");
bool backupDeleted = manager.DeleteBackupSlot("slot-1", slotNumber: 1);
int backupsDeleted = manager.DeleteAllBackupSlots("slot-1");
```

`DeleteSave(...)` removes the main save and related temporary or to-delete folders for the same resolved identity. It does not delete backups.

`DeleteBackupSlot(...)` removes one numbered backup folder. `DeleteAllBackupSlots(...)` removes all numbered backup folders for the identity and returns the number deleted.

Backup deletion can be used even when backup creation is currently disabled.

## Snapshots

Snapshots capture provider state in memory:

```csharp
SaveSnapshot snapshot = manager.CaptureSnapshot();
```

Restore a snapshot without touching disk:

```csharp
manager.RestoreSnapshot(snapshot);
```

Validate a snapshot before restoring it:

```csharp
manager.ValidateSnapshotForRestore(snapshot);
```

Snapshots include persisted providers and memory-only providers. They preserve provider keys, schema versions, load priorities, and captured state objects.

Use snapshots for undo/redo, tests, editor tooling, temporary runtime checkpoints, or transferring state between managers that share a compatible provider set. Snapshots are not a disk format and are not a replacement for save files.

Snapshot restore validates duplicate entries, unknown provider keys, schema mismatches, and state compatibility before mutating providers.

You can also construct a snapshot manually when tests or tooling need to inject known state:

```csharp
var snapshot = new SaveSnapshot();
snapshot.Add(
    key: "player",
    schemaVersion: 1,
    state: new PlayerState { Name = "Rook", Level = 5 },
    loadPriority: 0);

manager.ValidateSnapshotForRestore(snapshot);
manager.RestoreSnapshot(snapshot);
```

Each `SaveSnapshot.Entry` exposes `SaveKey`, `SchemaVersion`, `LoadPriority`, and `State`.

## Operation Choice

| Need | Use |
|---|---|
| Write normal save | `SaveToDisk(...)` |
| Replace invalid save intentionally | `ForceSaveToDisk(...)` |
| Load and throw on errors | `LoadFromDisk(...)` |
| Load and inspect status | `TryLoadFromDisk(...)` |
| Check loadability without restore | `ValidateSave(...)` |
| Populate save/load menu | `ListSaveSlots()`, `SaveExists(...)`, metadata reads |
| Restore backup | `LoadBackupSlotFromDisk(...)` or `TryLoadBackupSlotFromDisk(...)` |
| Validate backup | `ValidateBackupSlot(...)` |
| Delete saves or backups | `DeleteSave(...)`, `DeleteBackupSlot(...)`, `DeleteAllBackupSlots(...)` |
| Capture runtime state only | `CaptureSnapshot()` |
| Restore runtime state only | `RestoreSnapshot(...)` |
