# Backups And Recovery

`Workes.SaveSystem` can rotate backup slots and recover from interrupted save writes.

## Backups

Use backup-enabled options when saves should preserve previous versions:

```csharp
var options = SaveSystemOptions.CreateWithBackups(
    saveRootPath: "Saves",
    serializer: new JsonSaveSerializer(),
    backupSystemMaxBackupCount: 3);

var manager = new SaveManager<string>(options);
```

When a save is overwritten, previous content can be moved into numbered backup slots.

## Loading Backups

Load a backup slot by number:

```csharp
var loaded = manager.LoadBackupSlotFromDisk(
    "slot-1",
    slotNumber: 1);
```

Backup slots are useful for save-menu recovery, debug tooling, and user-facing rollback flows.

Use the structured try-load API when UI needs a status instead of an exception:

```csharp
var result = manager.TryLoadBackupSlotFromDisk(
    "slot-1",
    slotNumber: 1);
```

## Validating Saves

Use validation APIs when tooling needs to inspect loadability without mutating runtime provider state:

```csharp
var result = manager.ValidateSave("slot-1");
```

Backup slots can also be validated before a user attempts recovery.

```csharp
var result = manager.ValidateBackupSlot(
    "slot-1",
    slotNumber: 1);
```

For save-menu checks and display data:

```csharp
bool hasBackup = manager.BackupSlotExists("slot-1", slotNumber: 1);
var metadata = manager.ReadBackupSlotMetadata("slot-1", slotNumber: 1);
```

## Recovery

Recovery handles interrupted write folders such as temporary or to-delete folders. Recovery validation is intentionally strict and avoids running migrations while deciding which on-disk candidate is safe.

`ForceSaveToDisk(...)` remains the explicit repair path when the caller wants to overwrite an invalid or partially broken save with the current runtime state.

## Cleanup

Use delete APIs for save-menu cleanup or debug tooling:

```csharp
manager.DeleteSave("slot-1");
manager.DeleteBackupSlot("slot-1", slotNumber: 1);
var deleted = manager.DeleteAllBackupSlots("slot-1");
```

Deletion helpers operate on the configured save root and identity/path rules.
