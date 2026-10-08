# Changelog

All notable changes to `ArturRios.Output` are recorded in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- `Paginate` and `PaginateAsync` return an empty page when `(pageNumber - 1) * pageSize` does not fit in an `int`.
  The offset used to overflow, usually to a negative number that LINQ and SQLite treat as no offset, so a request for
  a far-out page (for example `pageNumber = int.MaxValue`) came back holding the first page's items.

## [3.2.0] - 2026-08-24

### Changed

- `PaginateAsync` strips the `Convert(..., Object)` node from the `orderBy` expression before ordering, as `Paginate`
  already did, so a relational provider can translate a value-type ordering key to SQL.
- `Paginate` and `PaginateAsync` agree on a supplied `totalCount`: it is clamped to a minimum of `0`, reported verbatim
  as `TotalItems`, and never suppresses the page query. The empty-result short circuit applies only when the count
  was computed by the method itself.
- `Paginate` and `PaginateAsync` throw `ArgumentNullException` for a `null` query.
- `AddErrors` / `AddMessages`, and the fluent `WithErrors` / `WithMessages`, accept a `null` collection and ignore it.
- `Microsoft.EntityFrameworkCore` updated from 10.0.10 to 10.0.11.

## [3.1.0] - 2026-07-23

### Changed

- `Data`, `Messages`, `Errors`, `Timestamp` and `PageSize` have public setters, and the `[JsonInclude]` attributes are
  gone, so the output types round-trip under `Newtonsoft.Json` as well as `System.Text.Json`, in both cross-serializer
  directions.
- `Microsoft.EntityFrameworkCore` updated from 10.0.1 to 10.0.10.

### Fixed

- `Messages` and `Errors` are never `null`: assigning `null` — as a deserializer does for an explicit
  `"errors": null` — resets them to an empty list instead of making `Success` throw.

## [3.0.0] - 2026-07-23

### Changed

- **Breaking:** `PaginatedOutput.WithPagination` takes the page size as its second argument:
  `WithPagination(pageNumber, pageSize, totalItems)`.
- **Breaking:** `PaginatedOutput.PageSize` is the requested page size rather than the number of items in the current
  page, and `TotalPages` is `0` when no page size has been set.
- `Microsoft.EntityFrameworkCore` updated from 9.0.10 to 10.0.1.

### Fixed

- `ProcessOutput` and `DataOutput` round-trip through `System.Text.Json`; deserializing used to produce an empty
  envelope regardless of the JSON.
- `TotalPages` was wrong on a partial last page and saturated to `int.MaxValue` on an empty page.

## [2.0.1] - 2025-12-04

### Changed

- Package metadata and the README shipped in the package refreshed. No library code changes.

## [2.0.0] - 2025-11-25

### Changed

- Package version moved to 2.0.0. No library code changes from 1.0.2.

## [1.0.2] - 2025-11-25

### Changed

- Targets `net10.0` instead of `net8.0`.

## [1.0.1] - 2025-11-12

### Added

- `ProcessOutput`, `DataOutput<T>` and `PaginatedOutput<T>` to return messages, errors and typed or paginated
  payloads, with fluent helpers.
- `CustomException`, a base exception carrying several messages.
- `Paginate` and `PaginateAsync` extension methods to paginate an `IQueryable<T>` into a `PaginatedOutput<T>`.

[Unreleased]: https://github.com/artur-rios/dotnet-output/compare/3.2.0...HEAD
[3.2.0]: https://github.com/artur-rios/dotnet-output/compare/v3.1.0...3.2.0
[3.1.0]: https://github.com/artur-rios/dotnet-output/compare/v3.0.0...v3.1.0
[3.0.0]: https://github.com/artur-rios/dotnet-output/compare/v2.0.1...v3.0.0
[2.0.1]: https://github.com/artur-rios/dotnet-output/compare/v2.0.0...v2.0.1
[2.0.0]: https://github.com/artur-rios/dotnet-output/compare/1.0.2...v2.0.0
[1.0.2]: https://github.com/artur-rios/dotnet-output/compare/1.0.1...1.0.2
[1.0.1]: https://github.com/artur-rios/dotnet-output/releases/tag/1.0.1
