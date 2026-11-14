namespace GalacticIntelligence.GameData;

/// <summary>Extensible namespace; never derive it from a localization section or itemType.</summary>
public sealed record IdentifierCategory
{
    public string Value { get; }
    public IdentifierCategory(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim().ToLowerInvariant();
    }
    public override string ToString() => Value;
    public static IdentifierCategory Commodity { get; } = new("commodity");
    public static IdentifierCategory Weapon { get; } = new("weapon");
    public static IdentifierCategory Equipment { get; } = new("equipment");
    public static IdentifierCategory ShipModel { get; } = new("ship-model");
    public static IdentifierCategory StationModel { get; } = new("station-model");
}

public enum SourceKind { VanillaLocalization, ModOverride, UserOverride }
public enum ResolutionStatus { Resolved, Unresolved }
/// <summary>CallerValidated means the caller explicitly attested the mapping; the library cannot verify game semantics.</summary>
public enum ResolutionConfidence { Unknown, Unverified, CallerValidated }
public enum DiagnosticSeverity { Warning, Error }

public sealed record ResolverDiagnostic(
    string Code, DiagnosticSeverity Severity, string Message,
    string? Source = null, int? LineNumber = null,
    IdentifierCategory? Category = null, int? Id = null, int? Section = null);

public sealed record LocalizationSource(
    SourceKind Kind, string Path, int? Section = null, int? LineNumber = null);

public sealed record IdentifierResolution(
    int RawId, IdentifierCategory Category, string? Name,
    LocalizationSource? Source, ResolutionStatus Status,
    ResolutionConfidence Confidence, string? DiagnosticMessage = null)
{
    /// <summary>Display-only fallback. Name stays null for unresolved identifiers.</summary>
    public string DisplayName => Name ?? $"{Category}:{RawId}";
}

public interface IIdentifierResolver
{
    IdentifierResolution Resolve(IdentifierCategory category, int id);
    /// <summary>All winning mappings with this name in this category; ordinal case-insensitive, trimmed, no fuzzy matching.</summary>
    IReadOnlyList<IdentifierResolution> FindByName(IdentifierCategory category, string name);
    IReadOnlyList<ResolverDiagnostic> Diagnostics { get; }
}

public static class IdentifierResolverExtensions
{
    public static IdentifierResolution ResolveCommodity(this IIdentifierResolver resolver, int id) => resolver.Resolve(IdentifierCategory.Commodity, id);
    public static IdentifierResolution ResolveWeapon(this IIdentifierResolver resolver, int id) => resolver.Resolve(IdentifierCategory.Weapon, id);
    public static IdentifierResolution ResolveEquipment(this IIdentifierResolver resolver, int id) => resolver.Resolve(IdentifierCategory.Equipment, id);
    public static IdentifierResolution ResolveShipModel(this IIdentifierResolver resolver, int id) => resolver.Resolve(IdentifierCategory.ShipModel, id);
    public static IdentifierResolution ResolveStationModel(this IIdentifierResolver resolver, int id) => resolver.Resolve(IdentifierCategory.StationModel, id);
}

/// <summary>Explicit category-to-section binding; no semantic defaults are supplied.</summary>
public sealed record SectionBinding(IdentifierCategory Category, int Section, bool CallerValidated = false, int NameStride = 1);
public sealed record LocalizationInput(string Path, IReadOnlyList<SectionBinding> Sections, bool GameFileFormat = false);
public sealed record OverrideInput(string Path, SourceKind Kind);
public sealed class ResolverOptions
{
    public IReadOnlyList<LocalizationInput> LocalizationFiles { get; init; } = [];
    public IReadOnlyList<OverrideInput> OverrideFiles { get; init; } = [];
}
