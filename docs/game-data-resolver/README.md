# Game data resolver

A standalone .NET 10 library (`src/GameDataResolver`) with no package dependencies, game installation requirement, decoder dependency, or web dependency. All shipped localization and name examples are **synthetic**. No authentic Star Valor localization file or save was available in this cloud task.

`IIdentifierResolver` resolves `(IdentifierCategory, Int32 ID)` and performs reverse name lookup within a category. Extension methods cover commodity, weapon, equipment, ship model, and station model IDs. `new IdentifierCategory("faction")` or another explicit namespace supports additional save fields without a library update. Categories are trimmed, lowercase invariant strings; names retain Unicode and case.

## Build and exercise

From the repository root, with .NET 10 installed:

```sh
dotnet build
dotnet run --project tests/GameDataResolver.Tests
dotnet run --project examples/GameDataResolver.Demo
```

The tests are executable unit tests with a nonzero exit code on failure, following this repository's `tests/Regression` convention. They need no test framework packages. Run them explicitly; `dotnet test` does not execute these console tests. The demo without arguments creates synthetic in-memory mappings. It does not treat Raven or any other actual name as a verified built-in mapping.

The demo also accepts a localization path and a **caller-selected** weapon section number:

```sh
dotnet run --project examples/GameDataResolver.Demo -- "/private/Star Valor/Language/english_main.txt" 123
```

`123` is an invented placeholder, not a known weapon section. A missing file produces an error diagnostic and exit code 1. The demo prints weapon ID 0, ship model ID 55, and unknown commodity ID 999; only the configured weapon namespace can resolve in file mode.

## Configuration and API

```csharp
using GalacticIntelligence.GameData;

// Obtain these from local application configuration; neither value is a library default.
string languagePath = configuration.LocalizationPath;
int weaponSection = configuration.WeaponSection;

IIdentifierResolver resolver = ResolverLoader.Load(new ResolverOptions
{
    LocalizationFiles = [new LocalizationInput(languagePath,
        [new SectionBinding(IdentifierCategory.Weapon, weaponSection)])],
    OverrideFiles = [
        new OverrideInput(configuration.ModMappingPath, SourceKind.ModOverride),
        new OverrideInput(configuration.UserMappingPath, SourceKind.UserOverride)
    ]
});
var result = resolver.ResolveWeapon(0);
var matches = resolver.FindByName(IdentifierCategory.Weapon, "Synthetic Light Emitter");
```

Omit optional override files from configuration when unused. Relative paths resolve against the process working directory; prefer absolute paths resolved by the host. Multiple localization inputs and multiple section bindings are supported, including explicitly assigning a section to more than one category. There are **no default section bindings**. Missing or malformed sources never create a speculative mapping.

Each `IdentifierResolution` contains `RawId`, `Category`, nullable `Name`, nullable `Source` (kind, configured path, optional section and line), `Status`, `Confidence`, and nullable `DiagnosticMessage`. `DisplayName` is a display-only fallback such as `commodity:999`; `Name` remains null when unresolved. Negative IDs are preserved and normally unresolved; external overrides can explicitly map sentinels. Localization entries accept nonnegative Int32 IDs.

Resolution confidence is `Unverified` by default, even when a name exists. A binding or individual override record can explicitly set `CallerValidated`; this records the caller's attestation, not a validation performed by the library. An override never inherits the validation flag of the mapping it replaces. Unresolved results have `Unknown` confidence and a diagnostic message identifying the category/ID. Keep the returned status and raw numeric ID in application DTOs.

Reverse lookup trims the query and uses ordinal case-insensitive equality. It returns all currently winning matches, sorted by numeric ID. Duplicate names may identify multiple IDs; callers must handle ambiguity. It does not fuzzy match, strip accents, normalize Unicode composition, search losing overrides, or cross categories.

`IdentifierRegistry` can also be constructed directly from typed `IdentifierMapping` records (e.g. in tests or from a host-owned store). It is a snapshot with read-only resolution operations and can be shared across threads. Rebuild and replace the snapshot to reload configuration. No automatic scanning or file watcher is installed.

## Parser contract (provisional)

Reported example syntax motivated this grammar; it has not been verified against an authentic file:

