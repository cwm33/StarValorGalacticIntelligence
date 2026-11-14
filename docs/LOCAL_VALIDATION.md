# Local validation of Galactic Command

The local integration used Steam build 26582067 (2.251d), its English catalog, private 2.05 and 2.251 main/Info pairs, and two earlier 2.251 snapshots. The installed assembly contains Base Building code and the modern main save records an enabled Base Building expansion. BepInEx was present with an empty plugin directory. No Galaxy Reforged assembly, configuration, localization or independently identified save was available. The import profile remains user-selected provenance. See also [historical resolver validation](game-data-resolver/VALIDATION.md).

## Local findings

| Evidence | 2.05 main | 2.251 Base Building main |
| --- | ---: | ---: |
| Total / discovered sectors | 3,075 / 103 | 1,152 / 26 |
| Root station registry / local small bases | 2,967 / 0 | 673 / 121 |
| Discovered station rows | 120 | 152 |
| Generated directed gate records / visible directed links | 2,988 / 131 | 1,076 / 30 |
| Visible price / inventory observations | 2,782 / 10,991 | 1,066 / 1,089 |
| Player cargo and storage rows / aboard | 211 / 13 | 114 / 21 |

- In 2.251d the normal station registration path assigns a game ID, adds the station to `GameDataInfo.stationList`, and links the ID from the sector. `TSector.smallBases` holds direct references to local stations. The two collections had no overlapping graph references or duplicated game IDs in the examined main saves. The modern small-base collection added hideouts and turrets. The implementation preserves distinct graph objects and joins their inventory by graph record within the import. Legacy codex rows have only a game ID and require global uniqueness before location or discovery is inferred.
- Generated gates are serialized `JumpGate` objects under `TSector.jumpGates`. `GalaxyMap` and `JumpgateStationControl.ConnectedSector` index the sector list using `connectedSector`. No invalid, self or non-reciprocal endpoints appeared in the authentic samples. Routing accepts directed links and preserves null sector slots. The discovery-safe view requires the source sector, destination sector and gate to be discovered. Generated gate triggers check runtime activation and a connected sector. The inspected trigger has no faction or discovery prerequisite. Direct warp has a separate map action and is outside this graph.
- Player-built gates use `JumpgateStation.connectedJumpgateStation`. The game sets reciprocal references and derives the destination from the referenced station's sector. The generated NPC wrapper uses sentinel station ID -99 and is instantiated around generated gate data. Thus a repeated sentinel could identify wrappers in a layout that serialized them, but the inspected saves contain neither such records nor any player-built gate. Player gates can connect within a sector and depend on generator power, energy and cooldown. They are excluded from routing until a populated fixture is observed. Live map and gate-prompt confirmation remains pending.
- In the 2.251d `ItemMarketPrice.UpdateValues` implementation, `buyingPrice` is the highest input-only market offer price, a player proceeds reference. `sellingPrice` is the lowest in-stock output-only market price, a player cost reference. `tradePrice` is the lazily populated galactic market reference for two-way trade. An input-only stock below 20 sets `IsResourceStarved`. `IsTrading` indicates a two-way module entry. The overstock flags indicate that the refresh found no understocked module in the corresponding group. They can be true when that group is absent, so read them with the direction/trading flags. Prices of -1 are sentinels. Stock changes affect one-way prices. Sector summaries do not carry an individual offer, live access, or player transaction modifiers.
- Legacy `station.market` is saved station stock. Modern `itemStock.items` is station inventory. `SM_Market.persistentItems` is saved market-module inventory used with runtime market entries. Legacy codex rows are saved price observations. Freshness, player transaction prices and quantities, reputation and skill modifiers, and mod-specific economics still require controlled transactions. The modern authentic imports produced no positive paired one-way spreads in the discovered view. Synthetic smoke tests exercised positive spreads and verified the direction.
- `spaceShipData.cargo` contains aboard and stored items. `stockStationID == -1` is aboard, nonnegative means station storage, and other negatives remain unidentified storage in the app. Type 4 is a ship item, with a saved loadout ID when present. Total cargo space depends on live ship stats, item volume and special holds. The saved Fleet Commander skill is distinct from total capacity. `character.mercenaries` supports the `PlayerFleetMember` subtype and its captain ID. No member was present in the examined saves. The root crew database includes NPCs and is not used as an owned roster.
- The 2.251d ownership predicate is `ownerID == 1`. One modern player-owned station was visible, with no serialized module rows to validate a production flow. Market inventory is not treated as player-accessible input stock.
- The modern main and backup shared 689 station IDs with matching type and sector index. Main and autosave shared 794. All 1,152 sector list positions had matching coordinates in both comparisons. These results support that campaign only. Comparison still excludes graph IDs, ambiguous station IDs and cross-save sector matching.

