# Galactic Command roadmap and evidence

This sprint extends the existing .NET 10 / ASP.NET Core / Vue 3 / TypeScript / Tailwind application. It adds no accounts, cloud services, runtime package dependencies, or database. All new validation in this cloud sprint uses synthetic fixtures. The older installed-game validation record in `game-data-resolver/VALIDATION.md` is historical evidence, not a claim that game files were available here.

## Local integration

- The Atlas renders directed, discovery-gated generated links from authentic 2.05 and 2.251 layouts. Sector details show exits and shortest gate paths. The advisor scores reach within two gate jumps and Trade Analyzer displays gate hops for its opt-in indicative spreads.
- Modern sector summary direction comes from the installed 2.251d implementation. No executable quote is extracted. Galaxy Reforged uses the same baseline interpretation; unsupported versions retain raw observations until their fields are validated.
- Station inventories retain their original game IDs and join by source graph record within the import. Legacy codex with a missing, negative or ambiguous game ID cannot resolve a location. Snapshot comparison excludes ambiguous station IDs.
- Fleet Command shows saved cargo and storage, including ship items, base Fleet Commander skill and typed roster records when present. The modern ownership flag identifies player installations in Atlas and Industry. See [Local validation](LOCAL_VALIDATION.md) for actual inputs and gaps.

## Cloud baseline delivered vertical slices

| Milestone | Delivered | Confidence and remaining work |
| --- | --- | --- |
| 1. Domain | Independent `Intelligence` library; typed snapshots, provenance, sectors, station and faction identities, relations, market observations, namespaced inventories, ships, installed items, explicit jump contract; indexes; API adapter | Extracted fields retain raw values and evidence. Name confidence remains separate. Sector IDs are list positions; cross-save identity is unverified. Ambiguous station IDs cannot join locations. Jump extraction is deferred. |
| 2. Atlas | Coordinates, zoom, drag pan, reset, pointer and keyboard selection, discovery styling, level and population, station panel, faction-presence and market filters, search, responsive map | Only the discovery-filtered domain is supplied to the client. Station faction presence is not sector ownership. Threat is unknown. Only explicit verified links can render; no links are currently extracted. |
| 3. Markets | Source-separated searchable observations and item catalog, names/IDs, location and faction filters, numeric sorting, stock, raw demand flags, negative/sentinel detection, largest same-field spreads | Legacy codex, legacy station market, modern sector prices, modern stock and module stock remain distinct. Price direction/freshness and flags' economic interpretation are unverified. No incompatible sources are combined. |
| 4. Trade | Independently tested quote engine; stock, demand, credits, cargo, existing cargo, access, hostility and cost constraints; bounded top rankings; explicit experimental UI | **No executable offers are extracted.** Default output is empty. Opt-in maps buyingPrice to player cost and sellingPrice to proceeds, explicitly unverified. Quantity, cargo profit, gross and net remain unknown without their required inputs. Codex records lack paired direction and are excluded. |
| 5. Headquarters | Adjustable observed-presence scoring, evidence coverage, per-criterion reasons, search and side-by-side comparison of up to three sectors | Preference heuristics, not game-verified optimal locations. Unsupported tech, resources, friendliness, neighbors and threat stay unknown. Unknown criteria are excluded from scoring, never treated as zero. Positive observed stock is not proof of purchase access. |
| 6. Fleet | Current model, installed weapons/equipment, saved slots/quantity/rarity, name confidence, direct ship values and distinction between NPC observations and owned fleet | Cargo, ownership roster, slot capacity, crew, drones, fitting performance, hull stats and DPS remain deferred. No invented capacity or stat warnings. Typed loadout records permit future catalog-stat providers. |
| 7. Industry | Station supply browser, custom recipe form, profile/version-scoped recipe catalog contract, multi-recipe JSON scenarios, aggregate input/output balances and known shortfalls | **No built-in game recipes.** User scenarios remain unverified. Missing starting stock stays unknown. No market stock is assumed to belong to the player. Ownership, deposits, production rates, sequencing, startup requirements and routing remain deferred. |
| 8. Comparison | Optional in-memory baseline; credits/model changes, newly observed station IDs, matching codex prices, source-specific stocks, raw faction endpoint values, weapon slots and known equipment quantities; JSON export | Same campaign must be indicated by the user. No baseline persistence beyond the current tab. Graph IDs never match records. Duplicate keys are excluded. Sector discoveries/prices, newly acquired fleet ships and missing-as-loss interpretations are deferred because identity/extraction is unverified. |
| 9. UX | Overview, Atlas, Market Intelligence, Trade Analyzer, Home Advisor, Fleet, Industry, Comparison; existing Save Explorer and resolver diagnostics; dark responsive layout, evidence notes, empty states, JSON exports | Technical graph tools and exports retain existing discovery controls. Broader accessibility and large authentic galaxy usability should be checked locally. |
| 10. Handoff | Synthetic domain/analytics, API, browser and integrity checks; updated guidance and local validation instructions | Authentic installed-game, DLC and mod integration remains a local requirement. See LOCAL_VALIDATION.md. |

These are functional, bounded slices, not a declaration that every requested feature in every milestone is complete.

