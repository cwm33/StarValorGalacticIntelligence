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
        var sectors = Rows("Sectors").Where(r => Int(r, "sectorIndex").HasValue).Select(r => new Sector(
            Int(r, "sectorIndex")!.Value, Text(r, "name"), Number(r, "x"), Number(r, "y"), Int(r, "level"),
            Bool(r, "discovered"), Bool(r, "spotted"), Number(r, "population"), Source("Sectors", r))).ToArray();
        var stations = Rows("Stations").Where(r => Int(r, "id").HasValue && Int(r, "sectorIndex").HasValue)
            .Select(r => new Station(Int(r, "id")!.Value, Int(r, "sectorIndex")!.Value, Id(r, "factionIndex", "faction"),
                Int(r, "modelIndex"), Int(r, "buildingPlanID"), Bool(r, "discovered"), Int(r, "level"), Source("Stations", r))).ToArray();
        var stationIndex = stations.GroupBy(s => s.Id).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());
        var markets = new List<MarketObservation>();
        foreach (var table in new[] { "Sector prices", "Codex prices" })
        foreach (var r in Rows(table))
        {
            if (Id(r, "itemID", "commodity") is not { } commodity) continue;
            var stationId = Int(r, "stationID");
            var sectorId = Int(r, "sectorIndex") ?? (stationId.HasValue && stationIndex.TryGetValue(stationId.Value, out var station) ? station.SectorId : (int?)null);
            markets.Add(new(commodity, table, sectorId, stationId, Number(r, "buyingPrice"), Number(r, "sellingPrice"),
                Number(r, "tradePrice"), Number(r, "price"), Bool(r, "IsBuyingOverstocked"), Bool(r, "IsResourceStarved"),
                Bool(r, "IsTrading"), Bool(r, "IsTradingOverstocked"), Source(table, r)));
        }
        var inventory = new List<Inventory>();
        foreach (var table in new[] { "Station stock", "Station market modules" })
        foreach (var r in Rows(table))
        {
            var ns = Int(r, "itemType") switch { 1 => "weapon", 2 => "equipment", 3 => "commodity", 4 => "ship-model", 5 => "crew", int n => $"item-type-{n}", _ => "unknown-item-type" };
            if (Id(r, "itemID", ns) is { } item && Int(r, "stationID") is int station)
                inventory.Add(new(item, station, Text(r, "sourceField") == "market" ? "Legacy station market" : table, Number(r, "stock"), Number(r, "lastStockCount"), Int(r, "rarity"), Source(table, r)));
        }
        var ships = new List<Ship>();
        foreach (var table in new[] { "Player ship", "Sector ships" })
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
            "Prices are saved observations with unverified direction and freshness; inventories are separate sources, not executable offers.",
            "Jump links, station ownership, faction access, threat, cargo capacity and production recipes have no validated extraction yet.",
            "Relation endpoints are preserved separately from faction display indexes; no hostility is inferred." }).ToList();
        if (stations.GroupBy(s => s.Id).Any(g => g.Count() > 1)) warnings.Add("Duplicate station IDs excluded from location joins.");
        var m = view.Metadata;
        return new(new(snapshotId, importedAt, Text(m, "savedVersion"), Text(m, "mechanics") ?? "Unknown", Bool(m, "baseBuilding"), spoilers),
            new(Text(m, "player"), Number(m, "credits"), Int(m, "level"), Id(m, "shipModelID", "ship-model")),
            sectors, stations, markets, inventory, ships, installed, relations, [], warnings);
    }
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
