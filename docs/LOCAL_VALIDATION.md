# Local validation of Galactic Command

Cloud development used synthetic saves only. Do not interpret the new tests as validation of Star Valor economics, Base Building production, Galaxy Reforged behavior or authentic save compatibility. Existing historical resolver evidence remains in `game-data-resolver/VALIDATION.md` and `../INTEGRATION.md`.

## Prepare private inputs

1. Check out `feature/galactic-command`; do not merge it before local review. Install .NET 10, Node 22.12+ and Python 3 (the API/browser fixtures use Python).
2. Keep authentic saves, Info companions, localization, game assemblies, mod files, private overrides and generated exports outside the repository. Use copies of at least one legacy 2.05 save pair, one modern 2.251 pair with Base Building and one independently identified Galaxy Reforged pair if available. Do not infer mod provenance from the DLC flag or the version.
3. Record SHA-256 hashes before any app validation. Example PowerShell, with your own private paths:

```powershell
$privateInputs = @('C:\private-fixtures\SaveGameData_01.dat', 'C:\private-fixtures\SaveGameData_01_Info.dat', 'D:\SteamLibrary\steamapps\common\Star Valor\Language\english_main.txt')
$before = $privateInputs | Get-FileHash -Algorithm SHA256
$before | Format-Table
```

The app must only read these files. Never add game data or these hash/path notes to Git.

## Reproduce automated checks

```powershell
dotnet build
dotnet run --project tests/GameDataResolver.Tests
dotnet run --project tests/Regression
dotnet run --project tests/Intelligence.Tests
npm --prefix frontend ci
npm --prefix frontend run build
npx --prefix frontend playwright install chromium
npm --prefix frontend run test:e2e
```

If browser installation through `npx --prefix` is unavailable, run `npx playwright install chromium` from `frontend`. To use an installed Chromium, set `PLAYWRIGHT_CHROMIUM_EXECUTABLE` to its full path. The Playwright configuration starts the loopback API when needed. It uses only a generated fixture in the OS temporary directory, checks the file hash, and removes it afterward. A current API process may be reused; ensure it is running this branch's build.

In a separate terminal, start the API and run the HTTP checks:

```powershell
dotnet run --project src/Api --no-launch-profile
# Another terminal, from repository root:
python tests/api_smoke.py
```

Do not run API smoke and browser tests concurrently: each import replaces the single current session. Smoke checks cover legacy/modern extraction, discovery/raw gates, domain source separation, default/experimental trade, advisor and invalid industrial scenarios.

Then run authentic regression pairs (substitute your paths):

```powershell
dotnet run --project tests/Regression -- 'C:/private-fixtures/SaveGameData_01.dat' 'C:/private-fixtures/SaveGameData_01_Info.dat' 'C:/private-fixtures/SaveGameData_02.dat' 'C:/private-fixtures/SaveGameData_02_Info.dat'
```

These existing regressions expect established layouts with populated markets. An unfamiliar or empty save can need a new explicit expected scenario rather than weakening assertions. The synthetic API writer does not reproduce every authentic NRBF encoding.

## Inspect authentic domain and UI

Configure localization and private overrides as documented in README; restart the server. Import a matching save pair at `http://127.0.0.1:5187` and select the actual provenance profile. Uncheck browser-copy storage if you do not want recent-save copies in IndexedDB.

- Compare Save Explorer metadata, sector/station counts, IDs, stock and installed loadout against `/api/{session-id}/intelligence?spoilers=false` and the game UI. Export intelligence JSON locally for inspection.
- With spoilers off, verify that every sector is discovered and every station is discovered in a discovered sector. Check market and inventory joins reveal no hidden station/sector details. Enable spoilers, inspect spotted and undiscovered styling, then disable them and confirm hidden selections disappear.
- Check coordinates, pan/reset and selection against the in-game map. Check empty coordinates and a large galaxy at desktop and narrow widths. The atlas does not equate station faction with ownership and must not render geographic jump links.
- Compare each market source separately. Legacy station `market`, modern `itemStock.items`, module `persistentItems`, legacy codex and sector prices must not be summed or treated as interchangeable. Preserve negative/sentinel fields. Verify both known names and numeric namespace IDs, including an ID shared by a weapon and commodity.
- Confirm Trade Analyzer is empty by default. Experimental output must remain labeled non-executable, with unknown quantity/profit when supporting data is missing. Do not promote its price-direction interpretation based on a suggestive field name.
- Test headquarters weights, zero weights, unknown criteria and three-way comparison. Scores should describe observed presence; no missing criterion should act as negative evidence.
- Compare installed weapons, slots and equipment quantities with the player ship. Sector ships must remain NPC observations, not a claimed owned fleet. Base-game reference names must retain their confidence label for unverified profiles/versions.
- In Industry, use privately verified recipes and confirmed personal stocks. Check profile/version rejection, negative/duplicate quantities, missing stock, zero stock and a multi-recipe scenario. Results are aggregate balances, not a feasible production schedule.
- Pin a discovered snapshot baseline, then import a later same-campaign save. Confirm credits, station IDs, matching codex prices, source-specific stocks, loadouts and relations. Verify missing/duplicate keys do not imply destruction or loss. Version changes must produce a compatibility warning. Sector list indices must not be used to claim new sectors. Reload the tab and confirm baseline memory is gone.

## Establish economic and industrial semantics before enabling them

For each supported game version and profile, capture a private before/after save around a controlled small purchase and sale. Record station, commodity namespace/ID, displayed price, credits, cargo quantity, station stock, time/refresh and faction state. Check which serialized field changes and which price represents player cost versus proceeds. Repeat across ordinary station stock, market modules and legacy codex. Validate demand/stock caps, commodity cargo volume, fees and any modifiers separately.

For Base Building and Galaxy Reforged, record real station ownership identifiers, production inputs/outputs, durations, capacities, mod version and recipe differences. Read assemblies/localization only for evidence; never load payload-named types or commit proprietary assets. Keep private recipe/catalog evidence outside Git; only synthetic equivalents belong in tests.

A future quote adapter must require validated source/version/profile semantics, known quantity constraints and current access evidence before declaring anything executable. A user scenario or an experimental switch must never set that status.

## Verify read-only integrity and handoff

```powershell
$after = $privateInputs | Get-FileHash -Algorithm SHA256
Compare-Object ($before | ForEach-Object { "$($_.Path):$($_.Hash)" }) ($after | ForEach-Object { "$($_.Path):$($_.Hash)" })
git status --short
```

`Compare-Object` should print nothing. Also retain the decoder regressions' built-in before/after hash results. Ensure Git contains no private inputs, catalogs, generated datasets, exports, paths or secrets. Report authentic counts and discrepancies in private local validation notes, then update public confidence documentation only with non-private facts and synthetic reproducers.
