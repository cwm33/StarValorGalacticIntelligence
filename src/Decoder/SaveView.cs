namespace Decoder;

public sealed class SaveView
{
    public Dictionary<string, object?> Metadata { get; } = new();
    public Dictionary<string, List<Dictionary<string, object?>>> Tables { get; } = new();
    public List<string> Warnings { get; } = new();
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyDictionary<int, string> WeaponNames { get; private set; } = new Dictionary<int, string>();
    public int HiddenSectors { get; private set; }

    public static SaveView Create(Graph graph, Graph? info, string mechanics, bool spoilers)
    {
        var result = new SaveView();
        var root = graph.Records[graph.Root - 1];
        var summary = info?.Records[info.Root - 1];
        object? Scalar(Graph source, object? value) =>
            value is Link link && source.Resolve(link) is { Kind: "primitive" } primitive ? primitive.Value : value;
        object? Field(Node? node, string name) => Scalar(graph, node?.Fields?.GetValueOrDefault(name));
        Node? Child(Node? node, string name) => Field(node, name) is Link link ? graph.Resolve(link) : null;
        IEnumerable<Node?> Slots(Node? node)
        {
            var items = node?.Items ?? Child(node, "_items")?.Items;
            if (items is null) yield break;
            var size = node?.Items is not null ? items.Length : Field(node, "_size") is int count ? count : -1;
            if (size < 0 || size > items.Length)
                throw new InvalidDataException("Invalid serialized list size.");
            for (var i = 0; i < size; i++)
                yield return items[i] is Link link ? graph.Resolve(link) : null;
        }
        IEnumerable<Node> List(Node? node) => Slots(node).OfType<Node>();
        void Add(string table, Node node, string fields, params (string Key, object? Value)[] context)
        {
            if (!result.Tables.TryGetValue(table, out var rows))
                result.Tables.Add(table, rows = new());
            var row = new Dictionary<string, object?> { ["record"] = node.Id, ["type"] = node.Type.Split(',')[0] };
            foreach (var (key, value) in context) row[key] = value;
            foreach (var field in fields.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (node.Fields?.ContainsKey(field) == true && Field(node, field) is not Link)
                    row[field] = Field(node, field);
            rows.Add(row);
        }
        result.Metadata["mechanics"] = mechanics;
        if (summary?.Type.StartsWith("SaveFileInfo,") == true)
        {
            result.Metadata["info"] = summary.Fields!.Select(x => (x.Key, Value: Scalar(info!, x.Value)))
                .Where(x => x.Value is not Link).ToDictionary(x => x.Key, x => x.Value);
            if (!Equals(Field(root, "totalTimePlayed"), Field(summary, "timePlayed")) ||
                !Equals(Field(root, "credits"), Field(summary, "credits")))
                result.Warnings.Add("The companion Info file reports different play time or credits. Main-save data takes precedence.");
        }
        else if (summary is not null)
            result.Warnings.Add("Unrecognized companion Info layout. Inspect its raw records in Schema.");
        if (!root.Type.StartsWith("GameDataInfo,") || root.Fields?.ContainsKey("sectors") != true)
        {
            result.Warnings.Add("Unrecognized main save layout. Raw inspection remains available.");
            return result;
        }
        var character = Child(root, "character");
        var playerShip = Child(root, "spaceShipData");
        var version = Field(root, "savedVersion");
        result.Metadata["player"] = Field(character, "name");
        result.Metadata["level"] = Field(character, "level");
        result.Metadata["credits"] = Field(root, "credits");
        result.Metadata["shipModelID"] = Field(playerShip, "shipModelID");
        result.Metadata["savedVersion"] = version;
        result.Metadata["baseBuilding"] = Field(root, "expansion_BaseBuilding");
        result.Metadata["marketLayout"] = root.Fields.ContainsKey("galacticMarket") ? "Sector prices and station stock" : "Legacy codex prices and station market";
        if (System.Convert.ToString(version, System.Globalization.CultureInfo.InvariantCulture) is not ("2.05" or "2.251"))
            result.Warnings.Add("This game version has not been validated. Inspect the raw records if fields are missing.");
        result.Metadata["fleetCommander"] = Field(character, "fleetCommander");
        if (playerShip is not null)
        {
            Add("Player ship", playerShip, "shipModelID HPstatus energyStatus shieldStatus");
            foreach (var item in List(Child(playerShip, "cargo")))
                Add("Player cargo", item, "itemType itemID rarity qnt shipLoadoutID stockStationID",
                    ("storageKind", Field(item, "stockStationID") switch
                    {
                        -1 => "Aboard",
                        int id when id >= 0 => "Station storage",
                        int => "Other storage",
                        _ => "Unknown"
                    }));
            foreach (var weapon in List(Child(playerShip, "weapons")))
                Add("Installed weapons", weapon, "weaponIndex slotIndex rarity");
            foreach (var equipment in List(Child(playerShip, "equipments")))
                Add("Installed equipment", equipment, "equipmentID rarity qnt");
        }

        foreach (var member in List(Child(character, "mercenaries")).DistinctBy(node => node.Id))
            if (member.Type.StartsWith("PlayerFleetMember,", StringComparison.Ordinal) && Child(member, "shipData") is { } ship)
                Add("Player fleet", ship, "shipModelID HPstatus energyStatus shieldStatus",
                    ("memberRecord", member.Id), ("crewMemberID", Field(member, "crewMemberID")), ("hangarDocked", Field(member, "hangarDocked")));

        var weapons = Child(root, "weaponList");
        var weaponItems = Child(weapons, "_items")?.Items;
        if (weaponItems is not null && Field(weapons, "_size") is int weaponCount &&
            weaponCount >= 0 && weaponCount <= weaponItems.Length)
        {
            var names = new Dictionary<int, string>();
            for (var index = 0; index < weaponCount; index++)
            {
                if (weaponItems[index] is not Link link) continue;
                var weapon = graph.Resolve(link);
                if (weapon.Type.StartsWith("TWeapon,", StringComparison.Ordinal) &&
                    Field(weapon, "name") is string name && !string.IsNullOrWhiteSpace(name))
                    names.Add(index, name);
            }
            result.WeaponNames = names;
        }
        var visible = new HashSet<int>();
        var sectors = Slots(Child(root, "sectors")).ToArray();
        for (var index = 0; index < sectors.Length; index++)
        {
            if (sectors[index] is not { } sector) continue;
            if (Field(sector, "discovered") is not true)
            {
                result.HiddenSectors++;
                if (!spoilers) continue;
            }
            visible.Add(index);
            Add("Sectors", sector, "name x y incX incY level discovered spotted population", ("sectorIndex", index));
            foreach (var price in List(Child(Child(sector, "marketPriceControl"), "prices")))
                Add("Sector prices", price, "itemID buyingPrice sellingPrice tradePrice IsBuyingOverstocked IsResourceStarved IsTrading IsTradingOverstocked", ("sectorIndex", index));
            foreach (var actor in List(Child(sector, "aiChars")))
                if (Child(actor, "shipData") is { } ship)
                    Add("Sector ships", ship, "shipModelID", ("sectorIndex", index), ("factionIndex", Field(actor, "factionIndex")));
        }
        if (Field(root, "currentSectorIndex") is int current && visible.Contains(current))
            result.Metadata["currentSectorIndex"] = current;

        foreach (var index in visible.Order())
            foreach (var gate in List(Child(sectors[index], "jumpGates")).DistinctBy(gate => gate.Id))
            {
                if (!gate.Type.StartsWith("JumpGate,", StringComparison.Ordinal) ||
                    (!spoilers && (Field(gate, "discovered") is not true ||
                        Field(gate, "connectedSector") is not int destination || !visible.Contains(destination))))
                    continue;
                Add("Generated jumpgates", gate, "x y yRotation discovered connectedSector", ("sectorIndex", index));
            }

        var registered = List(Child(root, "stationList")).DistinctBy(node => node.Id).ToDictionary(node => node.Id);
        var local = sectors.SelectMany(sector => List(Child(sector, "smallBases"))).DistinctBy(node => node.Id).ToDictionary(node => node.Id);
        var stations = registered.Values.Concat(local.Values).DistinctBy(node => node.Id).ToArray();
        // Codex has only a game ID. Check ambiguity before applying discovery filtering.
        var uniqueIds = stations.Select(station => Field(station, "id")).OfType<int>()
            .Where(id => id >= 0).GroupBy(id => id).Where(group => group.Count() == 1).Select(group => group.Key).ToHashSet();
        var stationIds = new HashSet<int>();
        foreach (var station in stations)
        {
            if (!spoilers && (Field(station, "discovered") is not true ||
                Field(station, "sectorIndex") is not int sectorIndex || !visible.Contains(sectorIndex))) continue;
            var id = Field(station, "id");
            if (id is int number && uniqueIds.Contains(number)) stationIds.Add(number);
            Add("Stations", station, "id factionIndex modelIndex nameIndex sectorIndex discovered level buildingPlanID buildingState ownerID state",
                ("inStationList", registered.ContainsKey(station.Id)), ("inSmallBases", local.ContainsKey(station.Id)));
            foreach (var item in List(Child(station, "market")))
                Add("Station stock", item, "itemType itemID rarity stock lastStockCount", ("stationID", id), ("stationRecord", station.Id), ("sourceField", "market"));
            foreach (var item in List(Child(Child(station, "itemStock"), "items")))
                Add("Station stock", item, "itemType itemID rarity stock lastStockCount", ("stationID", id), ("stationRecord", station.Id), ("sourceField", "itemStock.items"));
            foreach (var module in List(Child(station, "modules")))
                foreach (var item in List(Child(module, "persistentItems")))
                    Add("Station market modules", item, "itemType itemID rarity stock lastStockCount", ("stationID", id), ("stationRecord", station.Id), ("sourceField", "modules.persistentItems"), ("moduleRecord", module.Id));
        }
        foreach (var market in List(Child(Child(character, "codex"), "records")))
        {
            var station = Field(market, "stationID");
            if (!spoilers && (station is not int number || !stationIds.Contains(number))) continue;
            foreach (var item in List(Child(market, "items")))
                Add("Codex prices", item, "itemID price", ("stationID", station), ("uniqueStationID", station is int id && uniqueIds.Contains(id)));
        }
        foreach (var faction in List(Child(Child(root, "relations"), "relation")))
            Add("Faction relations", faction, "ID1 ID2 value");
        foreach (var faction in List(Child(root, "factionsData")))
            Add("Factions", faction, "attackPower attackPowerHighLevel");
        return result;
    }
}
