# Project guidance

- `src/Decoder` contains the standalone .NET 10 NRBF graph reader and evidence-based projections. Keep it independent of ASP.NET Core and the browser.
- `src/Api` exposes the loopback-only import, view, schema, and export endpoints. Keep the application local and server imports transient. Recent saves are bounded browser-local copies managed by the user.
- `frontend` contains Vue 3, TypeScript, Vite, and Tailwind. The production build goes to `src/Api/wwwroot`.
- `tests/Regression` runs synthetic checks and accepts private main/Info pairs by path. Keep real saves and exports outside version control.

## Read-only behavior

Open game saves for reading only. Never modify, rename, replace, or write back any game save or companion Info file. Use System.Formats.Nrbf and explicit record projections. Never invoke BinaryFormatter deserialization or load types named by a payload. Preserve references and unknown serialized fields in the raw graph. Maintain the spoiler gate on raw inspection and full exports.

## Coding and validation

Keep mappings close to their serialized fields and document the evidence for semantic interpretations. Preserve original numeric IDs, distinguish graph IDs, and record uncertainty. Treat Vanilla and Galaxy Reforged profile labels as provenance unless save-level evidence establishes a stronger claim. Verify both documented market layouts before changing extraction. Run `dotnet build`, the synthetic and applicable private fixture regressions, and `npm --prefix frontend run build`. Exercise any changed API endpoint with a local fixture. Executable trade analysis requires a source/version/profile-validated quote adapter. Current save-derived trade output must remain explicit opt-in experimental; do not fabricate routes, stock, cargo volume or faction access.

Keep game data bindings in the API host and the decoder independent of the resolver. Apply enrichment and per-save diagnostics after discovery filtering. Use path-free source labels in API responses. Keep localization and override files outside Git and read them without write access. See INTEGRATION.md for the distinct catalog namespaces. Run the resolver tests in addition to the existing build and decoder checks.

## Galactic Command domain and analytics

- `src/Intelligence` is independent of NRBF, the resolver, ASP.NET Core and I/O. Add domain calculations here, with synthetic executable unit tests in `tests/Intelligence.Tests`.
- `src/Api/IntelligenceProjection.cs` adapts the already discovery-filtered, enriched SaveView. Never feed a full raw projection to a safe domain feature. Keep safe/full caches and discovery controls consistent across all intelligence endpoints.
- Namespace every item identity. Preserve raw game IDs separately from graph navigation IDs. Duplicate IDs must not silently choose a winner in joins/comparison. Sector list positions are not verified stable across saves.
- Keep legacy codex prices, sector prices, legacy station market, station stock and module inventories separate. `sourceField` in stock projections preserves the original field family. Negative/sentinel prices are not valid transaction quotes. No current adapter emits verified quotes.
- Unknown is nullable, never an implicit zero. Headquarters scores exclude unknown criteria and report weighted evidence coverage. Do not equate sector level with technology or station faction presence with sector ownership.
- User industry catalogs are scenarios, not verified mechanics. Require matching profile/version and explicit sources. Never initialize personal production stock from station markets. Do not bundle unverified game recipes or performance statistics.
- Snapshot comparison uses stable station IDs, catalog namespaces and source-specific unique keys. Exclude graph IDs, ambiguous duplicate keys and sector-list matches. A baseline is optional tab memory and must contain discovered records only; do not add persistence without a user need.
- Validate significant changes with `dotnet build`, all three executable test projects, and `npm --prefix frontend run build`. With the API running, use `python3 tests/api_smoke.py`. Run browser checks with `npm --prefix frontend run test:e2e` (install Playwright Chromium or set `PLAYWRIGHT_CHROMIUM_EXECUTABLE`). Do not run API import suites concurrently, because the host intentionally retains one session.
- Keep `docs/ROADMAP.md` and `docs/LOCAL_VALIDATION.md` current. Cloud synthetic checks must not be described as new installed-game validation. Authentic private inputs and overrides remain outside Git.

## Compatibility, Arsenal and pricing

- Default Galaxy Reforged to baseline catalog/structure compatibility. Keep its label as provenance and apply profile-specific overrides; add exceptions only for established field incompatibilities. Saved numeric values are authoritative. Saved weapon definitions precede localization. Do not turn ordinary lookups into profile warnings.
- Arsenal aggregates personal cargo/storage and fitted records separately from station market projections. Deduplicate serialized graph references within a scope, preserve namespace/ID, rarity and storage IDs, and never add market stock to player totals. Unknown quantities/prices remain nullable.
- Pricing catalogs require exact version/profile selection, explicit source/confidence, unique namespace/ID keys and finite nonnegative values. Rarity multipliers and replacement/liquidation multipliers must be explicit. Equipment modifications and transaction modifiers are separate unsupported attributes. See [ITEM_PRICING.md](docs/ITEM_PRICING.md).
- `ownerID == 1` is ambiguous without subtype and state evidence. Never infer operational player infrastructure from that field alone. Commercial advisor counts exclude turrets, hideouts and gates. Unmapped solar/mineral fields remain unknown; only independently validated normalized suitability metrics may be scored.
