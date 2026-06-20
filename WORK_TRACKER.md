# Work Tracker

This file is the durable planning tracker for the save system work. Keep it updated after each slice so planning state does not live only in chat history.

## Current Goal

Move the legacy `com.workes.savesystem` Unity-style package into the new `Workes.SaveSystem` .NET package structure, then polish the API, implementation, docs, and tests into a reusable package that follows the root `design_principles.md`.

## Workspace Boundaries

- Make changes only inside `Workes.SaveSystem` unless the user explicitly changes this rule.
- Reading `design_principles.md` and other packages is allowed for context and style reference.
- Other packages are read-only inspiration and must not be edited as part of this work.
- The old `com.workes.savesystem` folder was only a temporary copy-paste source and has already been deleted by the user; no archive/removal work is needed.

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

## Maintenance Rules

1. Update This File After Every Completed Slice
2. Move Completed Implementation Points To `Completed`
3. Keep README/Style Work In `Documentation To-do`
4. Keep Deferred Implementation Ideas In `To-do`
5. Prefer Updating This File Instead Of Restating The Entire Point List In Chat
6. Normalize numbering when moving / adding / removing elements from any of the lists
7. Ensure numbering for all elements on the list
8. Build and run tests before marking an implementation slice complete
