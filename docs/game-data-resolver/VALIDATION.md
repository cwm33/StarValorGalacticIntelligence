# Validation evidence and outstanding questions

## What was validated here

The implementation was checked against labeled synthetic fixtures only. Repository inspection established .NET 10, executable regression-test conventions, and projected field spellings; it did not establish game catalog semantics. No authentic game assets, saves, or complete localization files were used or committed.

The user supplied preliminary reports of `Star Valor/Language/english_main.txt`, `[1] Weapons` with numbered pipe-delimited entries, and ship model ID 55 associated with the Raven frigate. These remain external observations, not facts independently confirmed by this module. The library intentionally supplies zero default category/section mappings and no Raven mapping.

The synthetic tests cover resolution/provenance, namespace ID reuse, reverse lookup ambiguity, unknown and negative IDs, missing sections/files, malformed and duplicate records, precedence independent of override-file ordering, Unicode, read-only source hashes, invalid JSON/encoding, schema evolution, unknown sections/fields, and arbitrary future namespaces. They establish behavior of this provisional parser and API, not compatibility with every actual game version.

## Private inputs needed for final validation

Keep these outside version control; use read-only access and share the smallest representative records needed:

- The actual English localization path and a small authorized excerpt showing its first headers, whitespace, entry separators, a multi-field record, and the weapon/commodity/equipment/ship/station sections. Include encoding/BOM details and game version. Inspect the full file locally to measure duplicate IDs/sections and any syntax not represented by the excerpt. Do not commit the complete localization dataset.
- Equivalent representative non-English records with accents and non-Latin names, plus any escaped pipes, multiline values, comments, or empty-name records. Compare numeric IDs across languages.
- A vanilla main/Info save pair or minimal field excerpts with exact serialized record type, field path, `shipModelID`, station `modelIndex`/`nameIndex`/`id`, market `itemType`/`itemID`, sector/codex `itemID`, relation `ID1`/`ID2`, and `factionIndex`. Pair each numeric value with an observed in-game name. Prefer multiple item types reusing an ID to prove category boundaries.
- Evidence around ship model ID 55: save field/type, game version, in-game model name, matching localization entry/section, DLC state, and whether Galaxy Reforged was active. The reported Raven association is a useful spot check, not sufficient evidence for a whole namespace.
- Base Building DLC station/building/market examples and corresponding localized catalogs, including generated versus model names and building plans.
- Galaxy Reforged mod version, relevant documented catalog additions/renames/ID allocation, and user-authored JSON mappings with known in-game outcomes. An explicit provenance profile is not proof of mod semantics.
- Representative records from at least two game versions, including newly added categories and IDs. Before/after source hashes should establish that local validation did not alter game data.

## Questions requiring evidence

1. Is the reported language path and exact filename correct for the current installation/platform? Is encoding consistently UTF-8 or BOM-marked UTF-16?
2. Is each record on one line? Can headers omit whitespace after `]`? Are comments, escaped delimiters, rich text, language columns, or multiline text supported? Which pipe field is actually the display name?
3. Which localization sections correspond to each game catalog? Are entry IDs direct internal IDs, indexes with offsets, or a separate localization numbering scheme?
4. What are actual `itemType` values, and do they vary by version/mod/DLC? Can commodity, equipment and weapon IDs overlap? How should records without `itemType` be disambiguated?
5. Do ship model IDs and station `modelIndex` resolve directly? Does `nameIndex` select generated station names rather than models? Are station instance IDs entirely unrelated to name catalogs?
6. Are faction list indexes equivalent to faction IDs/relation endpoints? Which additional namespaces are stable enough to expose?
7. How are mod IDs allocated and localized? Can mods replace vanilla IDs or supply independent catalogs? Which mapping is valid for an imported save versus the currently installed ruleset?
8. What do negative/sentinel IDs mean in each field? Should any resolve to a meaningful label rather than remain unknown?
9. Are category bindings stable across languages and game versions? Should host configuration be keyed by explicit game/DLC/mod provenance? How should a mismatch be signaled?
10. Do repeated localization IDs intentionally encode variants? Is first-entry-wins appropriate, or will verified grammar require a different record key?

Until answered, display raw IDs and uncertainty alongside any resolved names. Reject unsupported override schema versions and inspect diagnostics on game updates. No claim of broad future-version compatibility is made.

## Cloud verification record (2026-11-13)

Environment: .NET SDK 10.0.404, Node.js 24.22.1. Commands run from the repository root:

- `dotnet build` — passed, zero warnings/errors.
- `dotnet run --project tests/GameDataResolver.Tests --no-build` — 24/24 passed.
- `dotnet run --project tests/Regression --no-build` — existing synthetic decoder checks passed.
- `dotnet run --project examples/GameDataResolver.Demo --no-build` — synthetic in-memory demo passed.
- `dotnet run --project examples/GameDataResolver.Demo --no-build -- tests/GameDataResolver.Tests/TestData/synthetic-language.txt 1` — file-backed synthetic resolution passed; unconfigured namespaces remained unresolved.
- `npm --prefix frontend ci` and `npm --prefix frontend run build` — passed; no frontend source or dependency changes.
- `git diff --check` — passed.

Private main/Info regression pairs and authentic localization compatibility could not be tested because none were available. No changed API endpoints required exercising.
