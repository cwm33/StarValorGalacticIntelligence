using System.Text.Json.Serialization;

namespace GalacticIntelligence;

// IDs always include their namespace; a weapon #18 is never a commodity #18.
public sealed record Identity(string Namespace, int Id, string? Name = null, string Confidence = "Unknown", string? NameSource = null);
public sealed record Evidence(string Source, int? GraphRecord, IReadOnlyDictionary<string, object?> Raw,
    string Confidence = "Extracted", string? Note = null);
public sealed record Provenance(string SnapshotId, DateTimeOffset ImportedAt, string? GameVersion,
    string MechanicsProfile, bool? BaseBuilding, bool IncludesSpoilers, string SectorIdentity = "Save-list index; cross-save stability unverified");
public sealed record Sector(int Id, string? Name, double? X, double? Y, int? Level, bool? Discovered,
    bool? Spotted, double? Population, Evidence Evidence);
public sealed record Station(int Id, int SectorId, Identity? Faction, int? ModelId, int? BuildingPlanId,
    bool? Discovered, int? Level, Evidence Evidence, string Kind = "Station", bool? PlayerOwned = null);
public sealed record FactionRelation(int From, int To, double? Value, Evidence Evidence);
public sealed record Inventory(Identity Item, int StationId, string Source, double? Stock, double? LastStock,
    int? Rarity, Evidence Evidence, int? StationRecord = null, int? SectorId = null);
public sealed record CargoItem(Identity Item, double? Quantity, int? Rarity, string Storage, Evidence Evidence);
public sealed record MarketObservation(Identity Commodity, string Source, int? SectorId, int? StationId,
    double? BuyingPrice, double? SellingPrice, double? TradePrice, double? CodexPrice,
    bool? BuyingOverstocked, bool? ResourceStarved, bool? Trading, bool? TradingOverstocked,
    Evidence Evidence, string PriceSemantics = "Unverified");
public sealed record Ship(Identity? Model, string Role, int? SectorId, Identity? Faction, Evidence Evidence);
public sealed record InstalledItem(Identity Item, string Kind, int? Slot, double? Quantity, int? Rarity, Evidence Evidence);
public sealed record Player(string? Name, double? Credits, int? Level, Identity? ShipModel, int? FleetCommanderBase = null);
public sealed record JumpConnection(int From, int To, string Confidence, Evidence Evidence);

public sealed record GalaxySnapshot(Provenance Provenance, Player Player, IReadOnlyList<Sector> Sectors,
    IReadOnlyList<Station> Stations, IReadOnlyList<MarketObservation> Markets, IReadOnlyList<Inventory> Inventories,
    IReadOnlyList<Ship> Ships, IReadOnlyList<InstalledItem> InstalledItems, IReadOnlyList<FactionRelation> Relations,
    IReadOnlyList<JumpConnection> Connections, IReadOnlyList<string> Warnings)
{
    public IReadOnlyList<CargoItem> Cargo { get; init; } = [];
    [JsonIgnore] public GateRoutes Routes { get; } = new(Sectors, Connections);

    // Indexes are built once per projection and never exposed as duplicate JSON payloads.
    [JsonIgnore] public GalaxyIndex Index { get; } = new(Sectors, Stations, Markets, Inventories);
}

public sealed class GalaxyIndex
{
    public IReadOnlyDictionary<int, Sector> Sectors { get; }
    public IReadOnlyDictionary<int, Station> Stations { get; }
    public ILookup<int, Station> StationsBySector { get; }
    public ILookup<int, Inventory> InventoryByStationRecord { get; }
    public ILookup<(string Namespace, int Id), MarketObservation> MarketsByCommodity { get; }
    public GalaxyIndex(IReadOnlyList<Sector> sectors, IReadOnlyList<Station> stations,
        IReadOnlyList<MarketObservation> markets, IReadOnlyList<Inventory> inventories)
    {
        // Ambiguous identifiers are excluded from joins rather than silently picking a record.
        Sectors = sectors.GroupBy(s => s.Id).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());
        Stations = stations.Where(s => s.Id >= 0).GroupBy(s => s.Id).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());
        StationsBySector = stations.ToLookup(s => s.SectorId);
        InventoryByStationRecord = inventories.Where(s => s.StationRecord.HasValue).ToLookup(s => s.StationRecord!.Value);
        MarketsByCommodity = markets.ToLookup(m => (m.Commodity.Namespace, m.Commodity.Id));
    }
}
