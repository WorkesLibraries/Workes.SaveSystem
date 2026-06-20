# Work Tracker

This file is the durable planning tracker for the save system work. Keep it updated after each slice so planning state does not live only in chat history.

## To-do

1. Make recovery candidate validation strict about registered persisted provider files even when normal loads use `MissingProviderFileBehavior.Skip`.
   - Recovery is deciding whether an interrupted temp or to-delete folder is valid enough to promote.
   - Partial-load skip behavior should remain available for intentional loads, but recovery candidates should not be considered valid if any registered persisted provider file is missing.
   - Update README recovery guidance and add regression coverage for skip-mode recovery with a missing provider file.

2. Tighten migration data-node ownership for the built-in serializers.
   - `BinarySaveSerializer` currently reuses the JSON data-node implementation and factory because the binary payload is a Base64-encoded package-owned token model backed by the same `JToken` tree used for migrations.
   - That reuse keeps migration helpers shared, but it also means nodes from `JsonSaveSerializer.NodeFactory` can look acceptable to `BinarySaveSerializer.SerializeFromNode(...)`, which conflicts with the serializer-owned `NodeFactory` contract.
   - Decide between owner-token validation on JSON-backed nodes or a separate binary node wrapper/factory, then update README/XML docs and cross-serializer node tests.

3. Add internal load/recovery exception types for stable `TryLoad...` status classification.
   - `ClassifyLoadException(...)` currently relies partly on exception messages, which is brittle as diagnostics evolve.
   - Add internal exception types or a small internal status-carrying wrapper so `SaveLoadStatus` mapping is explicit without changing the public API.
   - Update tests for important classifications.

4. Stop recovery candidate validation from automatically running provider migrations.
   - Recovery validation currently uses the normal deserialize path, which can invoke user migration code while deciding whether a temp/to-delete candidate is valid.
   - Recovery should validate candidate structure and provider-file integrity without mutating serialized data or relying on migration side effects.
   - Define the desired older-schema recovery behavior, update README, and adjust tests that currently expect migration-compatible recovery.

5. Add recovery/test documentation coverage for strict recovery versus partial-load skip behavior.
    - Cover temp-only, main-plus-temp, and main-missing fallback cases with missing provider files under `MissingProviderFileBehavior.Skip`.
    - README should state that skip mode applies to normal loads, while recovery candidate promotion is stricter.

6. Add serializer data-node ownership tests and documentation.
    - Cover JSON serializer rejecting nodes not produced by its compatible factory.
    - Cover binary serializer rejecting nodes produced by the JSON serializer if owner-token or separate-wrapper enforcement is chosen.
    - README and XML docs should state how custom migration-capable serializers couple `DeserializeToNode`, `SerializeFromNode`, and `NodeFactory`.

7. Add migration validation edge-case tests after the new internal exception mapping is in place.
    - Re-check duplicate and missing migration paths once load-status classification no longer depends on exception message matching.

## Later

There are no remaining deferred implementation points in this tracker.

## Completed

These points are completed for the current package migration.

### 1. Created New Package Shell

- `Workes.SaveSystem` contains a solution, source project, and test project.
- Source project targets `netstandard2.1`.
- Test project targets `net9.0`.
- Nullable reference types and XML documentation generation are enabled for the source project.

### 2. Migrated Legacy Runtime Source

- Legacy `Runtime/*.cs` files were copied into `Workes.SaveSystem/src`.
- Source files use the `Workes.SaveSystem` namespace.
- `Newtonsoft.Json` was added as a package dependency for the migrated JSON serializer and data-node implementation.
- Unity-only imports and direct `UnityEngine.Debug` calls were removed from the migrated source.

### 3. Verified Baseline Build

- `dotnet test Workes.SaveSystem.sln` restores and builds the source and test projects.
- The current test project has no tests yet.
- The migrated source initially built with nullable and XML documentation warnings that were tracked for cleanup.

### 4. Cleaned Nullable And XML Documentation Warnings

- Made nullable intent explicit in DTOs, provider entries, metadata, snapshots, serializer return paths, backup paths, and JSON data-node access.
- Added missing XML documentation for public migration and data-node extension contracts.
- Kept `dotnet test Workes.SaveSystem.sln` warning-clean.

### 5. Added Initial Smoke Tests

