namespace GalacticIntelligence;

public sealed record ItemPrice(string Namespace, int Id, double BaseValue,
    IReadOnlyDictionary<int, double> RarityMultipliers, double? ReplacementMultiplier = null,
    double? LiquidationMultiplier = null);
public sealed record PricingCatalog(string GameVersion, string MechanicsProfile, string Source,
    string Confidence, IReadOnlyList<ItemPrice> Items, int SchemaVersion = 1, string? SaveSha256 = null);
public sealed record ArsenalItem(Identity Item, int? Rarity, string Storage, string Scope, bool Fitted,
    double? Quantity, int Records, IReadOnlyList<Evidence> Evidence, double? BaseValue = null,
    double? ReplacementPerItem = null, double? LiquidationPerItem = null,
    double? ReplacementValue = null, double? LiquidationValue = null,
    double? RarityValue = null, double? RarityTotal = null,
    string PriceConfidence = "Unknown", string? PriceSource = null);
public sealed record ArsenalReport(IReadOnlyList<ArsenalItem> Items, int DistinctModels,
    double? KnownQuantity, int UnknownQuantityRows, int ReplacementCoverage, int LiquidationCoverage,
    double? KnownReplacementValue, double? KnownLiquidationValue, string TotalsScope = "Player items only; known priced subset; station market observations excluded")
{
    private IEnumerable<ArsenalItem> StoredGear => Items.Where(i =>
        i.Scope == "Player" && !i.Fitted && i.Item.Namespace is ("weapon" or "equipment"));
    public int PricedGearGroups => StoredGear.Count(i => i.RarityTotal.HasValue);
    public int StoredGearGroups => StoredGear.Count();
    public double? StoredGearBaseValue
    {
        get
        {
            if (PricedGearGroups == 0) return null;
            var total = StoredGear.Sum(i => i.RarityTotal ?? 0);
            return double.IsFinite(total) ? total : null;
        }
    }
    public int PlayerGroups => Items.Count(i => i.Scope == "Player");
    public int MissingReplacementValues => PlayerGroups - ReplacementCoverage;
    public int MissingLiquidationValues => PlayerGroups - LiquidationCoverage;
}

public static class Arsenal
{
    public static ArsenalReport Build(GalaxySnapshot snapshot, PricingCatalog? catalog = null)
    {
        if (catalog is not null) Validate(catalog, snapshot.Provenance);
        var prices = catalog?.Items.ToDictionary(p => (p.Namespace, p.Id));
        var entries = new List<ArsenalItem>();
        foreach (var c in snapshot.Cargo)
        {
            var station = c.Evidence.Raw.GetValueOrDefault("stockStationID");
            entries.Add(new(c.Item, c.Rarity, c.Storage + (station is int id && id >= 0 ? $" #{id}" : ""), "Player", false,
                ValidQuantity(c.Quantity), 1, [c.Evidence]));
        }
        foreach (var i in snapshot.InstalledItems)
            entries.Add(new(i.Item, i.Rarity, "Current ship", "Player", true,
                // Each installed weapon record describes one slot; equipment carries its own quantity.
                i.Kind == "weapon" ? 1 : ValidQuantity(i.Quantity), 1, [i.Evidence]));
        foreach (var i in snapshot.Inventories)
            entries.Add(new(i.Item, i.Rarity, $"Station #{i.StationId} / record {i.StationRecord}: {i.Source}", "Market observation", false,
                ValidQuantity(i.Stock), 1, [i.Evidence]));
        // The same serialized object can be exposed by multiple projections. Count it once per scope.
        var seen = new HashSet<(string Scope, int Record)>();
        entries = entries.Where(e => e.Evidence[0].GraphRecord is not int r || seen.Add((e.Scope, r))).ToList();
        var rows = entries.GroupBy(e => (e.Item.Namespace, e.Item.Id, e.Rarity, e.Storage, e.Scope, e.Fitted))
            .Select(g => {
                var row = g.First() with { Quantity = g.All(e => e.Quantity.HasValue) ? Finite(g.Sum(e => e.Quantity!.Value)) : null,
                    Records = g.Count(), Evidence = g.SelectMany(e => e.Evidence).ToArray() };
                var price = prices?.GetValueOrDefault((row.Item.Namespace, row.Item.Id));
                if (price is null) return row;
                // Rarity is independent of modifications. No implicit rarity or sell multiplier.
                double? factor = row.Rarity is int rarity && price.RarityMultipliers.TryGetValue(rarity, out var f) ? f : null;
                var rarityValue = Finite(factor * price.BaseValue);
                var replacement = Finite(rarityValue * price.ReplacementMultiplier);
                var liquidation = Finite(factor * price.BaseValue * price.LiquidationMultiplier);
                return row with { BaseValue = price.BaseValue, RarityValue = rarityValue, RarityTotal = Finite(rarityValue * row.Quantity), ReplacementPerItem = replacement, LiquidationPerItem = liquidation,
                    ReplacementValue = Finite(replacement * row.Quantity), LiquidationValue = Finite(liquidation * row.Quantity),
                    PriceConfidence = catalog!.Confidence, PriceSource = catalog.Source };
            }).OrderBy(e => e.Item.Namespace).ThenBy(e => e.Item.Id).ThenBy(e => e.Rarity).ThenBy(e => e.Storage).ToArray();
        var owned = rows.Where(r => r.Scope == "Player").ToArray();
        return new(rows, owned.Select(r => (r.Item.Namespace, r.Item.Id)).Distinct().Count(),
            Finite(owned.Sum(r => r.Quantity ?? 0)), owned.Count(r => r.Quantity is null),
            owned.Count(r => r.ReplacementValue.HasValue), owned.Count(r => r.LiquidationValue.HasValue),
            owned.Any(r => r.ReplacementValue.HasValue) ? Finite(owned.Sum(r => r.ReplacementValue ?? 0)) : null,
            owned.Any(r => r.LiquidationValue.HasValue) ? Finite(owned.Sum(r => r.LiquidationValue ?? 0)) : null);
    }
    static double? Finite(double? n) => n is double v && double.IsFinite(v) ? v : null;
    static double? ValidQuantity(double? n) => n is double v && double.IsFinite(v) && v >= 0 ? v : null;
    static void Validate(PricingCatalog c, Provenance p)
    {
        bool Valid(double n) => double.IsFinite(n) && n >= 0;
        if (c.SchemaVersion != 1 || c.GameVersion != p.GameVersion || c.MechanicsProfile != p.MechanicsProfile ||
            string.IsNullOrWhiteSpace(c.Source) || string.IsNullOrWhiteSpace(c.Confidence) || c.Items is null ||
            c.Items.Any(i => i is null) ||
            c.Items.GroupBy(i => (i.Namespace, i.Id)).Any(g => g.Count() > 1) ||
            c.Items.Any(i => string.IsNullOrWhiteSpace(i.Namespace) || !Valid(i.BaseValue) || i.RarityMultipliers is null ||
                i.RarityMultipliers.Values.Any(v => !Valid(v)) ||
                i.ReplacementMultiplier is double r && !Valid(r) || i.LiquidationMultiplier is double l && !Valid(l)))
            throw new ArgumentException("Pricing catalog requires matching version/profile, unique namespaced IDs, provenance and finite nonnegative values.");
    }
}
