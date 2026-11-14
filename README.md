# Star Valor Galactic Intelligence

A Windows-first, read-only Save Explorer. The ASP.NET Core API and Vue interface run at http://127.0.0.1:5187. The application keeps one imported save pair in memory. Closing the server clears the session.

## Setup and launch

Install the .NET 10 SDK and Node.js 22.12+ (or a newer supported Node.js release). Double-click `Launch.cmd`, then open http://127.0.0.1:5187. Press Ctrl+C in the launcher window to stop. The launcher restores dependencies and builds the frontend. Once dependencies are installed, use the build and run commands below for local development.

```powershell
npm --prefix frontend ci
npm --prefix frontend run build
dotnet build
dotnet run --project src/Api --no-launch-profile
```

In Save Explorer, select `SaveGameData_XX.dat` and optionally the matching `SaveGameData_XX_Info.dat` from the game's `Save_Data` directory. Select a mechanics profile if the save's provenance is known. The browser reads the selected files and sends their bytes only to the loopback server. The server can spool large multipart requests into OS temporary storage during import. There is no save writeback or persistent save database.

The default view includes discovered sectors and stations marked discovered within those sectors. The spoiler switch also enables raw Schema search, reference navigation, and raw JSON export. Normalized JSON and table CSV exports follow the current spoiler setting. Table CSV exports the entire selected table. Raw record IDs are graph-local navigation identifiers, distinct from serialized game IDs.

## Recent saves

The import form remembers up to five successful imports in this browser, including the optional companion and selected mechanics profile. **Quick load** opens the copy from your last import. Import the files again to capture newer gameplay. Uncheck **Keep a browser copy for quick loading** for a one-time import. Use **Remove** or **Clear history** to delete browser copies.

## Game data and diagnostics

The API checks the standard Windows Steam install for `Language/english_main.txt`. For a different Steam library or another install, set the game directory before launching from PowerShell.

```powershell
$env:GameData__InstallationDirectory = 'D:\SteamLibrary\steamapps\common\Star Valor'
.\Launch.cmd
```

The resolver opens localization read-only at startup. Restart after changing configuration. Use **Resolver diagnostics** to see catalog sources, compatibility warnings, conflicts, and the resolved and unresolved IDs in the current view. Tables filter by name or raw ID. **Show original IDs** displays the IDs alongside names. Numeric fields remain available in JSON and CSV.

Optional private JSON files can supply validated mappings through `GameData__UserOverrides` and `GameData__GalaxyReforgedOverrides`. Set each environment variable to the absolute path of the respective file. The mod file applies only to saves imported with the Galaxy Reforged profile. Use [schema version 1](docs/game-data-resolver/README.md) with raw catalog IDs and set `callerValidated` to true only after verifying the mapping for the applicable saves. User overrides take precedence. Keep files outside Git.

## Validation

```powershell
dotnet run --project tests/Regression
dotnet run --project tests/Regression -- "C:/fixtures/SaveGameData_01.dat" "C:/fixtures/SaveGameData_01_Info.dat" "C:/fixtures/SaveGameData_02.dat" "C:/fixtures/SaveGameData_02_Info.dat"
```

The first command checks a handwritten cyclic NRBF fixture and malformed input. The second additionally checks real save pairs, extracted tables, discovery filtering, and before-and-after SHA-256 hashes. The real-save checks expect established layouts with populated market tables. Local regression fixtures were versions 2.05 and 2.251, with the Base Building flag enabled in 2.251. Keep personal saves, exports, and version/provenance notes outside version control.

## Field coverage and uncertainty

- Main-save fields supply player name, level, credits, ship model ID, and game version. Companion metadata is displayed separately because it may reflect a different point in time. An absent Base Building flag is unknown.
- The legacy layout exposes serialized station market stock and saved player trade codex prices. The newer layout exposes sector `ItemMarketPrice` records, station `ItemStock`, and market module `persistentItems`. Amounts and flags retain their serialized field names, including negative values. Pricing direction, freshness, and executable trade information require independent validation.
- Sectors use the position in the serialized sector list. Stations and items retain numeric game IDs. The player ship and ships linked from sector `aiChars` are normalized. Dynamic characters, ship loadouts, and other ship-bearing records are available through raw inspection. Faction tables expose serialized relation endpoints and faction attack-power fields.
- Vanilla and Galaxy Reforged are explicit user-selected provenance labels. Neither fixture has independent Galaxy Reforged provenance. Saved weapon names are available in that profile, along with explicitly validated overrides. Mod sidecars and mod-specific rules remain unverified.
- Unknown fields survive in the raw record graph. Unfamiliar roots are available for raw inspection. Unknown versions receive a warning. Array encodings that the NRBF reader or the decoder's safe materialization path cannot handle produce import errors. Imports are limited to 32 MiB per file with additional array element limits.

The decoder uses the record API described in [Microsoft's NRBF migration guide](https://learn.microsoft.com/dotnet/standard/serialization/binaryformatter-migration-guide/read-nrbf-payloads).

## Standalone game data resolver

The optional [.NET 10 resolver library](docs/game-data-resolver/README.md) resolves category-scoped numeric IDs using explicit localization bindings and external mod/user mappings. The standalone library has no game data dependencies. The API supplies the locally validated bindings described in [INTEGRATION.md](INTEGRATION.md). See the [validation record](docs/game-data-resolver/VALIDATION.md) for coverage and compatibility limits.

```sh
dotnet run --project tests/GameDataResolver.Tests
dotnet run --project examples/GameDataResolver.Demo
```