Local loopback runs imported both pairs, the modern backup and autosave. They checked safe/full gate counts, direct paths, hidden endpoints, trade status and advisor results with the local English catalog configured. A browser pass on the modern backup and main exercised Atlas routes, Market Intelligence, Trade Analyzer, Home Sector Advisor, Fleet Command, Industry and a same-campaign Snapshot Comparison after reimport. The solution build, resolver (26/26), intelligence (19/19), synthetic decoder and both authentic paired regressions, Vue type check/build, synthetic loopback smoke and Playwright (6/6) passed. SHA-256 comparison found no changed bytes across 170 input files under the save, language, managed assembly and BepInEx directories and the game executable. No private inputs or decompiled source belong in Git.

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
- Check coordinates, pan/reset and selection against the in-game map. Check empty coordinates and a large galaxy at desktop and narrow widths. The atlas does not equate station faction with ownership and should render only explicit generated gate endpoints. Compare a visible path against the in-game gate prompt and map.
- Compare each market source separately. Legacy station `market`, modern `itemStock.items`, module `persistentItems`, legacy codex and sector prices must not be summed or treated as interchangeable. Preserve negative/sentinel fields. Verify both known names and numeric namespace IDs, including an ID shared by a weapon and commodity.
- Confirm Trade Analyzer is empty by default. Experimental output must remain labeled non-executable, with unknown quantity/profit when supporting data is missing. Check that only supported modern base-game summaries use the assembly-confirmed direction.
- Test headquarters weights, zero weights, unknown criteria and three-way comparison. Scores should describe observed presence; no missing criterion should act as negative evidence.
- Compare installed weapons, slots and equipment quantities with the player ship. Confirm sector ships and the player fleet roster remain separate. Check cargo storage labels against the game inventory. Base-game reference names must retain their confidence label for unverified profiles/versions.
- In Industry, use privately verified recipes and confirmed personal stocks. Check profile/version rejection, negative/duplicate quantities, missing stock, zero stock and a multi-recipe scenario. Results are aggregate balances, not a feasible production schedule.
- Pin a discovered snapshot baseline, then import a later same-campaign save. Confirm credits, station IDs, matching codex prices, source-specific stocks, loadouts and relations. Verify missing/duplicate keys do not imply destruction or loss. Version changes must produce a compatibility warning. Sector list indices must not be used to claim new sectors. Reload the tab and confirm baseline memory is gone.

## Establish economic and industrial semantics before enabling them

For each supported game version and profile, capture a private before/after save around a controlled small purchase and sale. Record station, commodity namespace/ID, displayed price, credits, cargo quantity, station stock, time/refresh and faction state. Check which serialized field changes and which price represents player cost versus proceeds. Repeat across ordinary station stock, market modules and legacy codex. Compare a neutral faction and a reputation/skill change while holding the commodity and stock constant where possible. Keep the observations and copied saves private. Validate demand/stock caps, commodity cargo volume, fees and any modifiers separately.

For Base Building and Galaxy Reforged, record real station ownership identifiers, production inputs/outputs, durations, capacities, mod version and recipe differences. Read assemblies/localization only for evidence; never load payload-named types or commit proprietary assets. Keep private recipe/catalog evidence outside Git; only synthetic equivalents belong in tests.

A future quote adapter must require validated source/version/profile semantics, known quantity constraints and current access evidence before declaring anything executable. A user scenario or an experimental switch must never set that status.

## Verify read-only integrity and handoff

```powershell
$after = $privateInputs | Get-FileHash -Algorithm SHA256
Compare-Object ($before | ForEach-Object { "$($_.Path):$($_.Hash)" }) ($after | ForEach-Object { "$($_.Path):$($_.Hash)" })
git status --short
```

`Compare-Object` should print nothing. Also retain the decoder regressions' built-in before/after hash results. Ensure Git contains no private inputs, catalogs, generated datasets, exports, paths or secrets. Report authentic counts and discrepancies in private local validation notes, then update public confidence documentation only with non-private facts and synthetic reproducers.
