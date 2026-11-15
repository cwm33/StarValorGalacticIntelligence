using System.Globalization;
using Decoder;
using GalacticIntelligence.GameData;

sealed record CatalogName(int RawId, string Category, string? Name, string Status, string? Source, int? Section)
{
    public string DisplayName => Name ?? $"Unknown {Category switch
    {
        "ship-model" => "Ship",
        "station-model" => "Station Model",
        "weapon" => "Weapon",
        "equipment" => "Equipment",
        "faction" => "Faction",
        "crew" => "Crew Member",
        _ => "Item"
    }} (#{RawId})";
}

sealed class GameDataCatalog
{
    private static readonly IdentifierCategory Faction = new("faction");
    private static readonly IdentifierCategory Crew = new("crew");
    private static readonly IdentifierCategory UnknownItem = new("item");
    private readonly CatalogSnapshot vanilla;
    private readonly CatalogSnapshot reforged;

    public GameDataCatalog(IConfiguration config)
    {
        var directory = config["GameData:InstallationDirectory"];
        if (string.IsNullOrWhiteSpace(directory) && OperatingSystem.IsWindows())
        {
            var candidate = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Steam", "steamapps", "common", "Star Valor");
            if (File.Exists(Path.Combine(candidate, "Language", "english_main.txt"))) directory = candidate;
        }

        LocalizationInput[] files = string.IsNullOrWhiteSpace(directory) ? [] :
        [
            new(Path.Combine(directory, "Language", "english_main.txt"),
            [
                new(IdentifierCategory.Weapon, 1, true),
                new(IdentifierCategory.Equipment, 2, true, NameStride: 2),
                new(IdentifierCategory.Commodity, 3, true, NameStride: 2),
                new(Faction, 7, true, NameStride: 10),
                new(IdentifierCategory.ShipModel, 12, true)
            ], GameFileFormat: true)
        ];
        List<OverrideInput> overrides = [];
        if (config["GameData:UserOverrides"] is { Length: > 0 } userPath)
            overrides.Add(new(userPath, SourceKind.UserOverride));
        vanilla = Load(files, overrides);
        if (config["GameData:GalaxyReforgedOverrides"] is { Length: > 0 } modPath)
        {
            overrides.Add(new(modPath, SourceKind.ModOverride));
            reforged = Load(files, overrides);
        }
        else reforged = vanilla;
    }

    private static CatalogSnapshot Load(LocalizationInput[] files, List<OverrideInput> overrides)
    {
        var registry = ResolverLoader.Load(new() { LocalizationFiles = files, OverrideFiles = overrides });
        var sourceKinds = files.Select(x => (x.Path, Kind: SourceKind.VanillaLocalization))
            .Concat(overrides.Select(x => (x.Path, x.Kind))).ToArray();
        var conflicts = registry.Diagnostics.Where(d => d.Code == "ConflictingMapping");
        return new(registry, conflicts.Where(d => d.Category is not null && d.Id.HasValue)
            .Select(d => (d.Category!, d.Id!.Value, sourceKinds.First(s => s.Path == d.Source).Kind)).ToHashSet(),
            sourceKinds.Select(s => new CatalogSource(s.Kind.ToString(),
                s.Kind switch
                {
                    SourceKind.VanillaLocalization => "English game localization",
                    SourceKind.ModOverride => "Galaxy Reforged overrides",
                    _ => "User overrides"
                },
                registry.Entries.Count(e => e.Source!.Path == s.Path && e.Source.Kind == s.Kind))).ToArray(),
            registry.Diagnostics.Select(d => new CatalogIssue(d.Code, d.Severity.ToString(), d.LineNumber,
                d.Section, d.Category?.Value, d.Id,
                sourceKinds.FirstOrDefault(s => s.Path == d.Source).Kind.ToString())).ToArray());
    }

    public object Diagnostics(string mechanics = "Unknown", string? version = null)
    {
        var snapshot = Select(mechanics);
        var counts = snapshot.Registry.Entries.GroupBy(e => e.Category.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        var warnings = new List<string>();
        if (!snapshot.Sources.Any(s => s.Kind == nameof(SourceKind.VanillaLocalization) && s.Entries > 0))
            warnings.Add("English localization unavailable. Set GameData__InstallationDirectory and restart.");
        if (!string.IsNullOrEmpty(version) && version is not ("2.05" or "2.251"))
            warnings.Add("The save version is outside the validated mapping range. Catalog names still use category-scoped baseline mappings; inspect fields introduced by this version.");
        return new { snapshot.Sources, categories = counts, entries = counts.Values.Sum(), snapshot.Issues, warnings };
    }