- Added construction/configuration coverage for save options and default file-name resolution.
- Added duplicate provider registration coverage.
- Added simple JSON save/load coverage using a temporary directory.
- Added memory-only provider coverage confirming snapshot participation without provider data-file persistence.
- `dotnet test Workes.SaveSystem.sln` passes with 4 tests.

### 6. Tightened File-System Safety

- Added tests for invalid save names, invalid file-name resolver output, invalid temp folder suffixes, and similar save names in backup rotation.
- Added temp folder suffix validation to `SaveSystemOptions<TIdentity>`.
- Changed backup directory matching so save names such as `alpha` and `alpha2` cannot affect each other's backup rotation.
- `dotnet test Workes.SaveSystem.sln` passes with 8 tests.

### 7. Clarified Serializer And Migration Behavior

- Added JSON schema-version extraction tests for valid and invalid envelopes.
- Added duplicate migration-step registration coverage.
- Added successful v1-to-v2 migration coverage through `ISaveDataNode`.
- Changed failed migration application to throw `InvalidOperationException` instead of silently skipping the provider.
- Added missing migration path coverage.
- `dotnet test Workes.SaveSystem.sln` passes with 13 tests.

### 8. Covered Provider Lifecycle Behavior

- Added lifecycle tests for capture order, restore order, after-load callback timing, missing-save loads, and unregister behavior.
- Added provider registration validation for null, empty, or whitespace save keys.
- Confirmed load priority controls snapshot capture and restore order.
- Confirmed unregistered providers are excluded from future snapshots.
- `dotnet test Workes.SaveSystem.sln` passes with 18 tests.

### 9. Covered Backup And Recovery Baselines

- Added backup rotation coverage confirming slot 1 is the most recent previous save.
- Added backup slot loading coverage.
- Added disabled-backup load coverage.
- Added recovery coverage for interrupted swaps where main is missing and temp/to-delete exist.
- Added recovery rejection coverage for mismatched temp/main save metadata.
- `dotnet test Workes.SaveSystem.sln` passes with 23 tests.

### 10. Tightened Save Failure Atomicity

- Added tests for provider capture failures and invalid file-name resolver failures during save.
- Confirmed failed saves leave the existing main save loadable.
- Changed `SaveToDisk` to remove its temp folder when pre-swap save preparation fails.
- Confirmed failed saves do not leave `_tmp` or `_toDelete` folders behind.
- `dotnet test Workes.SaveSystem.sln` passes with 25 tests.

### 11. Tightened Snapshot Public API Shape

- Changed `SaveSnapshot.Entry` from mutable public fields to constructor-set read-only properties.
- Added validation for snapshot entries with empty save keys, invalid schema versions, or null states.
- Added tests covering snapshot entry value preservation and rejected invalid entries.
- `dotnet test Workes.SaveSystem.sln` passes with 29 tests.

### 12. Clarified Engine-Neutral Defaults

- Kept the core package free of direct Unity or Godot references.
- Added a path-explicit `SaveManager<StringSaveIdentity>.CreateDefault(ISaveSerializer, string)` overload for engine-owned persistent data paths.
- Kept the app-data `CreateDefault(ISaveSerializer)` overload as a documented convenience for plain .NET applications.
- Added early validation for null manager options, empty save roots, null serializers, and null save-name resolvers.
- Left diagnostics internal and unchanged; configurable logging is deferred unless diagnostics become a public contract.
- `dotnet test Workes.SaveSystem.sln` passes with 34 tests.

### 13. Covered Migration Failure Edge Cases

- Added downgrade rejection coverage for saves written with a newer schema version than the registered provider supports.
- Added invalid migration payload coverage for provider files missing the expected `Data` envelope.
- Added migration-step failure coverage to confirm thrown migration actions stop loading with a clear manager-level failure.
- Confirmed these behaviors already failed closed, so no production code changes were needed in this slice.
- `dotnet test Workes.SaveSystem.sln` passes with 37 tests.

### 14. Covered Backup Tampering And Gapped Backup Slots

- Added coverage for gapped backup slot normalization before rotation.
- Added coverage for deleting tampered backup folders beyond the configured backup count.
- Added coverage confirming unrelated backup folder names are ignored during rotation.
- Confirmed these behaviors already matched the intended backup normalization design, so no production code changes were needed in this slice.
- `dotnet test Workes.SaveSystem.sln` passes with 40 tests.

### 15. Covered Additional Recovery Folder Combinations

