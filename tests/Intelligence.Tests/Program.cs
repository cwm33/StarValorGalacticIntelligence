using Decoder;
using GalacticIntelligence;

var tests = new List<(string Name, Action Body)>();
void Test(string name, Action body) => tests.Add((name, body));
void Require(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
Dictionary<string, object?> Row(params (string Key, object? Value)[] cells) => cells.ToDictionary(p => p.Key, p => p.Value);
GalaxySnapshot Project(SaveView view) => IntelligenceProjection.Create(view, "synthetic", DateTimeOffset.UnixEpoch, false);
SaveView Fixture()
{
    var v = new SaveView();
    v.Metadata["credits"] = 100f;
    v.Metadata["savedVersion"] = 2.251f;
    v.Metadata["mechanics"] = "Galaxy Reforged";
    v.Tables["Sectors"] = [Row(("sectorIndex", 0), ("name", "Synthetic A"), ("x", 1f), ("y", 2f), ("discovered", true))];
    v.Tables["Stations"] = [Row(("id", 101), ("sectorIndex", 0), ("discovered", true))];
    v.Tables["Sector prices"] = [Row(("sectorIndex", 0), ("itemID", 18), ("buyingPrice", -1f), ("sellingPrice", 20f))];
    v.Tables["Codex prices"] = [Row(("stationID", 101), ("itemID", 18), ("price", 17f))];
    v.Tables["Station stock"] = [Row(("stationID", 101), ("itemType", 3), ("itemID", 18), ("stock", 0f)), Row(("stationID", 101), ("itemType", 1), ("itemID", 18), ("stock", 4f))];
    return v;
}
Test("typed projection, provenance and namespace indexes", () => {
    var s = Project(Fixture());
    Require(s.Player.Credits == 100 && s.Provenance.GameVersion == "2.251" && s.Provenance.MechanicsProfile == "Galaxy Reforged");
    Require(s.Index.StationsBySector[0].Single().Id == 101 && s.Index.InventoryByStation[101].Count() == 2);
    Require(s.Index.MarketsByCommodity[("commodity", 18)].Count() == 2);
    Require(s.Inventories.Select(i => i.Item.Namespace).Distinct().Count() == 2);
});
Test("sentinel values and missing fields preserved", () => {
    var s = Project(Fixture());
    Require(s.Markets[0].BuyingPrice == -1 && s.Markets[0].TradePrice is null && s.Inventories[0].Stock == 0);
    Require(s.Sectors[0].Level is null && s.Stations[0].Faction is null && s.Connections.Count == 0);
    Require(s.Markets.All(m => m.PriceSemantics == "Unverified"));
});
Test("legacy observations join locations without mixing prices and stock", () => {
    var s = Project(Fixture()); var codex = s.Markets.Single(m => m.Source == "Codex prices");
    Require(codex.SectorId == 0 && codex.CodexPrice == 17 && codex.BuyingPrice is null);
    Require(s.Markets.Count == 2 && s.Inventories.Count == 2);
});
Test("ambiguous station IDs cannot silently join codex", () => {
    var v = Fixture(); v.Tables["Stations"].Add(Row(("id", 101), ("sectorIndex", 5)));
    var s = Project(v); Require(!s.Index.Stations.ContainsKey(101));
    Require(s.Markets.Single(m => m.Source == "Codex prices").SectorId is null);
    Require(s.Warnings.Any(w => w.Contains("Duplicate")));
});
Test("unknown schema, nonfinite numeric fields and empty inventories", () => {
    var v = new SaveView(); v.Metadata["credits"] = double.NaN; v.Metadata["savedVersion"] = "future";
    var s = Project(v); Require(s.Player.Credits is null && s.Markets.Count == 0 && s.Provenance.GameVersion == "future");
});
Test("real projection discovery gate protects domain and raw evidence", () => {
    var nodes = new List<Node>();
    Link N(string type, Dictionary<string, object?>? fields = null, object?[]? items = null) {
        var id = nodes.Count + 1; nodes.Add(new(id, type, items is null ? "object" : "array", fields, null, items, null)); return new(id);
    }
    var root = N("GameDataInfo, Synthetic", new());
    var known = N("Sector", Row(("name", "Known"), ("discovered", true)));
    var hidden = N("Sector", Row(("name", "SECRET"), ("discovered", false), ("spotted", true)));
    var sectors = N("Array", items: [known, hidden]);
    var station = N("Station", Row(("id", 9), ("sectorIndex", 1), ("discovered", true)));
    var stations = N("Array", items: [station]);
    nodes[0].Fields!["sectors"] = sectors; nodes[0].Fields!["stationList"] = stations;
    var graph = new Graph(root.Ref, nodes);
    var safe = Project(SaveView.Create(graph, null, "Unknown", false));
    var full = IntelligenceProjection.Create(SaveView.Create(graph, null, "Unknown", true), "full", DateTimeOffset.UnixEpoch, true);
    Require(safe.Sectors.Count == 1 && safe.Stations.Count == 0 && full.Sectors.Count == 2 && full.Stations.Count == 1);
    Require(!System.Text.Json.JsonSerializer.Serialize(safe).Contains("SECRET"));
});
var failed = 0;
foreach (var (name, body) in tests) { try { body(); Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + e); } }
Console.WriteLine($"{tests.Count - failed}/{tests.Count} intelligence tests passed (synthetic only).");
return failed == 0 ? 0 : 1;
