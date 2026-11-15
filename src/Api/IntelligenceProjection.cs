using System.Globalization;
using Decoder;
using GalacticIntelligence;

// This adapter consumes the already discovery-filtered and enriched projection, never the NRBF graph.
public static class IntelligenceProjection
{
    public static GalaxySnapshot Create(SaveView view, string snapshotId, DateTimeOffset importedAt, bool spoilers)
    {
        IEnumerable<Dictionary<string, object?>> Rows(string table) => view.Tables.GetValueOrDefault(table) ?? [];
        Evidence Source(string table, Dictionary<string, object?> row) => new(table, Int(row, "record"),
            row.Where(p => !p.Key.EndsWith("Resolution", StringComparison.Ordinal)).ToDictionary(),
            Note: "Serialized observation; absence is unknown. Graph record IDs are navigation only.");
        Identity? Id(Dictionary<string, object?> row, string field, string ns)
        {
            if (Int(row, field) is not int value) return null;
            var name = row.GetValueOrDefault(field + "Resolution") as CatalogName;
            return new(ns, value, name?.Name, name?.Status ?? "Unknown", name?.Source);
        }
        Identity? StockItem(Dictionary<string, object?> row) => Id(row, "itemID", Int(row, "itemType") switch
        {
            1 => "weapon", 2 => "equipment", 3 => "commodity", 4 => "ship-model", 5 => "crew",
            int n => $"item-type-{n}", _ => "unknown-item-type"
        });
        var m = view.Metadata;
        var version = Text(m, "savedVersion");
        var profile = Text(m, "mechanics") ?? "Unknown";
        var fields = Rows("Asteroid fields").ToLookup(row => Int(row, "sectorIndex"));
        var deposits = Rows("Asteroid deposits").ToLookup(row => Int(row, "sectorIndex"));
        IndustrialMetrics? Industry(Dictionary<string, object?> row)
        {
            if (version != "2.251") return null;
            var type = Int(row, "sectorType");
            double? solar = type switch { 3 => 0, >= 0 and <= 4 => Number(row, "sunSize"), _ => null };
            var minerals = Number(row, "mineralLevel");
            var knownDeposits = deposits[Int(row, "sectorIndex")].Where(r => Number(r, "quantity") is >= 0).ToArray();
            var nonempty = knownDeposits.Where(r => Number(r, "quantity") > 0).Select(r => Int(r, "asteroidRecord")).Distinct().Count();
            return new(ResourceDeposits: knownDeposits.Length > 0 ? knownDeposits.Sum(r => Number(r, "quantity")!.Value) : null,
                SolarOutput: solar, MineralRichness: minerals,
                ObservedAsteroidFields: fields[Int(row, "sectorIndex")].Count(),
                Environment: type switch { 0 => "Normal", 1 => "Nebula", 2 => "Asteroid rush", 3 => "Pitch black", 4 => "Ship graveyard", _ => null },
                Source: "Star Valor 2.251d sector fields", Confidence: "Verified layout",
                Suitability: new(Percent(solar), Percent(minerals), knownDeposits.Length > 0 ? Math.Min(100, nonempty * 100d / 3) : null, NormalizationSource: "Saved 0–1 factor scaled to 0–100"));
        }
        static double? Percent(double? factor) => factor is >= 0 and <= 1 ? factor * 100 : null;

        var sectors = Rows("Sectors").Where(r => Int(r, "sectorIndex").HasValue).Select(r => new Sector(
            Int(r, "sectorIndex")!.Value, Text(r, "name"), Number(r, "x"), Number(r, "y"), Int(r, "level"),
            Bool(r, "discovered"), Bool(r, "spotted"), Number(r, "population"), Source("Sectors", r), Number(r, "x") + Number(r, "incX"), Number(r, "y") + Number(r, "incY"), Industry(r))).ToArray();
        var stations = Rows("Stations").Where(r => Int(r, "id").HasValue && Int(r, "sectorIndex").HasValue)
            .Select(r => new Station(Int(r, "id")!.Value, Int(r, "sectorIndex")!.Value, Id(r, "factionIndex", "faction"),
                Int(r, "modelIndex"), Int(r, "buildingPlanID"), Bool(r, "discovered"), Int(r, "level"), Source("Stations", r),
                StationSemantics.Classify(Text(r, "type")),
                Int(r, "ownerID") == 0 ? false : null)).ToArray();
        var stationRecords = stations.Where(station => station.Evidence.GraphRecord.HasValue)
            .ToDictionary(station => station.Evidence.GraphRecord!.Value);
        var sectorIds = sectors.Select(sector => sector.Id).ToHashSet();
        var gates = Rows("Generated jumpgates")
            .Where(r =>
                Int(r, "sectorIndex") is int from && Int(r, "connectedSector") is int to &&
                from != to && sectorIds.Contains(from) && sectorIds.Contains(to))
            .Select(r => new JumpConnection(Int(r, "sectorIndex")!.Value, Int(r, "connectedSector")!.Value,
                version is ("2.05" or "2.251") ? "Verified" : "Observed", Source("Generated jumpgates", r))).ToArray();
        var stationIndex = stations.Where(s => s.Id >= 0).GroupBy(s => s.Id).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());
        var markets = new List<MarketObservation>();
        foreach (var table in new[] { "Sector prices", "Codex prices" })
        foreach (var r in Rows(table))
        {
            if (Id(r, "itemID", "commodity") is not { } commodity) continue;
            var stationId = Int(r, "stationID");
            var sectorId = Int(r, "sectorIndex") ?? (Bool(r, "uniqueStationID") != false && stationId.HasValue && stationIndex.TryGetValue(stationId.Value, out var station) ? station.SectorId : (int?)null);
            markets.Add(new(commodity, table, sectorId, stationId, Number(r, "buyingPrice"), Number(r, "sellingPrice"),
                Number(r, "tradePrice"), Number(r, "price"), Bool(r, "IsBuyingOverstocked"), Bool(r, "IsResourceStarved"),
                Bool(r, "IsTrading"), Bool(r, "IsTradingOverstocked"), Source(table, r), table == "Sector prices" && version == "2.251"
                    ? "Sector summary: station buy/sell direction verified; transaction terms unverified" : "Unverified"));
        }
        var inventory = new List<Inventory>();
        foreach (var table in new[] { "Station stock", "Station market modules" })
        foreach (var r in Rows(table))
        {
            var stationRecord = Int(r, "stationRecord");
            if (StockItem(r) is { } item && Int(r, "stationID") is int station)
                inventory.Add(new(item, station, Text(r, "sourceField") == "market" ? "Legacy station market" : table,
                    Number(r, "stock"), Number(r, "lastStockCount"), Int(r, "rarity"), Source(table, r), stationRecord,
                    stationRecord is int record && stationRecords.TryGetValue(record, out var owner) ? owner.SectorId : null));
        }
        var ships = new List<Ship>();
        foreach (var table in new[] { "Player ship", "Player fleet", "Sector ships" })
        foreach (var r in Rows(table))
            ships.Add(new(Id(r, "shipModelID", "ship-model"), table, Int(r, "sectorIndex"), Id(r, "factionIndex", "faction"), Source(table, r)));
        var installed = new List<InstalledItem>();
        foreach (var (table, field, ns) in new[] { ("Installed weapons", "weaponIndex", "weapon"), ("Installed equipment", "equipmentID", "equipment") })
        foreach (var r in Rows(table))
            if (Id(r, field, ns) is { } item)
                installed.Add(new(item, ns, Int(r, "slotIndex"), Number(r, "qnt"), Int(r, "rarity"), Source(table, r)));
        var relations = Rows("Faction relations").Where(r => Int(r, "ID1").HasValue && Int(r, "ID2").HasValue)
            .Select(r => new FactionRelation(Int(r, "ID1")!.Value, Int(r, "ID2")!.Value, Number(r, "value"), Source("Faction relations", r))).ToArray();
        var warnings = view.Warnings.Concat(new[] {
            "Modern base-game sector price direction is established. Freshness, live stock, access and transaction prices require gameplay confirmation.",
            "Routes use saved generated gate topology. Player-built links, activation, direct warp, faction access, cargo capacity and recipes are outside the route and trade models.",
            "Relation endpoints are preserved separately from faction display indexes; no hostility is inferred." }).ToList();
        if (stations.GroupBy(s => s.Id).Any(g => g.Count() > 1)) warnings.Add("Duplicate station IDs excluded from location joins.");
        return new(new(snapshotId, importedAt, version, profile, Bool(m, "baseBuilding"), spoilers),
            new(Text(m, "player"), Number(m, "credits"), Int(m, "level"), Id(m, "shipModelID", "ship-model"), Int(m, "fleetCommander"), Int(m, "currentSectorIndex")),
            sectors, stations, markets, inventory, ships, installed, relations, gates, warnings)
        {
            Cargo = Rows("Player cargo").Select(r => (Row: r, Item: StockItem(r)))
                .Where(entry => entry.Item is not null)
                .Select(entry => new CargoItem(entry.Item!, Number(entry.Row, "qnt"), Int(entry.Row, "rarity"), CargoStorage(entry.Row), Source("Player cargo", entry.Row)))
                .ToArray()
        };
    }
    static string CargoStorage(Dictionary<string, object?> row) => (Int(row, "stockStationID"), Int(row, "itemType")) switch
    {
        (-2, 3) => "Commodity stash",
        (-3, 1) => "Weapon stash",
        (-4, 2) => "Equipment stash",
        (int n, _) when n < -1 => $"Unmapped storage ({n})",
        _ => Text(row, "storageKind") ?? "Unknown"
    };

    static string? Text(Dictionary<string, object?> row, string key) => row.GetValueOrDefault(key) is { } value ? Convert.ToString(value, CultureInfo.InvariantCulture) : null;
    static bool? Bool(Dictionary<string, object?> row, string key) => row.GetValueOrDefault(key) as bool?;
    static int? Int(Dictionary<string, object?> row, string key) => row.GetValueOrDefault(key) is int n ? n : null;
    static double? Number(Dictionary<string, object?> row, string key)
    {
        var value = row.GetValueOrDefault(key);
        if (value is not (byte or short or int or long or float or double or decimal)) return null;
        var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
        return double.IsFinite(number) ? number : null;
    }
}
