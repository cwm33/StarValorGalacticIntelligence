using System.Security.Cryptography;
using System.Text;
using GalacticIntelligence.GameData;

// Dependency-free executable unit tests, matching tests/Regression's convention.
var tests = new List<(string Name, Action Body)>();
void Test(string name, Action body) => tests.Add((name, body));
static void Require(bool condition, string message = "Assertion failed")
{
    if (!condition) throw new InvalidOperationException(message);
}
static LocalizationParseResult Parse(string text) => new LocalizationParser().Parse(new StringReader(text), "<synthetic>");
static bool Has(IEnumerable<ResolverDiagnostic> diagnostics, string code) => diagnostics.Any(d => d.Code == code);
var directory = Path.Combine(Path.GetTempPath(), "resolver-tests-" + Guid.NewGuid());
Directory.CreateDirectory(directory);
string Write(string filename, string content)
{
    var path = Path.Combine(directory, filename);
    File.WriteAllText(path, content, new UTF8Encoding(false));
    return path;
}
IdentifierRegistry Load(string path, params SectionBinding[] sections) => ResolverLoader.Load(new()
{
    LocalizationFiles = [new(path, sections)]
});
var fixture = Path.Combine(AppContext.BaseDirectory, "TestData", "synthetic-language.txt");
var weapons = IdentifierCategory.Weapon;
var ships = IdentifierCategory.ShipModel;

