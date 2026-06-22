# Work Tracker

This file is the durable planning tracker for the save system work. Keep it updated after each slice so planning state does not live only in chat history.

## To-do

1. Design application metadata provider support for after the preview release.
   - Keep clear ownership boundaries:
     - core `SaveMetadata` remains required save-system metadata;
     - `SaveSerializerMetadata` remains serializer-owned implementation metadata;
     - application metadata gets its own dedicated section and API.
   - Target API should mirror normal provider registration, e.g. `RegisterMetadataProvider(saveMenuMetadataProvider)`.
   - Use a typed metadata provider contract such as `ISaveMetadataProvider<TMetadata>` with `MetadataSchemaVersion`, `CaptureMetadata()`, and `RestoreMetadata(...)`.
   - Application metadata should be optional; if no metadata provider is registered, saves behave as they do now.
   - Application metadata should support its own schema version and migration source, reusing the existing save data node migration model where possible.
   - Keep this out of the preview implementation, but use this shape as the final design target before `1.0.0` if application metadata becomes a v1 feature.

2. Add non-mutating save validation APIs before the preview release.
   - Add `ValidateSave(TIdentity identity)` and `ValidateBackupSlot(TIdentity identity, int slotNumber)`.
   - Return a structured result, likely `SaveValidationResult`, rather than a Boolean `CanLoadSave(...)`.
   - Require successful `ValidateRegistrations()` because full validation needs registered providers, schematics, state types, migration sources, and serializer configuration.
   - Validation must not restore providers, call load lifecycle hooks, run recovery, write migrated data, delete temp folders, rotate backups, or otherwise mutate disk/runtime state.
   - Validate metadata, serializer metadata, required provider files, schema-version extraction, migration path availability, in-memory migration, deserialization, and provider state type compatibility.
   - Reuse `SaveLoadStatus` where possible so validation and try-load failures speak the same status language.
   - Include core `SaveMetadataInfo` in successful validation results so save menus can display timestamps without a second read.
   - Update README and tests.

3. Improve README coverage for options, metadata, schema versioning, and migrations.
   - Add a compact `SaveSystemOptions.Create(...)` showcase listing common settings and how to pass them.
   - Add a section explaining which save-system metadata exists: save id, created time, last-written time, and serializer metadata.
   - Expand schema-version documentation: what provider schema versions mean, when to bump them, and why they are stored inside payloads.
   - Expand migration documentation with practical examples for helper steps and custom data-node steps.
   - Fix migration examples so users modify the current `PlayerState` shape instead of implying they must keep old `PlayerStateV1`/`PlayerStateV2` runtime DTOs around.

4. Design metadata-backed provider manifest support for newly added providers before `1.0.0`.
   - Current `MissingProviderFileBehavior.Skip` is too broad because it can hide deleted/corrupt provider files.
   - Add save metadata that records which provider files existed when a save was written, including save key and schema version.
   - Use the manifest to distinguish old saves written before a provider existed from current saves with a missing provider file.
   - Add save-format/provider-introduction versioning as core compatibility data, likely on the normal provider/options contract rather than a separate optional interface.
   - Add an optional default-state hook, such as `CreateDefaultStateForMissingSave()`, so providers can opt into deterministic state restoration when loading old saves that predate the provider.
   - If the optional default-state hook is not implemented, older saves that predate the provider should leave the provider's current runtime state untouched.
   - Update migration/load docs and tests.

5. Add realistic cross-package serializer size comparison examples after MessagePack is available as a package.
   - Generate small, medium typical, large repetitive, and large varied/random-ish save examples.
   - Compare pretty JSON, compact JSON, compressed compact JSON, and MessagePack output sizes.
   - Include generated README summaries with byte counts and percentages.
   - Use the results to document realistic compression/MessagePack expectations instead of relying on the current best-case repetitive GZip example.

6. Sync README MessagePack examples after `Workes.SaveSystem.MessagePack` is published.
   - Replace conceptual package-reference examples with the published package version.
   - Link to the companion package README/repository once the URL is final.
   - Keep this core package free of a MessagePack package reference.

## Later

There are no remaining deferred implementation points in this tracker.

## Packaging And Dependency Strategy