    public SaveView Enrich(SaveView view, string mechanics)
    {
        var snapshot = Select(mechanics);
        void AddName(Dictionary<string, object?> row, string field, IdentifierCategory category)
        {
            if (row.GetValueOrDefault(field) is not int id) return;
            var lookup = snapshot.Registry.Resolve(category, id);
            var source = lookup.Source;
            var status = "Unresolved";
            string? name = null;
            if (source is not null && snapshot.Conflicts.Contains((category, id, source.Kind)))
                status = "Conflict";
            else if (source?.Kind is SourceKind.UserOverride or SourceKind.ModOverride &&
                lookup.Confidence == ResolutionConfidence.CallerValidated)
            {
                name = lookup.Name;
                status = "Resolved";
            }
            else if (category == IdentifierCategory.Weapon && view.WeaponNames.TryGetValue(id, out var saved))
            {
                name = saved;
                status = "Resolved";
                source = null;
            }
            else if (source?.Kind == SourceKind.VanillaLocalization)
            {
                name = lookup.Name;
                status = "Resolved";
            }

            var label = name is null ? null : source?.Kind.ToString() ?? "SaveWeaponCatalog";
            var resolution = new CatalogName(id, category.Value, name, status, label, name is null ? null : source?.Section);
            row[field + "Resolution"] = resolution;
        }

        AddName(view.Metadata, "shipModelID", IdentifierCategory.ShipModel);
        foreach (var (table, rows) in view.Tables)
        foreach (var row in rows)
        {
            switch (table)
            {
                case "Sector prices":
                case "Codex prices":
                    AddName(row, "itemID", IdentifierCategory.Commodity);
                    break;
                case "Player cargo":
                case "Station stock":
                case "Station market modules":
                    AddName(row, "itemID", row.GetValueOrDefault("itemType") switch
                    {
                        1 => IdentifierCategory.Weapon,
                        2 => IdentifierCategory.Equipment,
                        3 => IdentifierCategory.Commodity,
                        4 => IdentifierCategory.ShipModel,
                        5 => Crew,
                        int type => new IdentifierCategory($"item-type-{type}"),
                        _ => UnknownItem
                    });
                    break;
                case "Player ship":
                case "Player fleet":
                case "Sector ships":
                    AddName(row, "shipModelID", IdentifierCategory.ShipModel);
                    AddName(row, "factionIndex", Faction);
                    break;
                case "Stations":
                    AddName(row, "factionIndex", Faction);
                    AddName(row, "modelIndex", IdentifierCategory.StationModel);
                    break;
                case "Installed weapons":
                    AddName(row, "weaponIndex", IdentifierCategory.Weapon);
                    break;
                case "Installed equipment":
                    AddName(row, "equipmentID", IdentifierCategory.Equipment);
                    break;
            }
        }
        return view;
    }

    public static object Observed(SaveView view)
    {
        var identifiers = view.Metadata.Values.Concat(view.Tables.Values.SelectMany(rows => rows)
            .SelectMany(row => row.Values)).OfType<CatalogName>().Distinct()
            .OrderBy(x => x.Category).ThenBy(x => x.RawId).ToArray();
        return new
        {
            resolved = identifiers.Where(x => x.Status == "Resolved"),
            referenceNames = identifiers.Where(x => x.Status == "BaseGameReference"),
            unresolved = identifiers.Where(x => x.Status is "Unresolved" or "Conflict")
        };
    }

    private CatalogSnapshot Select(string mechanics) => mechanics == "Galaxy Reforged" ? reforged : vanilla;
    private sealed record CatalogSnapshot(IdentifierRegistry Registry,
        HashSet<(IdentifierCategory Category, int Id, SourceKind Kind)> Conflicts,
        CatalogSource[] Sources, CatalogIssue[] Issues);
    private sealed record CatalogSource(string Kind, string Label, int Entries);
    private sealed record CatalogIssue(string Code, string Severity, int? Line, int? Section, string? Category, int? Id, string Source);
}