```text
# SYNTHETIC EXAMPLE ONLY
[123] Example category
000 Synthetic Light Emitter|description|extra-field|
001 Émetteur léger|
```

- One numbered section header or entry per physical line. Header form: `[nonnegative Int32]`, optionally followed by whitespace and a label. Labels have no semantic effect.
- Entry form: nonnegative Int32 ID, whitespace, then pipe-delimited fields. The first field, trimmed, is the display name. At least one pipe is required. Remaining fields and trailing empty fields are exposed in `LocalizationEntry.Fields`; their semantics are unknown.
- Whitespace-only lines and lines beginning (after trimming) with `#`, `;`, or `//` are ignored. These comment conventions are assumptions for tolerance, not verified game syntax.
- Duplicate section headers combine their records. Duplicate `(section, ID)` entries keep the first and emit `DuplicateEntry` or `ConflictingEntry`. Different section numbers can reuse an ID freely.
- Malformed headers clear the current section until the next valid header. Invalid entries are diagnosed and skipped. Empty names, overflow, and entries outside a section are explicit diagnostics. Future numbered sections and additional pipe fields are accepted without assigning categories.
- The file loader uses strict UTF-8 by default and recognizes standard StreamReader BOMs (e.g. UTF-16 with BOM). Legacy encodings, quoted/escaped pipes, multiline strings, embedded section declarations, and multiple entries per line are not implemented; validate the real format first.
- `LocalizationParser.Parse(TextReader, source)` leaves the reader open; reader I/O exceptions propagate. `ResolverLoader` opens and disposes read-only streams and converts expected file, decoding, and JSON errors to diagnostics. It never writes to input paths.

## Overrides and precedence

External JSON files use schema version 1; property names are case-sensitive:

```json
{
  "schemaVersion": 1,
  "mappings": [
    { "category": "weapon", "id": 0, "name": "Synthetic Mod Emitter" },
    { "category": "ship-model", "id": 55, "name": "Synthetic Frigate", "callerValidated": false }
  ]
}
```

The host assigns `ModOverride` or `UserOverride` to the file through `OverrideInput.Kind`; a file cannot promote itself. Precedence for each category/ID is **user override > mod override > vanilla localization > unresolved** regardless of override file order. Within the same priority, the first valid mapping wins, in configured file/record order. Conflicts are reported rather than silently hidden. Equal names still produce duplicate/provenance diagnostics. Mod data is an explicitly supplied mapping, not a claim that a mod is installed or that a save used its ruleset.

Malformed JSON rejects the whole document; unsupported/missing `schemaVersion` and missing/non-array `mappings` reject the whole document with errors. Malformed individual mappings are skipped with warnings so valid siblings remain usable. Unknown JSON properties in schema version 1 are ignored for additive compatibility. Names/category must be nonempty strings, IDs must be Int32 JSON numbers, and `callerValidated`, when supplied, must be boolean. Future schema versions require deliberate support. There are no built-in game version or mod detection rules.

## Diagnostics and failure policy

Load diagnostics are available on `resolver.Diagnostics`. A missing source (`FileNotFound`), unreadable source (`FileReadError`), invalid encoding (`InvalidEncoding`), invalid JSON (`InvalidJson`), invalid document (`InvalidOverrideDocument`), or incompatible schema (`UnsupportedOverrideSchema`) has error severity. Parser, missing-section, malformed-record, duplicate, and precedence diagnostics have warning severity. The host decides whether any error blocks use; the resolver still returns mappings from valid inputs. For example, a missing optional mod file does not prevent vanilla resolution.

Unknown ID messages are returned per resolution, without mutating the registry or growing a global log. Collect them in a host-owned bounded/deduplicated report if needed. Load diagnostics contain local paths and mapping names: avoid exposing them via remote endpoints or normal exports without review.

Invalid programmer configuration (blank paths/categories, negative section bindings, an invalid override kind) throws an argument exception. Supply non-null configuration collections. This is distinct from bad content in a configured file.

See [INTEGRATION.md](../../INTEGRATION.md) for incorporation steps and [VALIDATION.md](VALIDATION.md) for evidence gaps and the private records needed to close them.
