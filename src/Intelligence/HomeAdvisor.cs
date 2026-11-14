namespace GalacticIntelligence;
public sealed record AdvisorWeights(double Stations = 1, double Markets = 1, double Equipment = 1, double Ships = 1, double Connectivity = 1);
public sealed record ScoreReason(string Criterion, double Weight, double? Score, string Reason);
public sealed record HomeCandidate(int SectorId, string? Name, double? Score, double EvidenceCoverage, IReadOnlyList<ScoreReason> Reasons);
public static class HomeAdvisor
{
    public static IReadOnlyList<HomeCandidate> Rank(GalaxySnapshot snapshot, AdvisorWeights weights)
    {
        var marketCounts = snapshot.Markets.Where(m => m.SectorId.HasValue).GroupBy(m => m.SectorId!.Value).ToDictionary(g => g.Key, g => g.Count());
        return snapshot.Sectors.Select(sector => {
            var stations = snapshot.Index.StationsBySector[sector.Id].Where(s => s.Kind != "Jumpgate").ToArray();
            var inventory = stations.Where(s => s.Evidence.GraphRecord.HasValue).SelectMany(s => snapshot.Index.InventoryByStationRecord[s.Evidence.GraphRecord!.Value]).ToArray();
            var reasons = new List<ScoreReason>();
            void Observed(string criterion, double weight, int count, int saturation, string reason) =>
                reasons.Add(new(criterion, double.IsFinite(weight) ? Math.Clamp(weight, 0, 10) : 0,
                    count > 0 ? Math.Min(100d, 100d * count / saturation) : null, count > 0 ? reason : "Unknown: no qualifying observations; absence is not evidence of no service."));
            Observed("Station presence", weights.Stations, stations.Length, 5, $"{stations.Length} station records; services unverified. 5 records saturate this preference.");
            Observed("Market evidence", weights.Markets, marketCounts.GetValueOrDefault(sector.Id), 10, "Observed price records; not a verified measure of turnover. 10 records saturate this preference.");
            Observed("Equipment availability", weights.Equipment, inventory.Where(i => i.Item.Namespace == "equipment" && i.Stock > 0).Select(i => i.Item.Id).Distinct().Count(), 5, "Distinct equipment IDs with positive saved stock; purchase access unverified. 5 IDs saturate.");
            Observed("Ship availability", weights.Ships, inventory.Where(i => i.Item.Namespace == "ship-model" && i.Stock > 0).Select(i => i.Item.Id).Distinct().Count(), 3, "Distinct ship models with positive saved stock. 3 models saturate.");
            Observed("Known connectivity", weights.Connectivity, snapshot.Routes.Reach(sector.Id, 2).Count(step => step.Jumps > 0), 4, "Sectors reachable within two saved gate jumps. 4 destinations saturate.");
            foreach (var unknown in new[] { "Technology level", "Friendly faction presence", "Neighbor commerce", "Resources and industrial potential", "Threat and faction access" })
                reasons.Add(new(unknown, 0, null, "Unknown: no validated interpretation or extraction."));
            var totalWeight = reasons.Sum(r => r.Weight);
            var knownWeight = reasons.Where(r => r.Score.HasValue).Sum(r => r.Weight);
            return new HomeCandidate(sector.Id, sector.Name,
                knownWeight > 0 ? reasons.Where(r => r.Score.HasValue).Sum(r => r.Score!.Value * r.Weight) / knownWeight : null,
                totalWeight > 0 ? knownWeight / totalWeight : 0, reasons);
        }).OrderByDescending(c => c.Score).ThenByDescending(c => c.EvidenceCoverage).ThenBy(c => c.SectorId).ToArray();
    }
}
