# Integrating the game data resolver

This branch adds `src/GameDataResolver`, `tests/GameDataResolver.Tests`, and `examples/GameDataResolver.Demo` to the solution. It does not wire the resolver into the running API, decoder, or frontend. The existing application remains responsible for category semantics and spoiler/discovery rules. The module builds and tests without game data.

## Incorporation steps

1. Bring the feature branch commits into the application's integration branch when ready. Preserve concurrent decoder/frontend changes. The resolver targets `net10.0`, has no NuGet dependencies, and can be copied or referenced independently of the solution.
2. Add a reference from the consuming project, normally `src/Api/Api.csproj`:

   ```sh
   dotnet add src/Api/Api.csproj reference src/GameDataResolver/GameDataResolver.csproj
   ```

3. Define host configuration for absolute localization paths, explicit category/section bindings, and optional mod and user JSON paths. Keep proprietary files and personal mappings outside the repository. The reportedly installed path is `Star Valor/Language/english_main.txt`; confirm spelling, encoding, and structure locally. Do not scan installations or assume section numbers.
4. Use `ResolverLoader.Load(ResolverOptions)` at startup or explicit configuration reload, inspect `Diagnostics`, and register the snapshot as `IIdentifierResolver`. For example, inside ASP.NET Core setup after constructing `options` from your host configuration:

   ```csharp
   using GalacticIntelligence.GameData;
   IIdentifierResolver resolver = ResolverLoader.Load(options);
   builder.Services.AddSingleton<IIdentifierResolver>(resolver);
   ```

   No dependency injection package is required in the library. For the strongest separation, keep naming in an API/application projection rather than adding dependencies to the NRBF decoder. Replace snapshots atomically on reload rather than modifying shared mappings.
5. Resolve only fields whose namespace has been established. Add a nullable name and resolution status alongside the original numeric field in DTOs; do not replace or reinterpret raw fields. `resolver.ResolveShipModel(id)` is suitable after confirming `id` is a ship model identifier. Unknown category/ID returns null name, raw ID, `Unresolved` status, and an explicit message.
6. Before exposing enriched data, apply the existing spoiler/discovery gate. Do not bypass raw schema/export restrictions or reveal hidden save rows by enriching them. Do not expose server file paths in frontend responses; translate provenance to a host-approved label if necessary. The library's full source information is for local diagnostics.
7. Run the resolver unit tests, the existing synthetic and applicable private regressions, `dotnet build`, and the frontend build. If wiring endpoints later, exercise each changed endpoint using a private local fixture as required by `AGENTS.md`. This branch changes no endpoints.

## Current save fields and namespace boundaries

These field spellings are observed in this repository's `src/Decoder/SaveView.cs`, not independently decoded from authentic saves in the cloud:

| Serialized field / projection | Integration contract | Evidence still needed |
| --- | --- | --- |
| Player/sector ship `shipModelID` | `ResolveShipModel(id)` | Corresponding localization section and version/mod provenance |
| Station stock / market modules `itemType` + `itemID` | Explicit host translation of itemType to category, then `Resolve(category, itemID)` | Enum meanings, cross-category reuse, mod behavior |
| Sector price and codex `itemID` without itemType | Preserve ID; resolve only after proving namespace from record context | Whether these are always commodities or another item catalog |
| Station `modelIndex` | `ResolveStationModel(id)` after namespace validation | Whether modelIndex actually addresses a localized station model catalog |
| Station `id`, `nameIndex`, `buildingPlanID` | Distinct custom categories only after validation | Instance ID vs catalog index, generated station names, building plan namespace |
| `factionIndex`, relation endpoints `ID1`/`ID2` | Possible `new IdentifierCategory("faction")` | Index/ID equivalence and localization table |
| Raw graph record references | Keep graph navigation unchanged | Graph record IDs are not game catalog IDs |

`itemType` values must never be cast to section numbers. No automatic itemType switch is shipped. Weapon, equipment, and commodity convenience methods express the intended integration contracts; they do not certify save enum mappings. Other identifiers can use explicit `IdentifierCategory` instances once their semantics are established.

## Validation before trusting names

Follow [the validation checklist](docs/game-data-resolver/VALIDATION.md). Record game version, language, DLC, mod version and evidence for each binding. Keep `CallerValidated` false until matched against observed gameplay and save IDs; the flag is a caller attestation. Do not turn the reported Raven ID 55 observation into a general built-in mapping without validating the correct namespace and game/mod version.

All library file operations use `FileAccess.Read`; the host must retain the project's existing read-only save policy. No BinaryFormatter, game type loading, runtime memory inspection, BepInEx, or installation side effects are needed.
