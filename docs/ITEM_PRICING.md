# Item pricing catalogs

Installed Steam build 26582067 (Star Valor 2.251d) supports a private baseline weapon and equipment catalog. Use the read-only extractor below with an authentic 2.251 manual save or autosave.

`GET /api/{snapshot}/arsenal?spoilers=false` builds the inventory and applies the bundled equipment baseline to 2.251 saves. `POST /api/{snapshot}/arsenal/value?spoilers=false` accepts a transient `PricingCatalog`. The browser Arsenal can load the same JSON from a local file. The API uses the already discovery-filtered projection; normal spoiler controls apply. Nothing is saved to disk by this feature.

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
6. Publish semantic findings, synthetic reproducers and the shared numeric equipment baseline. Do not commit game tables, private paths, real saves or proprietary assembly content. Solar/mineral mapping and ownership/state interpretation require their own controlled before/after observations.


## Shared equipment baseline

The [2.251 equipment catalog](../data/equipment-pricing-2.251.json) contains base values and rarity factors for 201 installed-game equipment IDs. Arsenal applies it automatically to 2.251 saves under all supported import profiles. A manually uploaded catalog overrides it for the current view. Restore default pricing to switch back.

Use the private extractor for weapon prices. Weapon IDs refer to the originating save's weapon list, which can contain crafted or modified definitions. The shared catalog contains only equipment IDs and prices.

## Build 26582067 extraction

With .NET 10, Python 3.11+ and UnityPy installed, run this from the repository root:

```powershell
python tools/ItemPricing/extract.py '<Star Valor installation>' '<private save path>' '<new catalog path outside Git>' --profile 'Galaxy Reforged'
```

Import the same save with that profile and load the JSON from Arsenal. The extractor validates the installed assembly and asset hashes. It reads the Unity Resources equipment index and validates sequential asset identifiers and names. The catalog records the save SHA-256 because weapon definitions and indices can differ between saves. Regenerate it for each newer snapshot.

- Equipment base value uses tech level plus sort power and an equipment-space contribution. Zero-space equipment has a 1.4 factor. Runtime ID assignment uses the Resources load order.
- Weapon base value uses raw normal-rarity DPS, slot space and range, with adjustments for beam components, torpedoes and damage type. Crafted definitions contribute their saved stats. The game applies rarity factors 0.4, 1, 2, 5, 10 and 12 for rarity 0 through 5. Unknown rarities are excluded from valuation.
- The game applies reputation, the purchasing skill and the ship trade modifier on purchases, with a floor of half the rarity-adjusted price. Resale starts at 10% plus the selling skill, then applies the ship trade modifier, adds 60 credits for weapons/equipment, and caps at half the rarity-adjusted price. Crew, ship bonuses and equipment contribute to the ship modifier. Ship hulls use a separate model-power/class/rarity formula, and equipped ship loadouts have their own transaction path.
- Replacement and liquidation estimates remain unavailable for generated catalogs. Effective player modifiers, instance effects, live market access and Galaxy Reforged settings require controlled observations.

### Nuclear Reactor price check

The installed common Nuclear Reactor has a baseline price of 330 credits. In the 2.251d equipment and weapon path, Honored is rank 2 and gives a 6% purchase discount. Bargain (skill 17) gives another 2 percentage points per invested point, applied before the ship trade multiplier. Trading (skill 19) adds 1 percentage point per invested point to the resale rate, which starts at 10%. The resale path has no direct reputation term. It applies the ship trade multiplier, adds 60 credits per item and caps proceeds at half the baseline.

With four Bargain points, zero Trading points and Honored reputation, the reactor purchase formula becomes `283.8 × (1 − shipTradeModifier)` and resale becomes `min(165, 33 × (1 + shipTradeModifier) + 60)`. A ship modifier of 19% would produce about 229.88 credits to buy and 99.27 to sell. This illustrates the reported price pair. The effective live modifier and any mod changes were not independently verified. The catalog retains the pre-transaction value.
