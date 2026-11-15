# Local game data validation

Validated against the installed Steam build 26582067 (game version 2.251d), its English localization, and private main/Info pairs with save versions 2.05 and 2.251. The newer save has the Base Building flag. The fixtures have no independently verified mod provenance.

## Format and evidence

The English file has a UTF-8 BOM, numbered section headers 0–25, CRLF line endings, pipe-terminated records, and hash-prefixed comments. Text can span physical lines. Empty placeholders occupy positions in the catalog. The game reads sections and entries by position. The integration checks the printed numbering against those positions before accepting a section. The installed file loaded without conflicts or format errors.

Read-only inspection of the installed managed assembly confirmed `Lang.LoadFile`, `ItemDB.ApplyLanguage`, `EquipmentDB.ApplyLanguage`, `ShipModelData.modelName`, `FactionDB.ApplyLanguage`, `ItemMarketPrice.AsItem`, and the category dispatch in `ItemStockData.GetNameWithQnt` and `MarketItem.GetName`. The application does not need the assembly to resolve names. See [catalog bindings](../../INTEGRATION.md) for the save-field relationships.

| Section | ID rule | Active names |
| --- | --- | ---: |
| 1 Weapons | Predefined slot | 57 |
| 2 Equipment | Twice the equipment ID | 201 |
| 3 Items | Twice the item ID | 71 |
| 7 Factions | Ten times the faction index | 7 |
| 12 Spaceships | Direct model ID | 110 |

The 2.251 player's raw ship model ID is 55. Section 12 and the enriched API return **Raven**. Other observed examples include station equipment #0 **Nuclear Reactor**, stock commodity #18 **Energy Cells**, sector price commodity #39 **Platinum**, and stock weapon #1 **Burst Light Laser**. Equipment #29 **Large Fusion Reactor** and a saved weapon above the predefined range were resolved in the installed loadout.

## Visible projection results

| Table | 2.05 resolved rows | 2.251 resolved rows |
| --- | ---: | ---: |
| Codex prices | 2,782 / 2,782 | — |
| Sector prices | — | 1,066 / 1,066 |
| Station stock | 10,991 / 10,991 | 492 / 492 |
| Station market modules | — | 597 / 597 |
| Installed player weapons | 13 / 13 | 11 / 11 |
| Installed player equipment | 29 / 29 | 33 / 33 |

Two legacy station model identifiers and one newer faction identifier remained unresolved in the visible projections. Station plans and generated names, faction relation endpoints, crew names, and extended mod catalogs require additional evidence. The installed BepInEx plugin directory was empty and no Galaxy Reforged localization file was found in the installation. Galaxy Reforged now uses the same baseline localization bindings, save-local weapon names and explicit validated overrides. This default follows its gameplay/balance scope; the historical fixtures are not newly identified mod fixtures.

## Regression results

- Full .NET solution build passed with zero warnings and errors.
- Resolver suite passed 26 of 26 tests, including multiline descriptions, blank placeholders, name strides, and rejection of a misnumbered section.
- Synthetic decoder checks and both private main/Info pairs passed. The legacy and modern market layouts yielded 103 and 26 discovered sectors respectively.
- Vue type checking and production build passed.
- Loopback API imports, view and diagnostics endpoints, normalized JSON and CSV exports, and raw schema gating passed. Checks covered the Raven mapping and preserved numeric IDs. The spoiler-disabled views included only discovered sectors and stations. The raw export and schema endpoints rejected requests without spoilers.
- A synthetic Galaxy Reforged override passed the API profile and precedence checks. The diagnostics reported the synthetic conflict without exposing source paths.
- SHA-256 checks verified that the English localization and four main/Info input files remained byte-identical.

No private save, game assembly, localization file, or personal override was committed. The original standalone cloud validation is retained in the imported commit history.
