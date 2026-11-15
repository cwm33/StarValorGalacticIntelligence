namespace GalacticIntelligence;

public sealed record NearbyPrice(Identity Commodity, int SectorId, double Price, int Jumps);
public sealed record NearbyTrade(NearbyPrice Buy, NearbyPrice Sell, int JumpsToSell)
{
    public double Spread => Sell.Price - Buy.Price;
    public int TotalJumps => Buy.Jumps + JumpsToSell;
}

public static class NearbyMarkets
{
    public static (NearbyPrice[] Buys, NearbyPrice[] Sells, NearbyTrade[] Trades) Find(
        GalaxySnapshot snapshot, int maxJumps, string? query, double? driveRange = null)
    {
        if (snapshot.Player.CurrentSectorId is not int origin) return ([], [], []);
        var routes = driveRange > 0 ? new GateRoutes(snapshot.Sectors, snapshot.Connections, driveRange.Value) : snapshot.Routes;
        var distance = routes.Reach(origin, maxJumps).ToDictionary(step => step.Sector, step => step.Jumps);
        var markets = snapshot.Markets.Where(m => m.Source == "Sector prices" && m.PriceSemantics != "Unverified" &&
            m.SectorId is int sector && distance.ContainsKey(sector) &&
            (string.IsNullOrWhiteSpace(query) || m.Commodity.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
                m.Commodity.Id.ToString() == query.Trim())).ToArray();

        IEnumerable<NearbyPrice> Prices(Func<MarketObservation, double?> price) => markets
            .Where(m => price(m) is double value && double.IsFinite(value) && value >= 0)
            .Select(m => new NearbyPrice(m.Commodity, m.SectorId!.Value, price(m)!.Value, distance[m.SectorId.Value]));

        var buys = Prices(m => m.SellingPrice).ToArray();
        var sells = Prices(m => m.BuyingPrice).ToArray();
        var buyers = sells.ToLookup(s => s.Commodity.Id);
        var legs = new Dictionary<int, Dictionary<int, int>>();
        var trades = new Dictionary<int, NearbyTrade>();
        double Score(NearbyTrade trade) => trade.Spread / Math.Max(1, trade.TotalJumps);
        foreach (var buy in buys)
        {
            if (!legs.TryGetValue(buy.SectorId, out var reachable))
            {
                reachable = routes.Reach(buy.SectorId, maxJumps - buy.Jumps)
                    .ToDictionary(step => step.Sector, step => step.Jumps);
                legs.Add(buy.SectorId, reachable);
            }
            foreach (var sell in buyers[buy.Commodity.Id])
                if (sell.SectorId != buy.SectorId && sell.Price > buy.Price && reachable.TryGetValue(sell.SectorId, out var jumps))
                {
                    var trade = new NearbyTrade(buy, sell, jumps);
                    if (!trades.TryGetValue(buy.Commodity.Id, out var best) || Score(trade) > Score(best) ||
                        (Score(trade) == Score(best) && trade.TotalJumps < best.TotalJumps))
                        trades[buy.Commodity.Id] = trade;
                }
        }

        return (
            buys.GroupBy(b => b.Commodity.Id).Select(g => g.OrderBy(b => b.Price).ThenBy(b => b.Jumps).First())
                .OrderBy(b => b.Jumps).ThenBy(b => b.Commodity.Id).Take(30).ToArray(),
            sells.GroupBy(s => s.Commodity.Id).Select(g => g.OrderByDescending(s => s.Price).ThenBy(s => s.Jumps).First())
                .OrderBy(s => s.Jumps).ThenBy(s => s.Commodity.Id).Take(30).ToArray(),
            trades.Values.OrderByDescending(Score).ThenByDescending(t => t.Spread)
                .ThenBy(t => t.TotalJumps).Take(30).ToArray());
    }
}