- Added recovery coverage for temp-only interrupted saves.
- Added recovery coverage for main-plus-temp interrupted saves.
- Added recovery coverage for main-plus-to-delete interrupted swaps.
- Changed `RecoverSave` to remove stale `_toDelete` folders when the promoted main save already exists and no temp folder remains.
- `dotnet test Workes.SaveSystem.sln` passes with 43 tests.

### 16. Finished Migration Structure Review

- Re-read the package design principles for namespace, structure, project settings, and testing expectations.
- Verified current source files use the `Workes.SaveSystem` root namespace and tests use `Workes.SaveSystem.Tests`.
- Added a reflection guard test so source assembly types must remain under the package root namespace.
- Fixed `VersionedPayload<T>` so the internal serialization DTO is no longer in the global namespace.
- Confirmed the source structure is responsibility-based (`Core`, `Configuration`, `Serialization`, `Data`, `Backup`, etc.) and no longer uses Unity-style `Runtime/`.
- Added a package-local `.gitignore` for `bin/` and `obj/` build outputs.
- The old copy-paste `com.workes.savesystem` source is no longer present, so comparison against legacy `Runtime/` is not possible in this workspace.
- `dotnet test Workes.SaveSystem.sln` passes with 44 tests.

### 17. Reassessed Dependency Direction And Added README

- Confirmed `Newtonsoft.Json` is currently a hard package dependency because metadata persistence, JSON schematics, JSON migration nodes, and the built-in serializer all use it.
- Deferred `System.Text.Json` to a possible future serializer implementation instead of treating it as a drop-in replacement for the current migration node model.
- Added a package README with installation guidance, quick start, normal usage, provider behavior, backup usage, migration basics, Unity/Godot setup notes, and compatibility notes.
- Documented that Unity and Godot consumers should provide engine-owned save root paths while the core package remains engine-neutral.
- `dotnet test Workes.SaveSystem.sln` passes with 44 tests.

### 18. Documented Extension Contracts

- Expanded the README with a dedicated extension section for provider, lifecycle, migration, serializer, and data-node contracts.
- Clarified when to use persisted provider registration versus memory-only snapshot registration.
- Documented migration-step determinism, one-version-at-a-time migration rules, and downgrade rejection.
- Documented custom serializer requirements and the coupling between migration-capable serializers and their data-node implementations.
- Added XML remarks for provider registration, provider compatibility values, lifecycle timing, and migration-capable serializer node ownership.
- `dotnet test Workes.SaveSystem.sln` passes with 44 tests.

### 19. Expanded Compatibility And Versioning Notes

- Split package versioning guidance from provider save-schema versioning guidance.
- Documented provider key and file-name compatibility expectations.
- Added guidance for when provider schema versions should and should not change.
- Documented serializer swap risks and migration limits.
- Added a checklist for long-lived save compatibility across application and package updates.
- `dotnet test Workes.SaveSystem.sln` passes with 44 tests.

### 20. Completed XML Documentation Pass

- Tightened public XML remarks for snapshots, schematics, file-name context, and string save identities.
- Clarified snapshot mutation semantics and validation exceptions.
- Clarified schematic ownership and schema-version timing during provider registration.
- Clarified that file-name resolvers should usually avoid schema versions so migrations can find older payloads.
- Changed `StringSaveIdentity` to reject whitespace save names early, matching manager save-name validation.
- Added regression coverage for whitespace save-name rejection.
- `dotnet test Workes.SaveSystem.sln` passes with 45 tests.

### 21. Added README-Aligned Tests

- Added tests that exercise the README quick-start save/load flow using the same state and provider shape.
- Added a test aligned with the README backup configuration example.
- Closed the last tracked test-suite to-do for example-aligned coverage.
- `dotnet test Workes.SaveSystem.sln` passes with 47 tests.

### 22. Removed Save Identity Marker Constraint

- Removed the `ISaveIdentity` marker interface and the generic constraints from `SaveManager<TIdentity>` and `SaveSystemOptions<TIdentity>`.
- Removed `StringSaveIdentity` because plain `string` identities supersede the simple validated convenience value.
- Added coverage for plain `string` identities and custom value identities resolved through `SaveNameResolver`.
- Normalized null identity handling so save, load, backup-load, and recovery operations throw `ArgumentNullException` before invoking the resolver.
- Updated README examples that referenced the removed identity type.
- `dotnet test Workes.SaveSystem.sln` passes with 52 tests.

### 23. Added Explicit Registration Validation Gate