Test("basic resolution and provenance", () =>
{
    var result = Load(fixture, new SectionBinding(weapons, 1)).ResolveWeapon(0);
    Require(result.Name == "Synthetic Light Emitter" && result.RawId == 0 && result.Category == weapons);
    Require(result.Status == ResolutionStatus.Resolved && result.Confidence == ResolutionConfidence.Unverified);
    Require(result.Source is { Kind: SourceKind.VanillaLocalization, Section: 1, LineNumber: 3 } && result.Source.Path == fixture);
});
Test("same ID across namespaces", () =>
{
    var resolver = Load(fixture, new SectionBinding(weapons, 1), new SectionBinding(ships, 22));
    Require(resolver.ResolveWeapon(0).Name != resolver.ResolveShipModel(0).Name);
    Require(!Has(resolver.Diagnostics, "ConflictingMapping"));
});
Test("unknown and negative IDs preserve raw values", () =>
{
    var resolver = Load(fixture, new SectionBinding(weapons, 1));
    foreach (var id in new[] { -1, 54321 })
    {
        var result = resolver.ResolveEquipment(id);
        Require(result.RawId == id && result.Name is null && result.Source is null);
        Require(result.Status == ResolutionStatus.Unresolved && result.Confidence == ResolutionConfidence.Unknown);
        Require(result.DiagnosticMessage!.Contains("Unknown identifier") && result.DisplayName == $"equipment:{id}");
    }
});
Test("missing section is explicit", () =>
{
    var resolver = Load(fixture, new SectionBinding(IdentifierCategory.Commodity, 42));
    Require(Has(resolver.Diagnostics, "MissingSection") && resolver.ResolveCommodity(0).Name is null);
});
Test("no automatic section/category assumptions", () =>
{
    var resolver = Load(fixture);
    Require(resolver.ResolveWeapon(0).Status == ResolutionStatus.Unresolved);
});
Test("Unicode and reverse lookup", () =>
{
    var resolver = Load(fixture, new SectionBinding(weapons, 1));
    Require(resolver.ResolveWeapon(1).Name == "Émetteur léger" && resolver.ResolveWeapon(2).Name == "合成レーザー");
    Require(resolver.FindByName(weapons, " ÉMETTEUR LÉGER ").Single().RawId == 1);
    Require(resolver.FindByName(ships, "Émetteur léger").Count == 0);
});
Test("pipe fields and future sections retained", () =>
{
    using var reader = File.OpenText(fixture);
    var result = new LocalizationParser().Parse(reader);
    Require(result.Sections.Contains(9999));
    Require(result.Entries.Single(e => e.Id == int.MaxValue).Fields.Count == 6);
    Require(result.Entries.First().Fields.SequenceEqual(new[] { "Synthetic Light Emitter", "description", "future-field", "" }));
    Require(result.Diagnostics.Count == 0);
});
Test("BOM, whitespace, comments, CRLF, EOF", () =>
{
    var parsed = Parse("\uFEFF[8] Label\r\n; comment\r\n// comment\r\n# comment\r\n\r\n  007 Name |metadata|");
    Require(parsed.Entries.Single() is { Section: 8, Id: 7, Name: "Name" });
    Require(parsed.Diagnostics.Count == 0);
});
Test("malformed lines and Int32 overflow", () =>
{
    var parsed = Parse("000 Orphan|\n[1]\nno record\n-1 Negative|\n2147483648 Overflow|\n1 No pipe\n2 |\n003 Good|");
    Require(parsed.Entries.Single().Name == "Good");
    Require(parsed.Diagnostics.Count(d => d.Code == "MalformedEntry") == 5);
    Require(Has(parsed.Diagnostics, "EntryWithoutSection"));
});
Test("bad header resets section until recovery", () =>
{
    var parsed = Parse("[1]\n0 Before|\n[bad]\n1 Ignored|\n[2147483648]\n2 Ignored|\n[2] Recovered\n3 After|");
    Require(parsed.Entries.Count == 2 && parsed.Entries.Last().Section == 2);
    Require(parsed.Diagnostics.Count(d => d.Code == "MalformedSection") == 2);
});
Test("duplicate records retain first including repeated sections", () =>
{
    var parsed = Parse("[1]\n0 First|\n0 First|\n[1] Again\n0 Conflicting|\n1 Second|");
    Require(parsed.Entries.Count == 2 && parsed.Entries.First().Name == "First");
    Require(Has(parsed.Diagnostics, "DuplicateSection") && Has(parsed.Diagnostics, "DuplicateEntry") && Has(parsed.Diagnostics, "ConflictingEntry"));
});
Test("empty localization reports missing configured sections", () =>
{
    var resolver = Load(Write("empty.txt", ""), new SectionBinding(weapons, 1));
    Require(Has(resolver.Diagnostics, "MissingSection"));
});
Test("missing files remain unresolved with diagnostics", () =>
{
    var resolver = Load(Path.Combine(directory, "missing", "english_main.txt"), new SectionBinding(weapons, 1));
    Require(Has(resolver.Diagnostics, "FileNotFound") && resolver.ResolveWeapon(0).Name is null);
});
Test("all source priorities and reverse lookup use winners", () =>
{
    var mod = Write("mod.json", """
        {"schemaVersion":1,"mappings":[{"category":"weapon","id":0,"name":"Mod Name"},{"category":"equipment","id":0,"name":"Mod Equipment"}]}
        """);
    var user = Write("user.json", """
        {"schemaVersion":1,"mappings":[{"category":"weapon","id":0,"name":"User Name"}]}
        """);
    var resolver = ResolverLoader.Load(new()
    {
        LocalizationFiles = [new(fixture, [new SectionBinding(weapons, 1)])],
        OverrideFiles = [new(user, SourceKind.UserOverride), new(mod, SourceKind.ModOverride)]
    });
    Require(resolver.ResolveWeapon(0) is { Name: "User Name", Source.Kind: SourceKind.UserOverride });
    Require(resolver.ResolveEquipment(0).Name == "Mod Equipment");
    Require(resolver.ResolveWeapon(1).Source!.Kind == SourceKind.VanillaLocalization);
    Require(resolver.FindByName(weapons, "Mod Name").Count == 0 && resolver.FindByName(weapons, "Synthetic Light Emitter").Count == 0);
    Require(resolver.FindByName(weapons, "User Name").Single().RawId == 0);
    Require(resolver.Diagnostics.Count(d => d.Code == "OverriddenMapping") == 2);
    var modOnly = ResolverLoader.Load(new() { LocalizationFiles = [new(fixture, [new SectionBinding(weapons, 1)])], OverrideFiles = [new(mod, SourceKind.ModOverride)] });
    Require(modOnly.ResolveWeapon(0).Source!.Kind == SourceKind.ModOverride);
});
Test("equal-priority first-wins and ambiguous reverse results", () =>
{
    var first = Write("first.json", """
        {"schemaVersion":1,"mappings":[{"category":"weapon","id":2,"name":"Shared"},{"category":"weapon","id":1,"name":"Shared"},{"category":"weapon","id":1,"name":"Shared"}]}
        """);
    var second = Write("second.json", """
        {"schemaVersion":1,"mappings":[{"category":"weapon","id":1,"name":"Other"}]}
        """);
    var resolver = ResolverLoader.Load(new() { OverrideFiles = [new(first, SourceKind.ModOverride), new(second, SourceKind.ModOverride)] });
    Require(resolver.ResolveWeapon(1).Name == "Shared");
    Require(resolver.FindByName(weapons, "shared").Select(r => r.RawId).SequenceEqual(new[] { 1, 2 }));
    Require(Has(resolver.Diagnostics, "DuplicateMapping") && Has(resolver.Diagnostics, "ConflictingMapping"));
});
Test("invalid JSON diagnosed without partial mappings", () =>
{
    var path = Write("invalid.json", "{\"schemaVersion\":1,\"mappings\":[{\"category\":\"weapon\",\"id\":0,\"name\":\"Partial\"},");
    var resolver = ResolverLoader.Load(new() { OverrideFiles = [new(path, SourceKind.UserOverride)] });
    Require(Has(resolver.Diagnostics, "InvalidJson") && resolver.ResolveWeapon(0).Name is null);
});
Test("unsupported versions rejected, not interpreted as v1", () =>
{
    foreach (var version in new[] { "2", "\"1\"", "null" })
    {
        var path = Write("future.json", "{\"schemaVersion\":" + version + ",\"mappings\":[{\"category\":\"weapon\",\"id\":0,\"name\":\"Future\"}]}");
        var resolver = ResolverLoader.Load(new() { OverrideFiles = [new(path, SourceKind.ModOverride)] });
        Require(Has(resolver.Diagnostics, "UnsupportedOverrideSchema") && resolver.ResolveWeapon(0).Name is null);
    }
});
Test("malformed override records skip individually", () =>
{
    var path = Write("records.json", """
        {"schemaVersion":1,"futureMetadata":true,"mappings":[
          null, {}, {"category":"weapon","id":"0","name":"Wrong type"},
          {"category":"weapon","id":2147483648,"name":"Overflow"},
          {"category":"weapon","id":0,"name":" "},
          {"category":"weapon","id":0,"name":"Invalid flag","callerValidated":"yes"},
          {"category":" WEAPON ","id":0,"name":"Good","future":42}
        ]}
        """);
    var resolver = ResolverLoader.Load(new() { OverrideFiles = [new(path, SourceKind.UserOverride)] });
    Require(resolver.Diagnostics.Count(d => d.Code == "MalformedOverride") == 6 && resolver.ResolveWeapon(0).Name == "Good");
});
Test("invalid override document shapes", () =>
{
    foreach (var json in new[] { "[]", "{}", "null", "{\"schemaVersion\":1}", "{\"schemaVersion\":1,\"mappings\":{}}" })
    {
        var resolver = ResolverLoader.Load(new() { OverrideFiles = [new(Write("shape.json", json), SourceKind.ModOverride)] });
        Require(resolver.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error));
    }
});
Test("caller validation is explicit and not inherited by overrides", () =>
{
    Require(Load(fixture, new SectionBinding(weapons, 1, true)).ResolveWeapon(0).Confidence == ResolutionConfidence.CallerValidated);
    var path = Write("validated.json", """
        {"schemaVersion":1,"mappings":[{"category":"weapon","id":0,"name":"Unverified"},{"category":"station-model","id":1,"name":"Attested","callerValidated":true}]}
        """);
    var resolver = ResolverLoader.Load(new() { LocalizationFiles = [new(fixture, [new SectionBinding(weapons, 1, true)])], OverrideFiles = [new(path, SourceKind.UserOverride)] });
    Require(resolver.ResolveWeapon(0).Confidence == ResolutionConfidence.Unverified);
    Require(resolver.ResolveStationModel(1).Confidence == ResolutionConfidence.CallerValidated);
});
Test("arbitrary namespaces support future save fields", () =>
{
    var path = Write("custom.json", """
        {"schemaVersion":1,"mappings":[{"category":"faction","id":5,"name":"Synthetic Faction"}]}
        """);
    var resolver = ResolverLoader.Load(new() { OverrideFiles = [new(path, SourceKind.ModOverride)] });
    Require(resolver.Resolve(new("FACTION"), 5).Name == "Synthetic Faction");
    Require(resolver.ResolveStationModel(5).Status == ResolutionStatus.Unresolved);
});
Test("read-only input hashes, caller-owned reader, concurrent lookups", () =>
{
    var before = SHA256.HashData(File.ReadAllBytes(fixture));
    var resolver = Load(fixture, new SectionBinding(weapons, 1));
    Parallel.For(0, 1000, i => Require(resolver.ResolveWeapon(0).Name == "Synthetic Light Emitter"));
    Require(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(fixture))));
    using var reader = new StringReader("[1]\n0 Name|");
    new LocalizationParser().Parse(reader);
    Require(reader.ReadToEnd() == "");
});
Test("invalid UTF8 diagnosed", () =>
{
    var path = Path.Combine(directory, "bad-encoding.txt");
    File.WriteAllBytes(path, new byte[] { 0x5b, 0x31, 0x5d, 0x0a, 0xff });
    Require(Has(Load(path, new SectionBinding(weapons, 1)).Diagnostics, "InvalidEncoding"));
});
Test("invalid configuration rejected", () =>
{
    try { _ = new IdentifierCategory(" "); throw new Exception("Accepted empty category"); }
    catch (ArgumentException) { }
    try { Load(fixture, new SectionBinding(weapons, -1)); throw new Exception("Accepted negative section"); }
    catch (ArgumentOutOfRangeException) { }
    try
    {
        ResolverLoader.Load(new() { OverrideFiles = [new(fixture, SourceKind.VanillaLocalization)] });
        throw new Exception("Accepted invalid override kind");
    }
    catch (ArgumentException) { }
});

var failed = 0;
try
{
    foreach (var (name, body) in tests)
    {
        try { body(); Console.WriteLine($"PASS {name}"); }
        catch (Exception error) { failed++; Console.Error.WriteLine($"FAIL {name}: {error}"); }
    }
}
finally { Directory.Delete(directory, recursive: true); }
Console.WriteLine($"{tests.Count - failed}/{tests.Count} resolver tests passed (synthetic fixtures only).");
return failed == 0 ? 0 : 1;
