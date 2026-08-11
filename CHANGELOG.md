# Changelog

This file records notable user-facing changes to `Workes.SaveSystem`.

## [1.0.1] - 2026-08-11

### Documentation

- Added `CHANGELOG.md` as first-class package documentation and packs it into the NuGet package root alongside `README.md`.
- Updated `README.md` into a concise package landing page with installation, a minimal example, companion package guidance, and links into the full documentation set.
- Added focused public documentation for quick start, configuration, save operations, providers, serialization, metadata, backups and recovery, migrations, extension points, and engine integration.

## [1.0.0] - 2026-06-25

### Added

- Added provider-root migration nodes for object, collection, dictionary, primitive, and null payloads.
- Added `ReplaceWith` support for migration nodes.
- Added contextual serializer migration plumbing for metadata-backed serializers such as MessagePack companion serializers.
- Added nullable provider state support for reference types and `Nullable<T>`.
- Added optional typed application metadata providers with inline serializer-native metadata data, independent schema versions, and migrations.
- Added provider manifests with default-state hooks for old saves that predate newly added providers.

[1.0.1]: https://github.com/WorkesLibraries/Workes.SaveSystem/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/WorkesLibraries/Workes.SaveSystem/releases/tag/v1.0.0
