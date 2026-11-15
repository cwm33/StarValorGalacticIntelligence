namespace GalacticIntelligence;
public sealed record AdvisorWeights(double Stations = 1, double Markets = 1, double Equipment = 1, double Ships = 1, double Connectivity = 1, double Solar = 0, double Minerals = 0, double Mining = 0, double Industry = 0)
{
    public static AdvisorWeights Preset(string preset) => preset switch
    {
        "industrial" => new(1, 2, 1, 0, 2, 3, 3, 1, 3),
        "mining" => new(0, 1, 0, 0, 2, 0, 3, 4, 1),
        _ => new(2, 4, 2, 1, 3)
    };
}
public sealed record ScoreReason(string Criterion, double Weight, double? Score, string Reason);
public sealed record HomeCandidate(int SectorId, string? Name, double? Score, double EvidenceCoverage, IReadOnlyList<ScoreReason> Reasons);
public static class HomeAdvisor
{
    public static IReadOnlyList<HomeCandidate> Rank(GalaxySnapshot snapshot, AdvisorWeights weights)
    {
        var marketCounts = snapshot.Markets.Where(m => m.SectorId.HasValue).GroupBy(m => m.SectorId!.Value).ToDictionary(g => g.Key, g => g.Count());
        return snapshot.Sectors.Select(sector => {
            var stations = snapshot.Index.StationsBySector[sector.Id].Where(StationSemantics.HasEconomicPurpose).ToArray();
            var inventory = stations.Where(s => s.Evidence.GraphRecord.HasValue).SelectMany(s => snapshot.Index.InventoryByStationRecord[s.Evidence.GraphRecord!.Value]).ToArray();
            var reasons = new List<ScoreReason>();
            void Observed(string criterion, double weight, int count, int saturation, string reason) =>
                reasons.Add(new(criterion, double.IsFinite(weight) ? Math.Clamp(weight, 0, 10) : 0,
                    count > 0 ? Math.Min(100d, 100d * count / saturation) : null, count > 0 ? reason : "Unknown: no qualifying observations; absence is not evidence of no service."));
            Observed("Station presence", weights.Stations, stations.Length, 5, $"{stations.Length} economic structure records; access and operating state unverified. 5 records saturate this preference.");
            Observed("Market evidence", weights.Markets, marketCounts.GetValueOrDefault(sector.Id), 10, "Observed price records; not a verified measure of turnover. 10 records saturate this preference.");
            Observed("Equipment availability", weights.Equipment, inventory.Where(i => i.Item.Namespace == "equipment" && i.Stock > 0).Select(i => i.Item.Id).Distinct().Count(), 5, "Distinct equipment IDs with positive saved stock; purchase access unverified. 5 IDs saturate.");
            Observed("Ship availability", weights.Ships, inventory.Where(i => i.Item.Namespace == "ship-model" && i.Stock > 0).Select(i => i.Item.Id).Distinct().Count(), 3, "Distinct ship models with positive saved stock. 3 models saturate.");
            Observed("Known connectivity", weights.Connectivity, snapshot.Routes.Reach(sector.Id, 2).Count(step => step.Jumps > 0), 4, "Sectors reachable within two saved gate jumps. 4 destinations saturate.");
            void Metric(string criterion, double weight, double? value, string? source) =>
                reasons.Add(new(criterion, double.IsFinite(weight) ? Math.Clamp(weight, 0, 10) : 0,
                    value is double n && double.IsFinite(n) && n >= 0 && n <= 100 && !string.IsNullOrWhiteSpace(source) ? n : null,
                    source ?? "Unknown: no validated field mapping; excluded from score."));
            Metric("Solar output", weights.Solar, sector.Industry?.Suitability?.Solar, sector.Industry?.Suitability?.NormalizationSource);
            Metric("Mineral richness", weights.Minerals, sector.Industry?.Suitability?.Minerals, sector.Industry?.Suitability?.NormalizationSource);
            Metric("Mining potential", weights.Mining, sector.Industry?.Suitability?.Mining, sector.Industry?.Suitability?.NormalizationSource);
            Observed("Industrial infrastructure", weights.Industry, stations.Count(s => s.Kind is "Industrial" or "Workshop"), 3, "Industrial or workshop subtypes; production and operation unverified.");
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
