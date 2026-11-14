# Resolver integration

The API registers an immutable game catalog at startup and enriches the existing save views after discovery filtering. The decoder provides raw projections and the save-local weapon catalog. It has no resolver dependency. Import and normalized JSON keep every raw field and add a sibling `*Resolution` object with the raw ID, category, name, status, source label, and localization section where applicable. CSV keeps its numeric columns and adds the corresponding display labels. Raw schema and raw JSON remain behind the spoiler gate.

The Vue table filter matches names and raw IDs in the selected table. **Show original IDs** displays IDs alongside names. **Resolver diagnostics** reports loaded source labels, category and entry counts, load issues, and the identifiers observed in the current view. With spoilers disabled, diagnostics use only the discovered projection.

## Catalog bindings

| Save context | Catalog lookup |
| --- | --- |
| `shipModelID`, station stock type 4 | Section 12 (Spaceships), direct model ID |
| Station stock type 3, sector price `itemID`, legacy trade codex `itemID` | Section 3 (Items), entry `2 × ID`; the following record is the description |
| Station stock type 2, installed `equipmentID` | Section 2 (Equipment), entry `2 × ID`; the following record is the description |
| Station stock type 1, installed `weaponIndex` | Index into main-save `weaponList`, using the saved `TWeapon.name`. Section 1 (Weapons) is loaded for diagnostics. In game build 2.251d, the predefined names in this section address slots below 60. Crafted and modified weapons use the save-local catalog. |
| Station and sector ship `factionIndex` | Section 7 (Factions), entry `10 × index` |
| Station stock type 5 | Crew namespace. Section 23 contains crew-related text. Crew instance ID to display name remains unverified. |
| Station `modelIndex`, `buildingPlanID`, `nameIndex`, and `id` | Separate fields. Section 25 includes building plan labels. In 2.251d the station's building plan has its own localization coordinates, and the station can have a custom name. The legacy model index has no validated localization binding. |
| Relation endpoints `ID1` and `ID2` | Raw values. The app has no verified mapping to faction display indexes. |

The stock type switch applies to market and inventory records. The game's `ItemType` enum classifies items within ItemDB and is a separate concept. Graph record IDs identify decoder records.

The host recognizes base mappings for tested save versions 2.05 and 2.251. The Unknown mechanics profile can use these mappings with a provenance warning. For Galaxy Reforged and untested save versions the host uses saved weapon names and explicitly validated overrides. Equal-priority conflicts in a winning override source suppress the affected name. See [local validation](docs/game-data-resolver/VALIDATION.md) and [configuration](README.md#game-data-and-diagnostics).