- Removed eager provider capture/serialize validation from `RegisterProvider<TState>()` so registration stays lightweight.
- Added `SaveManager<TIdentity>.ValidateRegistrations()` to validate persisted provider state capture, serializer compatibility, file-name resolution, file extension validity, and migration policy.
- Required successful registration validation before `SaveToDisk`, `LoadFromDisk`, and `LoadBackupSlotFromDisk`.
- Reset validation state when providers are registered or unregistered.
- Added focused tests for lightweight registration, disk-operation gating, validation-time capture, and validation-time capture failure.
- Updated existing tests to validate registrations during setup where disk save/load behavior is under test.
- Updated README and XML documentation so examples and public method docs explain the validation step.
- `dotnet test Workes.SaveSystem.sln` passes with 58 tests.

### 24. Loosened JSON Schematic Construction

- Removed the `Activator.CreateInstance<T>()` compatibility probe from `JsonSaveSchematic<T>` so schematic creation no longer requires public parameterless constructors.
- Kept real compatibility validation at `ValidateRegistrations()` and load time, using actual provider state and saved data.
- Added coverage for constructor-based Newtonsoft-compatible state DTOs without parameterless constructors.
- Added coverage that validation still rejects state that cannot serialize.
- Updated README and XML documentation to explain that the built-in JSON serializer supports Newtonsoft-compatible constructor-based DTOs when real state can serialize and deserialize.
- `dotnet test Workes.SaveSystem.sln` passes with 60 tests.

### 25. Added Snapshot Restore Validation

- Added `SaveManager<TIdentity>.ValidateSnapshotForRestore()` and call it before `RestoreSnapshot(...)` mutates providers.
- `RestoreSnapshot(null)` now throws `ArgumentNullException`.
- Snapshot restore validation rejects duplicate provider keys, unknown provider keys, schema mismatches, and persisted-provider state incompatible with the registered schematic.
- Registered providers absent from the snapshot remain allowed and are skipped during restore.
- Documented that restore validation is pre-mutation, but provider-thrown restore failures after validation can still leave earlier providers restored.
- Added tests proving validation failures do not mutate providers or run lifecycle callbacks.
- Updated README and XML documentation for restore semantics.
- `dotnet test Workes.SaveSystem.sln` passes with 66 tests.

### 26. Reworked Provider Unregistration Semantics

- Changed `UnregisterProvider(ISaveProvider)` to remove only the exact registered provider instance and return whether removal happened.
- Added `UnregisterProvider(string saveKey)` for explicit key-based removal.
- Reset registration validation only when a provider is actually removed.
- Documented instance-based and key-based unregistration behavior in README and XML documentation.
- Added tests for exact-instance removal, same-key different-instance safety, key-based removal, invalid removal keys, and validation invalidation after removal.
- `dotnet test Workes.SaveSystem.sln` passes with 70 tests.

### 27. Moved Fully To Typed Providers

- Split provider contracts into a metadata-only `ISaveProvider` base and a state-owning `ISaveProvider<TState>` interface.
- Changed persisted registration to `RegisterProvider(ISaveProvider<TState> provider)`, with type inference from the provider instead of repeated state type arguments at call sites.
- Added `RegisterMemoryProvider(ISaveProvider<TState> provider)` for explicit snapshot-only providers that should not write provider files to disk.
- Updated manager internals to store typed capture/restore delegates while keeping snapshots and serializers non-generic.
- Added state-type validation before snapshot restore, including memory-only providers that do not have schematics.
- Updated README examples, extension-contract docs, and tests to use typed providers.
- `dotnet test Workes.SaveSystem.sln` passes with 71 tests.

### 28. Decided Provider File Missing Behavior On Load

- Added `MissingProviderFileBehavior` with strict `Throw` behavior as the default and explicit `Skip` behavior for deliberate partial-load scenarios.
- Added `SaveSystemOptions<TIdentity>.MissingProviderFileBehavior`.
- Changed disk and backup loads so registered persisted providers require matching provider files by default.
- Kept memory-only providers exempt from file loading and kept unknown extra provider files ignored.
- Documented strict missing-file behavior and the skip opt-in in README and XML documentation.
- Added tests for missing provider files, skip-mode partial loads, unknown extra files, partial save folders, and backup-slot missing files.
- `dotnet test Workes.SaveSystem.sln` passes with 76 tests.

