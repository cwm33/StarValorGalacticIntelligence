namespace GalacticIntelligence;

// Transaction quotes are an extension contract. The save adapter currently emits NO verified quotes.
public sealed record TradeQuote(Identity Commodity, string Location, double? PlayerBuyPrice, double? PlayerSellPrice,
    double? AvailableStock, double? DemandLimit, double? CargoUnitsPerItem, bool Verified, bool Fresh,
    bool? Accessible, bool? Hostile, string Source);
public sealed record TradeConstraints(double? Credits, double? CargoCapacity, double? ExistingCargo, double? MaximumQuantity,
    double? RouteCost = null, double? TravelEffort = null);
public sealed record TradeOpportunity(Identity Commodity, string BuyAt, string SellAt, double CostPerItem, double ProceedsPerItem,
    double DifferencePerItem, double? Quantity, double? GrossProfit, double? EstimatedNetProfit, double? ProfitPerCargoUnit,
    double? TravelEffort, bool Executable, IReadOnlyList<string> Warnings);
public static class TradeAnalyzer
{
    private static bool Valid(double? n) => n.HasValue && double.IsFinite(n.Value) && n >= 0;
    public static IReadOnlyList<TradeOpportunity> Analyze(IReadOnlyList<TradeQuote> quotes, TradeConstraints limits,
        bool includeExperimental = false, string rankBy = "difference", int take = 100)
    {
        var capacity = Math.Clamp(take, 1, 500);
        var results = new PriorityQueue<TradeOpportunity, double>();
        double Rank(TradeOpportunity r) => (rankBy switch { "gross" => r.GrossProfit, "net" => r.EstimatedNetProfit,
            "cargo" => r.ProfitPerCargoUnit, _ => r.DifferencePerItem }) ?? double.NegativeInfinity;
        foreach (var group in quotes.GroupBy(q => (q.Commodity.Namespace, q.Commodity.Id)))
        {
            if (group.Key.Namespace != "commodity") continue;
            var buys = group.Where(q => Valid(q.PlayerBuyPrice) && (q.Verified || includeExperimental));
            var sells = group.Where(q => Valid(q.PlayerSellPrice) && (q.Verified || includeExperimental));
            foreach (var buy in buys)
            foreach (var sell in sells)
            {
                if (buy.Location == sell.Location || buy.Source != sell.Source || sell.PlayerSellPrice <= buy.PlayerBuyPrice) continue;
                var warnings = new List<string>();
                if (!buy.Verified || !sell.Verified) warnings.Add("Experimental price direction; not an executable or guaranteed profit.");
                if (!buy.Fresh || !sell.Fresh) warnings.Add("Observation freshness unknown or stale.");
                if (buy.Hostile == true || sell.Hostile == true) warnings.Add("Hostile destination or origin.");
                if (buy.Hostile is null || sell.Hostile is null) warnings.Add("Hostility unknown.");
                if (buy.Accessible != true || sell.Accessible != true) warnings.Add("Faction access blocked or unknown.");
                var knownLimits = new List<double>();
                bool quantityKnown = true;
                void Limit(double? n, string missing) { if (Valid(n)) knownLimits.Add(Math.Floor(n!.Value)); else { quantityKnown = false; warnings.Add(missing); } }
                Limit(buy.AvailableStock, "Available stock unknown; station inventories are not assumed to be offers.");
                Limit(sell.DemandLimit, "Destination demand quantity unknown.");
                if (Valid(limits.Credits) && buy.PlayerBuyPrice > 0) knownLimits.Add(Math.Floor(limits.Credits!.Value / buy.PlayerBuyPrice!.Value));
                else if (!Valid(limits.Credits)) { quantityKnown = false; warnings.Add("Available credits unknown."); }
                var volume = buy.CargoUnitsPerItem;
                if (volume > 0 && Valid(volume) && Valid(limits.CargoCapacity) && Valid(limits.ExistingCargo))
                    knownLimits.Add(Math.Floor(Math.Max(0, limits.CargoCapacity!.Value - limits.ExistingCargo!.Value) / volume.Value));
                else { quantityKnown = false; warnings.Add("Cargo capacity, existing cargo or commodity volume unknown."); }
                if (limits.MaximumQuantity is not null) Limit(limits.MaximumQuantity, "Invalid requested quantity.");
                double? quantity = quantityKnown && knownLimits.Count > 0 ? knownLimits.Min() : null;
                // A known zero constraint proves inability to trade even if other limits are missing.
                if (knownLimits.Contains(0)) quantity = 0;
                if (quantity == 0) warnings.Add("Known constraints permit no units.");
                var difference = sell.PlayerSellPrice!.Value - buy.PlayerBuyPrice!.Value;
                var gross = quantity * difference;
                var net = Valid(limits.RouteCost) ? gross - limits.RouteCost : null;
                if (!Valid(limits.RouteCost)) warnings.Add("Route cost unknown; net profit is unknown.");
                if (!Valid(limits.TravelEffort)) warnings.Add("Travel effort unknown; no proximity route assumed.");
                var executable = buy.Verified && sell.Verified && buy.Fresh && sell.Fresh && buy.Accessible == true && sell.Accessible == true &&
                    buy.Hostile == false && sell.Hostile == false && quantity > 0;
                var result = new TradeOpportunity(buy.Commodity, buy.Location, sell.Location, buy.PlayerBuyPrice.Value, sell.PlayerSellPrice.Value,
                    difference, quantity, gross, net, volume > 0 && Valid(volume) ? difference / volume : null,
                    Valid(limits.TravelEffort) ? limits.TravelEffort : null, executable, warnings);
                results.Enqueue(result, Rank(result));
                if (results.Count > capacity) results.Dequeue();
            }
        }
        return results.UnorderedItems.Select(i => i.Element).OrderByDescending(Rank)
            .ThenBy(r => r.BuyAt).ThenBy(r => r.SellAt).ToArray();
    }
}