These notes are not immediate implementation points, but they should guide the final pre-v1 packaging decisions.

1. Keep the first reusable package focused on completing the current core system.
   - Do not split JSON into a separate package for v1 unless a much stronger dependency-free core requirement appears.
   - Requiring every user to install both `Workes.SaveSystem` and a serializer package would make the first-release experience worse right now.

2. Treat MessagePack as an optional companion package when it lands.
   - The intended shape is `Workes.SaveSystem.MessagePack`.
   - Basic MessagePack serializer work has moved to the separate `Workes.SaveSystem.MessagePack` package.
   - MessagePack migration and serializer-metadata field-map work belongs in the companion package tracker, not this core tracker.
   - The README should show how to install/use MessagePack as a normal `ISaveSerializer` once that package is published.
   - Keep MessagePack implementation late in the v1 process so serializer metadata, compression, transforms, and migration routing are stable first.
   - MessagePack should be positioned as the compact production-save option, not the default readable serializer.

3. Keep GZip compression in the core package unless a real packaging reason appears.
   - `System.IO.Compression.GZipStream` is platform-provided for the current target and does not add a NuGet dependency.
   - Expose `CompressedSaveSerializer` as the public API.
   - Keep the concrete GZip payload transform internal unless users need standalone transform composition.

4. Revisit a dependency-free default serializer as a separate design project after v1.
   - A custom package-owned serializer could make the core system fully self-sufficient without Newtonsoft.
   - The likely serious option is a stable package-owned node/binary serializer built around the migration data-node model.
   - This should not be rushed into v1 because reflection mapping, collections, nested objects, nullable values, enums, compatibility rules, and migration behavior all need careful design.
   - If successful later, it could become the default serializer and allow JSON/Newtonsoft to move to an optional package.

## Completed

These points are completed for the current package migration.

## Completed Milestones

These milestones are completed for the current package migration.

### Compression

- `CompressedSaveSerializer` provides the public GZip compression wrapper API.
- Compressed serializers compose extensions such as `.json.gz`.
- Compression uses platform `System.IO.Compression.GZipStream` and adds no NuGet dependency.
- The concrete GZip payload transform remains internal.
- Compression preserves migration and serializer metadata through the inner serializer capability properties.
- Serializer output examples include compressed compact JSON.

### Core Save Workflows

- Provider registration, validation, snapshots, save/load, existence checks, slot listing, deletion, backup rotation, and force-save repair are implemented.
- `DeleteAllBackupSlots(...)` removes all numbered backup slots for a save identity and returns the deleted count.
- Recovery handles temp/to-delete folders, validates candidates strictly, and avoids running migrations during recovery validation.

### Serializer Architecture

- Serializer payloads are byte-based.
- JSON supports pretty and compact formatting.
- Base64/binary serializer experiments were removed before release.
- Serializer capabilities are exposed through `ISaveSerializer.Migration` and `ISaveSerializer.Metadata`.
- `TransformedSaveSerializer` supports byte transforms while routing migration and metadata correctly.
- MessagePack serializer implementation moved out to the optional `Workes.SaveSystem.MessagePack` companion package so the core package does not take a MessagePack dependency.

### Migration System

- Migration uses format-neutral `ISaveDataNode` trees.
- Data nodes expose explicit `IsNull()` and `SetNull()` helpers for null migration edits.
- Migration helpers cover add/set/remove/rename/move/default/null operations.
- Registration validation catches duplicate/null migration steps.
- Try-load reports migration failures through structured statuses.

### Metadata And Diagnostics

- Save metadata is serialized through the active serializer.
- `SaveMetadata` is public as the property-based serializer-facing metadata payload contract.
- Manager-owned metadata creation and timestamp preparation helpers are internal implementation details.
- Metadata exposes core save id and timestamps.
- Serializer-owned metadata is supported internally for advanced serializers.
- Existing metadata that deserializes to null or the wrong type fails strict; `ForceSaveToDisk(...)` remains the explicit repair path.
- Try-load statuses use internal status exceptions instead of message parsing.

### API Polish

- Relative save paths, typed providers, memory providers, lifecycle callbacks, validation helpers, and read APIs are in place.
- Public API shape tests cover removed/hidden implementation details.

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