### 29. Improved Options Construction Ergonomics

- Added a non-generic `SaveSystemOptions` factory class for common option construction.
- Added `SaveSystemOptions.Create(...)` for string save identities with default temp-folder and provider-file naming.
- Added `SaveSystemOptions.Create<TIdentity>(...)` for custom identities while keeping save-root ownership explicit.
- Added `SaveSystemOptions.CreateWithBackups(...)` overloads for named backup-enabled construction.
- Updated `SaveManager.CreateDefault(...)` to use the options factory internally.
- Updated README examples and README-aligned tests to use the simpler construction path.
- Added tests for string identity defaults, custom identity resolvers, backup-enabled factories, and missing-provider-file policy passthrough.
- `dotnet test Workes.SaveSystem.sln` passes with 79 tests.

### 30. Re-evaluated Migration Data-Node Public Surface

- Kept `ISaveDataNode` and `ISaveDataNodeFactory` as the advanced migration extension surface.
- Added `ISaveDataNodeFactory.CreateNull()` so `SaveDataNodeType.Null` is constructible and the null node contract is complete.
- Updated JSON data nodes to report null values as `SaveDataNodeType.Null`.
- Tightened JSON data-node object and array operations so wrong-shape operations fail with clear `InvalidOperationException`s.
- Added same-implementation validation so JSON data nodes reject nodes created by other data-node implementations.
- Documented null-node support and the same serializer/factory ownership rule.
- Added extension-contract tests for object operations, array operations, null nodes, wrong-shape operations, and foreign-node rejection.
- `dotnet test Workes.SaveSystem.sln` passes with 84 tests.

### 31. Added Simple Migration Helper APIs

- Added `SaveMigrationStep.From(...)` to compose several migration actions into one schema-version step.
- Added helper actions for top-level field defaults, primitive setting, null setting, removal, renaming, and moving.
- Kept one-operation convenience methods such as `SaveMigrationStep.AddIntDefault(fromVersion, key, value)` for simple one-edit migrations.
- Preserved the existing `SaveMigrationStep` constructor so advanced migrations can still manipulate `ISaveDataNode` directly.
- Added README examples for simple helper usage and advanced data-node usage.
- Added tests for helper composition, default handling, primitive setting, removal, rename overwrite behavior, validation, and real disk-load migration.
- `dotnet test Workes.SaveSystem.sln` passes with 92 tests.

### 32. Added Options-Based Diagnostics Callback

- Replaced static console warning output with manager-owned diagnostics configured through options.
- Added `SaveSystemOptions<TIdentity>.WarningSink` and factory parameters for opt-in warning callbacks.
- Routed disabled backup-load warnings, backup normalization warnings, and migration warnings through the warning sink.
- Kept diagnostics silent by default.
- Documented warning-sink usage in README.
- Added tests for default silence, disabled-backup warning callbacks, migration warning callbacks, and options factory passthrough.
- `dotnet test Workes.SaveSystem.sln` passes with 95 tests.

### 33. Added Save Slot Listing

- Added `SaveManager<TIdentity>.ListSaveSlots()` to list available main save slots from the configured save root.
- Returned resolved save folder names as strings because custom `TIdentity` values may not be reconstructable from folder names.
- Ignored backup folders, temp folders, to-delete folders, and folders without save metadata so menus do not show save-system artifacts.
- Sorted listed slots with ordinal string ordering for deterministic UI and test behavior.
- Documented the listing API and identity tradeoff in README and XML documentation.
- Added tests for missing save roots, sorted listing, artifact filtering, and custom identity resolvers.
- `dotnet test Workes.SaveSystem.sln` passes with 99 tests.

### 34. Added Save And Backup Deletion

- Added `SaveManager<TIdentity>.DeleteSave(...)` for main save deletion without requiring provider registration validation.
- Added `SaveManager<TIdentity>.DeleteBackupSlot(...)` for deleting individual numbered backup slots.
- Made main save deletion clean related temp and to-delete artifacts while intentionally leaving backups for explicit backup deletion.
- Allowed backup deletion even when backup creation is currently disabled so cleanup tooling can remove old folders.
- Documented save and backup deletion behavior in README and XML documentation.
- Added tests for main-save deletion, backup retention, missing saves, recovery-artifact cleanup, backup-slot deletion, disabled-backup cleanup, and invalid backup slot numbers.
- `dotnet test Workes.SaveSystem.sln` passes with 105 tests.

