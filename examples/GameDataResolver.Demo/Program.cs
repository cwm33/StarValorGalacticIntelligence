using GalacticIntelligence.GameData;

IIdentifierResolver resolver;
if (args.Length == 0)
{
    Console.WriteLine("SYNTHETIC DEMO: invented data, no verified game mappings.");
    resolver = new IdentifierRegistry([
        new(IdentifierCategory.Weapon, 0, "Synthetic Light Emitter", new(SourceKind.ModOverride, "<synthetic-demo>")),
        new(IdentifierCategory.ShipModel, 55, "Synthetic Frigate", new(SourceKind.ModOverride, "<synthetic-demo>"))
    ]);
}
else if (args.Length == 2 && int.TryParse(args[1], out var section) && section >= 0)
{
    // The caller chooses the section; this does not establish its actual game semantics.
    resolver = ResolverLoader.Load(new()
    {
        LocalizationFiles = [new(args[0], [new(IdentifierCategory.Weapon, section)])]
    });
}
else
{
    Console.Error.WriteLine("Usage: demo [localization-path caller-selected-weapon-section]");
    return 2;
}
foreach (var result in new[] { resolver.ResolveWeapon(0), resolver.ResolveShipModel(55), resolver.ResolveCommodity(999) })
    Console.WriteLine($"{result.Category}:{result.RawId} -> {result.DisplayName} [{result.Status}, {result.Confidence}, {result.Source?.Kind.ToString() ?? "fallback"}] {result.DiagnosticMessage}");
foreach (var diagnostic in resolver.Diagnostics)
    Console.WriteLine($"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}");
return resolver.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error) ? 1 : 0;
