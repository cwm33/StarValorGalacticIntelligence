# Item pricing catalogs

No installed game files or authentic prices were available for this cloud change. Existing local documentation establishes sector commodity price direction, not weapon/equipment base tables, rarity factors or player sell modifiers. Saved market prices are observations and never substitute for an item catalog.

`GET /api/{snapshot}/arsenal?spoilers=false` builds the inventory with unknown valuations. `POST /api/{snapshot}/arsenal/value?spoilers=false` accepts a transient `PricingCatalog`. The browser Arsenal can load the same JSON from a local file. The API uses the already discovery-filtered projection; normal spoiler controls apply. Nothing is saved to disk by this feature.

The typed contract lives in `src/Intelligence/Arsenal.cs`. Schema version 1:

```json
{
  "schemaVersion": 1,
  "gameVersion": "2.251",
  "mechanicsProfile": "Galaxy Reforged",
  "source": "Synthetic documentation example; NOT game prices",
  "confidence": "Synthetic",
  "items": [
    {
      "namespace": "equipment",
      "id": 123456,
      "baseValue": 100,
      "rarityMultipliers": { "0": 1, "1": 2 },
      "replacementMultiplier": 1,
      "liquidationMultiplier": 0.5
    }
  ]
}
```

**All numbers above are synthetic test values.** Do not use them as game prices. Catalog version and profile must match the imported save exactly; generate separate catalog metadata for the selected profile after validation. Catalog identity keys are unique `(namespace, id)` pairs. Values must be finite and nonnegative. Empty catalogs are valid. Missing rarity multipliers or replacement/liquidation multipliers produce unknown estimates, even when base value is known. There is no implicit normal-rarity factor and no default sell percentage.

Per-unit estimate = base value × explicit rarity multiplier × explicit replacement or liquidation multiplier. Per-group estimate additionally multiplies the saved quantity. Numeric overflow is unknown. Coverage counts player groups with known totals. The report includes known quantity, unknown-quantity groups, distinct player models and known priced-subset totals; station observations never enter these totals. Base values and source/confidence labels remain visible even when an estimate is unavailable. JSON exports preserve raw evidence; CSV preserves namespaced IDs and valuation provenance.

Rarity is a separate saved attribute. Equipment modifications, crafted attributes, damage/condition, faction reputation, skills, discounts and transaction conditions are not covered. Estimates are not executable quotes. Catalog `confidence` and `source` are caller attestations, not application verification. A future local extractor can emit this contract; it must not load payload-named types or make the decoder depend on game binaries.

## Local extraction and validation

1. Keep all game binaries, localization, saves, extracts and catalogs outside this repository. Record the installed game build, saved version, selected mod profile and actual mod version/configuration. Hash private inputs before and after read-only analysis.
2. Inspect the installed managed assemblies with a local read-only decompiler. Trace item category dispatch and the actual weapon/equipment/commodity model ID to database entry. Locate the base-price getter and all rarity, modification and buy/sell calculations. Do not assume localization positions are pricing indices. Record method/field names and build evidence privately.
3. Extract only verified numeric catalog entries to a private JSON file. Preserve category namespaces. Emit explicit rarity factors only for enumerated and observed rarity values. Leave unsupported replacement/liquidation multipliers absent. Do not copy station commodity prices into weapon/equipment tables.
4. Validate an unmodified item of each relevant category and rarity against UI prices and controlled purchases/sales. Record credits and quantity before/after, item ID, rarity, modifications, station, faction, skills and refresh state. Separate base price, replacement estimate and actual liquidation proceeds. Repeat under changed reputation/skills and the active Galaxy Reforged configuration to identify price-affecting settings.
5. Load the private catalog in Arsenal. Confirm per-unit and total calculations, exact version/profile rejection, unknown items/rarities, partial coverage and station-stock exclusion. Compare fitted and stored instances of the same ID. Unsupported modifications must remain documented limitations rather than hidden factors.
6. Publish only semantic findings and synthetic reproducers. Do not commit game tables, private paths, real saves or proprietary assembly content. Solar/mineral mapping and ownership/state interpretation require their own controlled before/after observations.