### 35. Added Save And Backup Existence Checks

- Added `SaveManager<TIdentity>.SaveExists(...)` for lightweight main-save checks.
- Added `SaveManager<TIdentity>.BackupSlotExists(...)` for lightweight numbered backup checks.
- Chose raw, non-mutating disk inspection: existence checks do not recover temp folders, load provider data, or require registration validation.
- Required save metadata for both save and backup existence so metadata-less directories do not look like valid saves.
- Allowed backup existence checks even when backup creation is currently disabled.
- Documented existence-check behavior in README and XML documentation.
- Added tests for valid saves, missing and metadata-less saves, temp-folder non-recovery, unvalidated managers, valid backups, missing and metadata-less backups, disabled-backup checks, and invalid backup slot numbers.
- `dotnet test Workes.SaveSystem.sln` passes with 113 tests.

### 36. Added Read-Only Save Metadata API

- Added public `SaveMetadataInfo` as a read-only projection of save-system-owned metadata.
- Added `SaveManager<TIdentity>.ReadSaveMetadata(...)` for main save metadata reads.
- Added `SaveManager<TIdentity>.ReadBackupSlotMetadata(...)` for numbered backup metadata reads.
- Added core metadata timestamps: `CreatedAtUtc` and `LastWrittenAtUtc`, while preserving the stable `SaveId` across overwrites.
- Chose read-only core metadata for this slice and documented that application-owned display metadata should remain in providers for now.
- Metadata read APIs return null for missing metadata and throw when a present metadata file is invalid.
- Documented metadata reads and the core/application metadata boundary in README and XML documentation.
- Added tests for save metadata reads, stable save ids, timestamp updates, missing metadata, invalid metadata, backup metadata reads, missing backup metadata, and invalid backup slot numbers.
- `dotnet test Workes.SaveSystem.sln` passes with 120 tests.

### 37. Added Try-Load Result APIs

- Added public `SaveLoadStatus` for structured load outcomes.
- Added public `SaveLoadResult` with success state, status, message, and captured exception details.
- Added `SaveManager<TIdentity>.TryLoadFromDisk(...)` for non-throwing main save loads.
- Added `SaveManager<TIdentity>.TryLoadBackupSlotFromDisk(...)` for non-throwing backup slot loads.
- Kept the existing strict `LoadFromDisk(...)` and `LoadBackupSlotFromDisk(...)` paths as the source of truth.
- Reported missing saves, disabled backups, invalid requests, unvalidated registrations, missing provider files, migration failures, recovery failures, corrupt data, and fallback load failures through result statuses.
- Documented try-load usage in README and XML documentation.
- Added tests for successful try-loads, missing saves, unvalidated registrations, missing provider files, corrupt data, migration failures, disabled backups, and missing backups.
- `dotnet test Workes.SaveSystem.sln` passes with 128 tests.

### 38. Added Default Binary Serializer

- Added `BinarySaveSerializer` as a built-in serializer that writes `.bin` provider files.
- Added `BinarySaveSchematic<T>` to wrap provider state in the same versioned payload shape as the JSON serializer.
- Implemented a package-owned binary token codec over the existing migration data-node model instead of using unsafe `BinaryFormatter` or adding another dependency.
- Kept migration support by implementing `ISaveMigrationCapableSerializer` and reusing the JSON data-node factory internally.
- Documented that the current serializer contract stores provider payloads as strings, so binary payloads are Base64-encoded on disk rather than written as raw bytes.
- Added tests for binary payload shape, schema-version extraction, manager save/load, migration helper support, and invalid binary payload rejection.
- `dotnet test Workes.SaveSystem.sln` passes with 132 tests.

### 39. Decided System.Text.Json Core Strategy

- Verified that `System.Text.Json` is not available to the current `netstandard2.1` source project without adding an additional package reference.
- Decided not to add a parallel `System.Text.Json` serializer to the core first version because it would not make the package dependency-free while Newtonsoft remains required for migration data nodes, the current JSON serializer, the binary serializer token model, and metadata persistence.
- Decided not to replace Newtonsoft in this slice because that would require a coordinated migration-node, serializer, metadata, and compatibility rewrite rather than a simple serializer swap.
- Documented that a future `System.Text.Json` adapter remains possible when a clear consumer need appears and compatibility limits are acceptable.
- Updated README dependency notes and suggested next steps with the decision.
- `dotnet test Workes.SaveSystem.sln` passes with 132 tests.

