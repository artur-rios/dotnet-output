# Changelog

All notable changes to `ArturRios.Output` are recorded in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- `PaginateByToken` and `PaginateByTokenAsync` work with an `object`-typed key or tiebreaker selector, as a sort column
  picked at run time gives. The first page came back, but the second threw `InvalidCastException` for a reference-type
  property (`x => x.Name`) and `InvalidOperationException` ("cannot compare keys of type System.Object") for a boxed
  value type (`x => (object)x.Id`). The token's value is now read, and compared, as the type the selector returns
  before the cast to `object`.
- A page token is no longer rejected as `PageTokenErrors.Mismatch` when it is read under another culture than the one
  that issued it. The token's fingerprint included the key selector's text, whose constants print in the current
  culture, so with a selector such as `x => x.Id * 1.5` a token issued under en-US did not match under pt-BR. The
  selector is now printed with the invariant culture. Tokens issued under the invariant culture stay valid, and so do
  those issued under any culture for a selector whose text the culture does not change; others, such as pt-BR tokens
  for a selector with a fractional constant, are rejected once, as a mismatch, and the listing restarts from its first
  page.
- `PaginateByToken` and `PaginateByTokenAsync` page through an enum key (`x => x.Status`, `x => (object)x.Status`, or
  a nullable enum). The first page came back, but the second threw `ArgumentException` for an enum, and every page
  threw `InvalidOperationException` ("cannot compare keys") for a nullable enum: an enum has no comparison operator,
  and its only `CompareTo` takes an `object`. The key is now compared as its underlying number, the order `OrderBy`
  sorts it in and, for EF Core, the column an enum is stored in by default.
- `PaginateByToken` and `PaginateByTokenAsync` with a page size of `int.MaxValue` return every item. They returned an
  empty page, as a successful one, since the one item they read past the page to know whether there is a next one
  made the count wrap to a negative number; against PostgreSQL the query failed instead.
- A `PageTokenOptions.Lifetime` as long as `TimeSpan.MaxValue` no longer makes every token's read throw
  `ArgumentOutOfRangeException`: a token's age is now compared with the lifetime, instead of its issue time plus the
  lifetime, which overflowed, with the current time.

## [3.3.2] - 2026-11-14

### Changed

- `Microsoft.EntityFrameworkCore` updated from 10.0.11 to 10.0.15.

## [3.3.1] - 2026-10-29

### Changed

- The package's project URL is the documentation site's new address, `https://docs.artur-rios.tech/dotnet-output`, since the
  site moved from GitHub Pages.

## [3.3.0] - 2026-10-13

### Added

- Token pagination: `PaginateByToken` and `PaginateByTokenAsync` page an `IQueryable<T>` by signed continuation token
  instead of page number. Each page resumes after the previous one with a keyset filter (`WHERE key > @lastKey`), so
  deep pages stay fast, rows added or removed between requests are neither repeated nor skipped, and the count query
  is skipped unless `includeTotalCount` is set. Overloads take a tiebreaker for keys that repeat, and a
  `SortDirection`.
- Tokens are HMAC-SHA256 signed, so a caller can only reach a page with the token issued by the page before it:
  an edited or forged token is rejected. `PageTokenOptions` carries the signing key, an optional `Lifetime` and an
  optional `Scope` that binds tokens to one listing. A rejected token returns an empty output carrying one of the
  `PageTokenErrors` (`Invalid`, `Expired`, `Mismatch`) instead of throwing.
- `PaginatedOutput<T>.NextPageToken`, `HasNextPage` and `WithNextPageToken`.

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

[Unreleased]: https://github.com/artur-rios/dotnet-output/compare/3.3.2...HEAD
[3.3.2]: https://github.com/artur-rios/dotnet-output/compare/3.3.1...3.3.2
[3.3.1]: https://github.com/artur-rios/dotnet-output/compare/3.3.0...3.3.1
[3.3.0]: https://github.com/artur-rios/dotnet-output/compare/3.2.0...3.3.0
[3.2.0]: https://github.com/artur-rios/dotnet-output/compare/v3.1.0...3.2.0
[3.1.0]: https://github.com/artur-rios/dotnet-output/compare/v3.0.0...v3.1.0
[3.0.0]: https://github.com/artur-rios/dotnet-output/compare/v2.0.1...v3.0.0
[2.0.1]: https://github.com/artur-rios/dotnet-output/compare/v2.0.0...v2.0.1
[2.0.0]: https://github.com/artur-rios/dotnet-output/compare/1.0.2...v2.0.0
[1.0.2]: https://github.com/artur-rios/dotnet-output/compare/1.0.1...1.0.2
[1.0.1]: https://github.com/artur-rios/dotnet-output/releases/tag/1.0.1
