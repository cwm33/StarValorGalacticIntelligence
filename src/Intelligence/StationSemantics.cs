namespace GalacticIntelligence;

public static class StationSemantics
{
    // Serialized subtype is evidence of structure purpose, not faction access or operating state.
    public static string Classify(string? type) => type switch
    {
        "Station" => "Commercial",
        "IndustryStation" or "IndustrialStation" or "FactoryStation" => "Industrial",
        "Workshop" or "WorkshopStation" => "Workshop",
        "HideoutStation" => "Hideout",
        "TurretStation" or "Turret" => "Turret",
        "JumpgateStation" => "Jumpgate",
        "PlayerStation" => "Player-built installation",
        null => "Unknown",
        _ => "Specialized"
    };
    public static bool HasEconomicPurpose(Station station) => station.Kind is ("Commercial" or "Industrial" or "Workshop") && station.OperationState is not ("Wrecked" or "Destroyed");
}

// Values must be supplied by an independently validated adapter. No guessed serialized mapping.
public sealed record IndustrialMetrics(double? SolarOutput = null, double? MineralRichness = null,
    double? ResourceDeposits = null, double? MiningPotential = null, string? Environment = null,
    string? Source = null, string Confidence = "Unknown", IndustrialSuitability? Suitability = null, int? ObservedAsteroidFields = null);

// An adapter must document how raw metrics and units become normalized [0,100] scores.
// These values are never inferred by clamping raw solar output or resource quantities.
public sealed record IndustrialSuitability(double? Solar = null, double? Minerals = null,
    double? Mining = null, string? NormalizationSource = null);