### 40. Documented Scope And Provider-Set Composition

- Decided not to add provider-group APIs for the first reusable version.
- Confirmed scope can be modeled with custom save identities and `SavePathResolver` when the same provider set is saved per profile, character, world, or slot.
- Initially documented flat resolved save names, then superseded that with relative save path support in the following slice.
- Documented separate managers as the recommended pattern when different provider sets have different lifecycles, such as global settings versus per-profile gameplay.
- Left true provider groups as a possible future feature only if repeated partial-save domain needs justify the added complexity.
- `dotnet test Workes.SaveSystem.sln` passes with 132 tests.

### 41. Added Relative Save Path Support

- Renamed the public resolver concept from `SaveNameResolver`/`saveNameResolver` to `SavePathResolver`/`savePathResolver`.
- Allowed save identities to resolve to safe relative paths under the manager's save root.
- Rejected absolute paths, empty path segments, `.` and `..` segments, invalid segment characters, reserved `_backup` segments, and path segments that collide with temp/to-delete artifact suffixes.
- Updated atomic save, recovery, deletion, existence checks, metadata reads, and provider file loading to operate on full relative save paths.
- Updated backup storage and rotation so backups mirror relative save paths under `_backup`, such as `_backup/profile-a/slot-1_0001`.
- Changed `ListSaveSlots()` to search recursively and return normalized relative save paths with `/` separators.
- Updated README and XML documentation to describe save path behavior and scope composition through relative paths.
- Added tests for safe nested save paths, rejected traversal, recursive slot listing, artifact filtering in nested paths, and nested backup load/rotation.
- `dotnet test Workes.SaveSystem.sln` passes with 135 tests.

### 42. Added NuGet Package Metadata

- Added package id, version, authors, company, product, description, package tags, project URL, repository URL/type, and package README metadata to the source project.
- Packed the root README into the NuGet package as `README.md`.
- Updated README installation guidance to note that package metadata and README packaging are configured, while publishing still waits on final release/versioning and licensing decisions.
- Did not add a license expression or license file because no package license has been chosen in this workspace.
- Verified `dotnet test Workes.SaveSystem.sln` passes with 135 tests.
- Verified `dotnet pack src\Workes.SaveSystem.csproj` succeeds.

### 43. Tightened Provider Registration Contract Validation

- Rejected duplicate resolved provider file names during `ValidateRegistrations()` so custom `FileNameResolver` collisions cannot overwrite provider files.
- Documented `SaveKey` as stable after provider registration and `SchemaVersion` as stable after registration validation.
- Added runtime checks before disk save/load operations so provider `SaveKey` or `SchemaVersion` drift fails with a clear error instead of producing missing or mismatched provider files.
- Updated README and XML documentation for provider identity/version stability and custom file-name resolver uniqueness.
- Added tests for duplicate resolved file names, changed provider keys after registration, changed provider keys after validation, changed schema versions after validation, and memory-provider schema drift.
- `dotnet test Workes.SaveSystem.sln` passes with 140 tests.

### 44. Clarified Binary Serializer And Non-Null Provider State Contracts

- Updated `BinarySaveSerializer` and `BinarySaveSchematic` XML documentation to describe the package-owned binary token codec instead of BSON.
- Documented that provider `CaptureState()` must return a non-null state object and that empty state should be represented by an explicit DTO.
- Added registration validation for null provider state so persisted providers fail during `ValidateRegistrations()` rather than during first save.
- Updated README provider contract guidance for non-null captured state.
- Added regression coverage for null provider state during registration validation.
- `dotnet test Workes.SaveSystem.sln` passes with 141 tests.

### 45. Documented Disabled Backup Try-Load Ordering

- Kept the chosen `TryLoadBackupSlotFromDisk(...)` behavior where disabled backups return `SaveLoadStatus.BackupSystemDisabled` before validating identity, slot number, or provider registrations.
- Added tests covering disabled backups with null identity, invalid slot numbers, and unvalidated registrations.
- Updated README try-load guidance so backup-disabled UI checks are documented as cheap and non-throwing.
- `dotnet test Workes.SaveSystem.sln` passes with 143 tests.

### 46. Added Try-Register Provider Convenience APIs

