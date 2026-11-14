namespace GalacticIntelligence.GameData;

public sealed record IdentifierMapping(
    IdentifierCategory Category, int Id, string Name, LocalizationSource Source,
    bool CallerValidated = false);

/// <summary>Immutable snapshot. Build once after loading configuration, share across readers.</summary>
public sealed class IdentifierRegistry : IIdentifierResolver
{
    private readonly Dictionary<(IdentifierCategory Category, int Id), IdentifierResolution> mappings = [];
    private readonly Dictionary<IdentifierCategory, Dictionary<string, IReadOnlyList<IdentifierResolution>>> names = [];
    public IReadOnlyList<ResolverDiagnostic> Diagnostics { get; }
    public IReadOnlyList<IdentifierResolution> Entries { get; }

    public IdentifierRegistry(IEnumerable<IdentifierMapping> entries, IEnumerable<ResolverDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var messages = diagnostics?.ToList() ?? [];
        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry.Category);
            ArgumentNullException.ThrowIfNull(entry.Source);
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.Name);
            var rank = Rank(entry.Source.Kind);
            var key = (entry.Category, entry.Id);
            var resolution = new IdentifierResolution(entry.Id, entry.Category, entry.Name.Trim(), entry.Source,
                ResolutionStatus.Resolved, entry.CallerValidated ? ResolutionConfidence.CallerValidated : ResolutionConfidence.Unverified);
            if (mappings.TryGetValue(key, out var existing))
            {
                var replace = rank > Rank(existing.Source!.Kind);
                var code = rank == Rank(existing.Source.Kind)
                    ? (existing.Name == resolution.Name ? "DuplicateMapping" : "ConflictingMapping") : "OverriddenMapping";
                messages.Add(new(code, DiagnosticSeverity.Warning,
                    $"{key.Category}:{key.Id}: '{existing.Name}' ({existing.Source.Kind}, {existing.Source.Path}) vs '{resolution.Name}' ({entry.Source.Kind}, {entry.Source.Path}); {(replace ? "incoming" : "existing")} mapping retained. Equal-priority sources use first-entry-wins.",
                    entry.Source.Path, entry.Source.LineNumber, entry.Category, entry.Id));
                if (!replace) continue;
            }
            mappings[key] = resolution;
        }
        foreach (var category in mappings.Values.GroupBy(x => x.Category))
            names[category.Key] = category.GroupBy(x => x.Name!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<IdentifierResolution>)Array.AsReadOnly(g.OrderBy(x => x.RawId).ToArray()), StringComparer.OrdinalIgnoreCase);
        Diagnostics = messages.AsReadOnly();
        Entries = Array.AsReadOnly(mappings.Values.ToArray());
    }

    public IdentifierResolution Resolve(IdentifierCategory category, int id)
    {
        ArgumentNullException.ThrowIfNull(category);
        return mappings.TryGetValue((category, id), out var result) ? result : new(id, category, null, null,
            ResolutionStatus.Unresolved, ResolutionConfidence.Unknown,
            $"Unknown identifier {category}:{id}; no configured mapping was loaded. See load diagnostics for missing files or sections.");
    }

    public IReadOnlyList<IdentifierResolution> FindByName(IdentifierCategory category, string name)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return names.TryGetValue(category, out var index) && index.TryGetValue(name.Trim(), out var results) ? results : [];
    }

    private static int Rank(SourceKind kind) => kind switch
    {
        SourceKind.VanillaLocalization => 0,
        SourceKind.ModOverride => 1,
        SourceKind.UserOverride => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