## Architecture

- `src/Decoder`: standalone NRBF reader and evidence-based projections. Stock rows now include `sourceField` to distinguish `market`, `itemStock.items`, and `modules.persistentItems`; module graph IDs are navigation evidence only.
- `src/Intelligence`: no dependency on decoder, resolver, web framework, files or network. `Galaxy.cs` defines typed records and indexes. `TradeAnalyzer`, `HomeAdvisor`, `IndustryPlanner` are pure calculation services.
- `src/Api/IntelligenceProjection.cs`: consumes the **already filtered and enriched** `SaveView`. It does not inspect NRBF nodes. It preserves raw fields, names and catalog confidence. It never infers stock-price joins, faction hostility or routes.
- Session-local safe/full lazy projections are separate. `/api/{id}/intelligence`, `/trade`, and `/advisor` honor the same `spoilers` switch as the existing view. `/api/industry/plan` evaluates transient user scenarios with a local request header and validates profile/version and quantity bounds.
- `frontend/src/intelligence.ts`: typed transport contract. Dedicated Vue components consume domain records. Snapshot comparison is a pure TypeScript function over discovered snapshots and ignores graph record IDs. Baseline memory is independent of recent-save IndexedDB copies.
- Playwright is a development-only dependency; the production app still uses Vue only.

## Next evidence-driven integrations

1. Capture controlled modern purchases and sales to establish freshness, station price and quantity constraints, reputation and skill modifiers. Validate legacy and modded mechanics independently.
2. Identify cargo capacity, commodity volume, current cargo, player fleet and station ownership from authentic records. Keep unknown and zero distinct.
3. Correlate generated endpoints with live gate prompts. Capture a player-built gate pair, including disabled and local configurations, and validate route usability and access.
4. Validate station services, sector technology, faction endpoint mapping and access rules before adding them to scores or trade executability.
5. Supply private recipe/stat catalogs with explicit game/DLC/mod versions and evidence. Add production timing and fleet fitting calculations only after validation.
6. Compare successive authentic saves to validate station/item keys, schema evolution and loadout matching. Extend comparison only when stable keys exist.

## Cloud sprint validation results

- .NET 10.0.404 solution build: passed, zero warnings/errors.
- Existing resolver executable suite: 26/26 passed; includes read-only localization hashes.
- Existing synthetic decoder regression: passed; cyclic references, null/string identity and malformed input. Authentic private fixture arguments were not available.
- New intelligence executable suite: 18/18 passed, including namespace collisions, duplicate IDs, unknown schemas, discovery-gated evidence, sentinels, missing stock/cargo/routes, hostile/unknown access, insufficient credits/cargo, empty inventory, profile/version-scoped custom recipes, unknown advisor criteria, and bounded ranking of 2,000 quotes.
- Vue type checking and Vite production build: passed.
- Playwright: 6/6 passed using installed Chromium. Coverage includes responsive atlas and pointer selection, spoiler toggles, market separation, non-executable experimental trade, headquarters comparison, custom production form, fleet/explorer access, unchanged input-file SHA-256, baseline retention through a later import, a 2,000-sector fixture, comparison namespace/graph-ID boundaries, sentinel deltas, and changed schemas.
- Actual loopback HTTP imports of generated legacy and modern NRBF fixtures: passed. Safe/full projections, market source labels, resolver, JSON exports, raw gates, default/experimental trade, headquarters and malformed/empty industrial scenarios were checked.
- `git diff --check`: passed. No proprietary assets, private saves, localization, recipe datasets, overrides or secrets were added.

These results establish implementation behavior on synthetic data, not authentic economic semantics or mod compatibility.


## Vanilla-compatible defaults and Arsenal (current)

- Galaxy Reforged now retains normal localization, saved weapon catalog resolution, generated gate routing, market direction, nearby discovery and inventory joins. The supported baseline layouts remain 2.05 and 2.251; profile labels are provenance, with targeted exceptions for established incompatibilities.
- Arsenal aggregates personal cargo/station storage and fitted weapons/equipment; market/module stock is a separate scope excluded from player totals. It supports name/ID search, rarity/location/type/quantity filters, sorting, pagination, distinct-model counts, stored ships and filtered CSV/JSON exports.
- The pricing domain and transient API accept user/local-extraction catalogs with exact profile/version, explicit base value, rarity multipliers, replacement/liquidation multipliers and evidence. No authentic pricing data is available in cloud and no prices are bundled. Missing coverage stays unknown. See [item pricing](ITEM_PRICING.md).
- Station classifications exclude defensive structures, hideouts and gates from economic presence. Raw ownership and state are inspectable; `ownerID == 1` does not verify a player base. Industrial, mining and trading presets expose evidence coverage. Solar/mineral/resource/environment metrics have an extensible domain contract and no guessed decoder mappings.
- Remaining local validation: controlled ownership/build/state transitions (including the reported HideoutStation #772), subtype service mappings, solar/mineral normalized criteria, fleet loadouts, material subcategories, item modifications, actual replacement/sale mechanics, and populated player-built gates. Private inputs and proprietary extracts remain outside Git. Cloud results are synthetic validation only.