- Added `TryRegisterProvider(...)` and `TryRegisterMemoryProvider(...)` convenience APIs that tentatively register a provider and immediately run the normal global registration validation path.
- Failed try-registration now removes the attempted provider and restores the previous registration-validation state.
- Successful try-registration leaves the manager validated for disk save/load operations.
- Updated README provider setup guidance with try-registration usage and rollback semantics.
- Added tests for successful persisted try-registration, failed persisted try-registration rollback, previous validation-state preservation, and successful memory-provider try-registration.
- `dotnet test Workes.SaveSystem.sln` passes with 147 tests.

### 47. Simplified Migration-Capable Serializer Factory Ownership

- Removed `ISaveDataNodeFactory` inheritance from `ISaveMigrationCapableSerializer`.
- Kept migration node creation available through the explicit `ISaveMigrationCapableSerializer.NodeFactory` property.
- Removed duplicate public node-factory pass-through methods from the built-in JSON and binary serializers.
- Updated migration engine internals, README guidance, and XML documentation to use the serializer-owned node factory.
- Added public-shape coverage ensuring migration-capable serializers expose node creation through `NodeFactory` only.
- `dotnet test Workes.SaveSystem.sln` passes with 148 tests.

### 48. Hardened Save Recovery Candidate Validation

- Changed recovery to validate candidate save folders through the normal load-compatible path, including metadata checks, provider file checks, schema-version extraction, migrations, deserialization, and snapshot validation.
- Preferred valid temp saves during interrupted swaps, but fell back to a valid `_toDelete` previous save when the temp save was corrupt and the main save was missing.
- Added warning-sink diagnostics when recovery falls back from an invalid temp save to the previous save.
- Preserved recovery artifacts when neither temp nor previous-save candidates could be validated.
- Added coverage for corrupt temp fallback, all-candidate-invalid preservation, and recovering an interrupted older-schema temp save through provider migrations.
- Added README recovery guidance covering automatic recovery, migration-compatible validation, fallback behavior, and artifact preservation.
- `dotnet test Workes.SaveSystem.sln` passes with 151 tests.

### 49. Tightened Provider Registration And Removal Edge Cases

- Validated memory-provider captured state during `ValidateRegistrations()` so `TryRegisterMemoryProvider(...)` rejects null state instead of leaving a provider that fails later during snapshot capture.
- Kept memory providers serializer-free while applying the same non-null state contract as persisted providers.
- Changed `UnregisterProvider(ISaveProvider)` to find the exact registered provider instance and remove it by the originally registered key, even if the provider's current `SaveKey` has drifted.
- Updated README provider guidance for memory-provider validation and instance-based unregister behavior.
- Added regression coverage for null memory-provider state during validation/try-registration and unregistering a drifted provider instance.
- `dotnet test Workes.SaveSystem.sln` passes with 154 tests.

### 50. Clarified Registration Validation Limits And Migration List Validation

- Rejected null entries in custom `ISaveMigrationSource.Migrations` lists with a clear registration-validation error before duplicate-version grouping runs.
- Updated public migration source XML documentation to state that the migration list and each migration step must be non-null.
- Clarified README validation guidance so `ValidateRegistrations()` is described as an early write-compatibility check, while deserialize-only issues can still surface during load.
- Updated migration README guidance to mention null migration entries are rejected during registration validation.
- Added regression coverage for null migration steps from custom migration sources.
- `dotnet test Workes.SaveSystem.sln` passes with 155 tests.

### 51. Rejected Null Deserialized Provider Payloads

- Changed JSON and binary schematics to reject envelopes whose provider `Data` payload deserializes to null.
- Wrapped provider deserialization in `SaveManager` with provider context so malformed provider payloads classify as corrupt data in try-load results.
- Documented that valid envelopes with null provider payload data are corrupt save data because provider state must be non-null.
- Added JSON try-load coverage for null provider payload data and binary serializer coverage for null payload data.
- `dotnet test Workes.SaveSystem.sln` passes with 157 tests.

## Maintenance Rules

1. Update This File After Every Completed Slice
2. Move Completed Implementation Points To `Completed`
3. Include fitting README/XML documentation updates in the same slice as the behavior or API change
4. Keep Deferred Implementation Ideas In `To-do`
5. Prefer Updating This File Instead Of Restating The Entire Point List In Chat
6. Normalize numbering when moving / adding / removing elements from any of the lists
7. Ensure numbering for all elements on the list
8. Build and run tests before marking an implementation slice complete
9. Include before/after or new-usage examples in the final slice summary so API ergonomics are visible
