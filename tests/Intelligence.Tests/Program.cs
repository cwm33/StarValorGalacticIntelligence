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
    v.Tables["Stations"] = [Row(("record", 44), ("id", 101), ("sectorIndex", 0), ("discovered", true))];
    v.Tables["Sector prices"] = [Row(("sectorIndex", 0), ("itemID", 18), ("buyingPrice", -1f), ("sellingPrice", 20f))];
    v.Tables["Codex prices"] = [Row(("stationID", 101), ("itemID", 18), ("price", 17f))];
    v.Tables["Station stock"] = [Row(("stationRecord", 44), ("stationID", 101), ("itemType", 3), ("itemID", 18), ("stock", 0f)), Row(("stationRecord", 44), ("stationID", 101), ("itemType", 1), ("itemID", 18), ("stock", 4f))];
    return v;
}
Test("typed projection, provenance and namespace indexes", () => {
    var s = Project(Fixture());
    Require(s.Player.Credits == 100 && s.Provenance.GameVersion == "2.251" && s.Provenance.MechanicsProfile == "Galaxy Reforged");
    Require(s.Index.StationsBySector[0].Single().Id == 101 && s.Index.InventoryByStationRecord[44].Count() == 2);
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
Test("station graph identity, duplicate game IDs and gate visibility", () => {
    var nodes = new List<Node>();
    Link N(string type, Dictionary<string, object?>? fields = null, params object?[] items)
    {
        var id = nodes.Count + 1;
        nodes.Add(new(id, type, fields is null ? "array" : "object", fields, null, fields is null ? items : null, null));
        return new(id);
    }
    var rootFields = Row(("savedVersion", 2.251f));
    var root = N("GameDataInfo, Synthetic", rootFields);
    var stock = N("Stock", Row(("itemType", 3), ("itemID", 18), ("stock", 7)));
    var a = N("Station, Synthetic", Row(("id", 123), ("sectorIndex", 0), ("discovered", true), ("ownerID", 0), ("market", N("Array", null, stock))));
    var b = N("JumpgateStation, Synthetic", Row(("id", 123), ("sectorIndex", 3), ("discovered", true), ("ownerID", 1)));
    var known = N("JumpGate, Synthetic", Row(("discovered", true), ("connectedSector", 3)));
    var unknown = N("JumpGate, Synthetic", Row(("discovered", false), ("connectedSector", 3)));
    var invalid = N("JumpGate, Synthetic", Row(("discovered", true), ("connectedSector", 1)));
    var hidden = N("JumpGate, Synthetic", Row(("discovered", true), ("connectedSector", 2)));
    var origin = N("Sector", Row(("discovered", true), ("smallBases", N("Array", null, a)),
        ("jumpGates", N("Array", null, known, known, unknown, invalid, hidden))));
    rootFields["sectors"] = N("Array", null, origin, null, N("Sector", Row(("discovered", false))), N("Sector", Row(("discovered", true))));
    rootFields["stationList"] = N("Array", null, a, b);
    var price = N("Price", Row(("itemID", 18), ("price", 7)));
    var trade = N("TradeRecord", Row(("stationID", 123), ("items", N("Array", null, price))));
    var codex = N("Codex", Row(("records", N("Array", null, trade))));
    var ally = N("PlayerFleetMember, Synthetic", Row(("crewMemberID", 5),
        ("shipData", N("Ship", Row(("shipModelID", 12))))));
    var npc = N("AIMercenaryCharacter, Synthetic", Row(("shipData", N("Ship", Row(("shipModelID", 13))))));
    rootFields["character"] = N("Character", Row(("codex", codex), ("mercenaries", N("Array", null, ally, npc))));

    var graph = new Graph(root.Ref, nodes);
    var view = SaveView.Create(graph, null, "Unknown", false);
    var snapshot = Project(view);
    Require(snapshot.Markets.Count == 0);
    Require(snapshot.Ships.Single().Model?.Id == 12 && snapshot.Ships.Single().Role == "Player fleet");
    Require(snapshot.Stations.Count == 2 && !snapshot.Index.Stations.ContainsKey(123));
    Require(snapshot.Stations.Single(x => x.Kind == "Jumpgate").PlayerOwned == true);
    Require(snapshot.Stations.Single(x => x.Kind == "Station").PlayerOwned == false);
    Require(snapshot.Inventories.Single().StationRecord == a.Ref && snapshot.Inventories.Single().SectorId == 0);
    Require(snapshot.Connections.Single().To == 3 && snapshot.Routes.Find(0, 3)!.SequenceEqual(new[] { 0, 3 }));
    Require(snapshot.Routes.Find(3, 0) is null && snapshot.Routes.Find(0, 1) is null && snapshot.Routes.Find(0, 2) is null);
    var full = Project(SaveView.Create(graph, null, "Unknown", true));
    Require(full.Markets.Single().SectorId is null);
    Require(full.Connections.Count == 3 && full.Routes.Find(0, 2) is not null);
});
TradeQuote Quote(string location, double? buy = null, double? sell = null, bool verified = true, double? stock = 10, double? demand = 10,
    bool? access = true, bool? hostile = false, string ns = "commodity", string source = "Synthetic transaction") =>
    new(new(ns, 18), location, buy, sell, stock, demand, 2, verified, true, access, hostile, source);
Test("verified trade respects stock, credits, cargo and existing cargo", () => {
    var r = TradeAnalyzer.Analyze([Quote("A", buy: 10), Quote("B", sell: 20)], new(35, 10, 4, 8, 2, 3)).Single();
    Require(r.Quantity == 3 && r.GrossProfit == 30 && r.EstimatedNetProfit == 28 && r.ProfitPerCargoUnit == 5 && r.Executable);
});
Test("verified prices without grounded route costs cannot be called executable", () => {
    var r = TradeAnalyzer.Analyze([Quote("A", buy: 10), Quote("B", sell: 20)], new(100, 20, 0, 10)).Single();
    Require(r.Quantity == 10 && !r.Executable && r.EstimatedNetProfit is null);
});
Test("unverified quotes require opt-in and never become executable", () => {
    var quotes = new[] { Quote("A", buy: 10, verified: false), Quote("B", sell: 20, verified: false) };
    Require(TradeAnalyzer.Analyze(quotes, new(100, 20, 0, 10)).Count == 0);
    Require(!TradeAnalyzer.Analyze(quotes, new(100, 20, 0, 10), true).Single().Executable);
});
Test("unknown stock, cargo and travel do not become zero or executable", () => {
    var r = TradeAnalyzer.Analyze([Quote("A", buy: 10, stock: null), Quote("B", sell: 20)], new(100, null, null, null)).Single();
    Require(r.Quantity is null && r.GrossProfit is null && r.EstimatedNetProfit is null && r.TravelEffort is null && !r.Executable);
});
Test("insufficient credits, full cargo and empty inventories permit zero units", () => {
    foreach (var limits in new[] { new TradeConstraints(5, 10, 0, null), new TradeConstraints(100, 10, 10, null) }) {
        var r = TradeAnalyzer.Analyze([Quote("A", buy: 10), Quote("B", sell: 20)], limits).Single(); Require(r.Quantity == 0 && !r.Executable);
    }
    Require(TradeAnalyzer.Analyze([Quote("A", buy: 10, stock: 0), Quote("B", sell: 20)], new(100, 10, 0, null)).Single().Quantity == 0);
});
Test("hostility and unknown access block executable status", () => {
    foreach (var destination in new[] { Quote("B", sell: 20, hostile: true), Quote("B", sell: 20, access: null), Quote("B", sell: 20, hostile: null) }) {
        var r = TradeAnalyzer.Analyze([Quote("A", buy: 10), destination], new(100, 20, 0, 10)).Single(); Require(!r.Executable && r.Warnings.Count > 0);
    }
});
Test("missing and sentinel quotes, namespace collisions and incompatible sources never trade", () => {
    foreach (var origin in new[] { Quote("A", buy: null), Quote("A", buy: -1), Quote("A", buy: double.NaN), Quote("A", buy: 10, ns: "weapon"), Quote("A", buy: 10, source: "Codex") })
        Require(TradeAnalyzer.Analyze([origin, Quote("B", sell: 20)], new(100, 20, 0, 10)).Count == 0);
});
Test("advisor missing criteria unknown, transparent weights and no negative evidence", () => {
    var s = Project(Fixture()); var a = HomeAdvisor.Rank(s, new()).Single();
    Require(a.Score > 0 && a.EvidenceCoverage < 1 && a.Reasons.Single(r => r.Criterion == "Known connectivity").Score is null);
    Require(a.Reasons.Single(r => r.Criterion == "Equipment availability").Score is null);
    Require(HomeAdvisor.Rank(s, new(0, 0, 0, 0, 0)).Single().Score is null);
});
ProductionRecipe Recipe(string profile = "Galaxy Reforged", string version = "2.251") => new("synthetic-only", profile, version, "Synthetic test; not a game recipe",
    [new(new("commodity", 1), 3)], [new(new("commodity", 2), 2)]);
Test("industry catalog calculates known bottlenecks and leaves missing stock unknown", () => {
    var r = IndustryPlanner.Plan(new("Galaxy Reforged", "2.251", [Recipe()], [new("synthetic-only", 4)], [new(new("commodity", 1), 5)]));
    Require(r.Resources.Single(x => x.Item.Id == 1).Shortfall == 7 && r.Resources.Single(x => x.Item.Id == 2).Produced == 8);
    Require(r.Resources.Single(x => x.Item.Id == 2).Available is null && r.Resources.Single(x => x.Item.Id == 2).Shortfall is null);
});
Test("industry profile and version mismatch, duplicate resources and invalid quantities rejected", () => {
    foreach (var recipe in new[] { Recipe("Vanilla"), Recipe(version: "future"), Recipe() with { Inputs = [new(new("commodity", 1), -1)] }, Recipe() with { Inputs = [new(new("commodity", 1), 1), new(new("commodity", 1), 1)] } }) {
        try { IndustryPlanner.Plan(new("Galaxy Reforged", "2.251", [recipe], [], [])); throw new Exception("Accepted invalid catalog"); }
        catch (ArgumentException) { }
    }
});
Test("empty industry catalogs never fabricate recipes or stock", () => {
    var r = IndustryPlanner.Plan(new("Unknown", "future", [], [], [])); Require(r.Resources.Count == 0 && r.Warnings.Count > 0);
});
Test("large observed markets rank a bounded top set", () => {
    var quotes = Enumerable.Range(0, 2000).Select(i => Quote($"Sector {i}", buy: i + 1, sell: i + 2, verified: false, stock: null)).ToArray();
    var r = TradeAnalyzer.Analyze(quotes, new(1000, null, null, null), true);
    Require(r.Count == 100 && r[0].DifferencePerItem == 2000 && r.All(o => !o.Executable));
});
var failed = 0;
foreach (var (name, body) in tests) { try { body(); Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + e); } }
Console.WriteLine($"{tests.Count - failed}/{tests.Count} intelligence tests passed (synthetic only).");
return failed == 0 ? 0 : 1;
