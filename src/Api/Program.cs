using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Decoder;

const int port = 5187;
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(k =>
{
    k.Listen(IPAddress.Loopback, port);
    k.Limits.MaxRequestBodySize = 2L * SaveDecoder.MaxBytes + 65536;
});
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals);
builder.Services.AddSingleton<GameDataCatalog>();
var app = builder.Build();
var catalog = app.Services.GetRequiredService<GameDataCatalog>();
app.Use(async (context, next) =>
{
    if (context.Request.Host.Host != "127.0.0.1" || context.Request.Host.Port != port)
    {
        context.Response.StatusCode = 400;
        return;
    }
    context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();

Session? current = null;
using var importLock = new SemaphoreSlim(1);
app.MapPost("/api/import", async (HttpRequest request) =>
{
    if (request.Headers["X-Star-Valor"] != "import" || !request.HasFormContentType)
        return Results.BadRequest(new { error = "Expected a local multipart import." });
    if (!await importLock.WaitAsync(0))
        return Results.Conflict(new { error = "An import is already in progress." });
    try
    {
        var form = await request.ReadFormAsync(request.HttpContext.RequestAborted);
        var save = form.Files.GetFile("save");
        var info = form.Files.GetFile("info");
        var match = Regex.Match(save?.FileName ?? "", @"^SaveGameData_([0-9]+)(?:_Auto)?[.]dat$", RegexOptions.IgnoreCase);
        if (save is null || !match.Success)
            return Results.BadRequest(new { error = "Choose SaveGameData_XX.dat or SaveGameData_XX_Auto.dat." });
        if (info is not null && !info.FileName.Equals(
            $"SaveGameData_{match.Groups[1].Value}_Info.dat", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = "The companion Info file must match the main save slot." });
        var mechanics = form["mechanics"].ToString();
        if (mechanics is not ("Vanilla" or "Galaxy Reforged")) mechanics = "Unknown";
        Graph Decode(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            return SaveDecoder.Read(stream);
        }
        var graph = Decode(save);
        var companion = info is null ? null : Decode(info);
        var safe = catalog.Enrich(SaveView.Create(graph, companion, mechanics, false), mechanics);
        Volatile.Write(ref current, new Session(Guid.NewGuid(), graph, companion, mechanics, safe, catalog, DateTimeOffset.UtcNow));
        return Results.Ok(new { id = current.Id, view = safe, hasInfo = companion is not null });
    }
    catch (Exception e) when (e is IOException or InvalidDataException or
        System.Runtime.Serialization.SerializationException or NotSupportedException or
        InvalidOperationException or ArgumentException or KeyNotFoundException or OverflowException)
    {
        return Results.BadRequest(new { error = $"Import failed. Check the save format or version. {e.Message}" });
    }
    finally { importLock.Release(); }
});

app.MapGet("/api/{id:guid}/intelligence", (Guid id, bool? spoilers) =>
    Find(id) is { } s ? Results.Ok(spoilers == true ? s.FullIntelligence.Value : s.SafeIntelligence.Value) : Missing());
app.MapGet("/api/{id:guid}/route", (Guid id, bool? spoilers, int from, int to) =>
{
    if (Find(id) is not { } s) return Missing();
    var snapshot = spoilers == true ? s.FullIntelligence.Value : s.SafeIntelligence.Value;
    var path = snapshot.Routes.Find(from, to);
    return Results.Ok(new { path, jumps = path?.Length - 1 });
});
app.MapGet("/api/{id:guid}/nearby-markets", (Guid id, bool? spoilers, int? jumps, string? query, double? driveRange) =>
{
    if (Find(id) is not { } s) return Missing();
    var snapshot = spoilers == true ? s.FullIntelligence.Value : s.SafeIntelligence.Value;
    if (driveRange is double range && (!double.IsFinite(range) || range < 0))
        return Results.BadRequest(new { error = "Enter a finite, nonnegative jump-drive range." });
    var result = GalacticIntelligence.NearbyMarkets.Find(snapshot, Math.Clamp(jumps ?? 4, 0, 12), query, driveRange);
    return Results.Ok(new { result.Buys, result.Sells, result.Trades });
});
app.MapGet("/api/{id:guid}/trade", (Guid id, bool? spoilers, bool? experimental, string? rankBy) =>
{
    if (Find(id) is not { } s) return Missing();
    var snapshot = spoilers == true ? s.FullIntelligence.Value : s.SafeIntelligence.Value;
    // Base-game sector summaries provide indicative spreads only.
    var quotes = snapshot.Markets.Where(m => m.Source == "Sector prices" && m.SectorId.HasValue && m.PriceSemantics != "Unverified")
        .Select(m => new GalacticIntelligence.TradeQuote(m.Commodity, $"Sector #{m.SectorId}", m.SellingPrice, m.BuyingPrice,
            null, null, null, false, false, null, null, m.Source, m.SectorId)).ToArray();
    return Results.Ok(new { interpretation = "Indicative sector spread: sellingPrice is the player cost reference; buyingPrice is the proceeds reference.",
        opportunities = GalacticIntelligence.TradeAnalyzer.Analyze(quotes,
            new(snapshot.Player.Credits, null, null, null), experimental == true, rankBy ?? "difference").Select(opportunity => opportunity with
            {
                GateJumps = opportunity.FromSector is int from && opportunity.ToSector is int to
                    ? snapshot.Routes.Find(from, to)?.Length - 1 : null
            }),
        note = "Legacy codex observations have no paired price direction and are excluded. No verified transaction quotes are currently extracted." });
});
app.MapGet("/api/{id:guid}/arsenal", (Guid id, bool? spoilers) =>
{
    if (Find(id) is not { } s) return Missing();
    return Results.Ok(GalacticIntelligence.Arsenal.Build(spoilers == true ? s.FullIntelligence.Value : s.SafeIntelligence.Value));
});
app.MapPost("/api/{id:guid}/arsenal/value", (Guid id, bool? spoilers, GalacticIntelligence.PricingCatalog pricing) =>
{
    if (Find(id) is not { } s) return Missing();
    try { return Results.Ok(GalacticIntelligence.Arsenal.Build(spoilers == true ? s.FullIntelligence.Value : s.SafeIntelligence.Value, pricing)); }
    catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
});
app.MapGet("/api/{id:guid}/advisor", (Guid id, bool? spoilers, double? stations, double? markets, double? equipment, double? ships, double? connectivity, string? preset) =>
{
    if (Find(id) is not { } s) return Missing();
    return Results.Ok(GalacticIntelligence.HomeAdvisor.Rank(spoilers == true ? s.FullIntelligence.Value : s.SafeIntelligence.Value,
        preset is not null ? GalacticIntelligence.AdvisorWeights.Preset(preset) : new(stations ?? 1, markets ?? 1, equipment ?? 1, ships ?? 1, connectivity ?? 1)));
});
app.MapPost("/api/industry/plan", (GalacticIntelligence.IndustryRequest scenario, HttpRequest request) =>
{
    if (request.Headers["X-Star-Valor"] != "scenario") return Results.BadRequest(new { error = "Expected a local planning scenario." });
    try { return Results.Ok(GalacticIntelligence.IndustryPlanner.Plan(scenario)); }
    catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
});
app.MapGet("/api/resolver", () => Results.Ok(catalog.Diagnostics()));
app.MapGet("/api/{id:guid}/resolver", (Guid id, bool? spoilers) =>
{
    if (Find(id) is not { } session) return Missing();
    var view = View(session, spoilers);
    var version = Convert.ToString(view.Metadata.GetValueOrDefault("savedVersion"), CultureInfo.InvariantCulture);
    return Results.Ok(new { catalog = catalog.Diagnostics(session.Mechanics, version), observed = GameDataCatalog.Observed(view) });
});
app.MapGet("/api/{id:guid}/view", (Guid id, bool? spoilers) =>
    Find(id) is { } s ? Results.Ok(View(s, spoilers)) : Missing());
app.MapGet("/api/{id:guid}/schema", (Guid id, bool? spoilers, string? source, string? q, int? offset, int? record, int? itemOffset) =>
{
    if (Find(id) is not { } s) return Missing();
    if (spoilers != true) return Results.BadRequest(new { error = "Enable spoilers to inspect raw records." });
    var graph = source == "info" ? s.Info : s.Main;
    if (graph is null) return Results.NotFound();
    if (record is int number)
    {
        if (number < 1 || number > graph.Records.Count) return Results.NotFound();
        var node = graph.Records[number - 1];
        return Results.Ok(new { node.Id, node.Type, node.Kind, node.Fields, node.Shape, node.Value,
            itemCount = node.Items?.Length, items = node.Items?.Skip(Math.Max(0, itemOffset ?? 0)).Take(100) });
    }
    var rows = graph.Records.Where(node => string.IsNullOrEmpty(q) ||
        node.Type.Contains(q, StringComparison.OrdinalIgnoreCase) ||
        node.Id.ToString(CultureInfo.InvariantCulture) == q ||
        node.Fields?.Keys.Any(k => k.Contains(q, StringComparison.OrdinalIgnoreCase)) == true);
    return Results.Ok(new { graph.Root, total = rows.Count(), records = rows.Skip(Math.Max(0, offset ?? 0))
        .Take(100).Select(node => new { node.Id, node.Type, node.Kind }) });
});
app.MapGet("/api/{id:guid}/export.json", (Guid id, bool? spoilers, bool? raw, HttpResponse response) =>
{
    if (Find(id) is not { } s) return Missing();
    if (raw == true && spoilers != true) return Results.BadRequest(new { error = "Raw exports contain spoilers." });
    response.Headers.ContentDisposition = "attachment; filename=star-valor-save.json";
    return raw == true ? Results.Json(new { s.Main, s.Info, s.Mechanics }) : Results.Json(View(s, spoilers));
});
app.MapGet("/api/{id:guid}/export.csv", (Guid id, bool? spoilers, string? table) =>
{
    if (Find(id) is not { } s) return Missing();
    if (table is null || !View(s, spoilers).Tables.TryGetValue(table, out var rows)) return Results.NotFound();
    var headers = rows.SelectMany(row => row.Keys).Distinct().ToArray();
    var csv = new StringBuilder().AppendJoin(',', headers.Select(Cell)).Append("\r\n");
    foreach (var row in rows)
        csv.AppendJoin(',', headers.Select(key => Cell(row.GetValueOrDefault(key) is CatalogName resolution
            ? resolution.DisplayName : row.GetValueOrDefault(key)))).Append("\r\n");
    return Results.File(new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
        "text/csv; charset=utf-8", "star-valor-table.csv");
});
app.Run();

Session? Find(Guid id) => Volatile.Read(ref current) is { } s && s.Id == id ? s : null;
static SaveView View(Session s, bool? spoilers) =>
    spoilers == true ? s.Full.Value : s.Safe;
static IResult Missing() => Results.NotFound(new { error = "Import the save again. The current session has changed." });
static string Cell(object? value)
{
    var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    if (value is string && (text.TrimStart().StartsWith('=') || text.TrimStart().StartsWith('+') ||
        text.TrimStart().StartsWith('-') || text.TrimStart().StartsWith('@') ||
        (text.Length > 0 && char.IsControl(text[0])))) text = "'" + text;
    return "\"" + text.Replace("\"", "\"\"") + "\"";
}

sealed class Session
{
    public Guid Id { get; }
    public Graph Main { get; }
    public Graph? Info { get; }
    public string Mechanics { get; }
    public SaveView Safe { get; }
    public DateTimeOffset ImportedAt { get; }
    public Lazy<SaveView> Full { get; }
    public Lazy<GalacticIntelligence.GalaxySnapshot> SafeIntelligence { get; }
    public Lazy<GalacticIntelligence.GalaxySnapshot> FullIntelligence { get; }
    public Session(Guid id, Graph main, Graph? info, string mechanics, SaveView safe, GameDataCatalog catalog, DateTimeOffset importedAt)
    {
        Id = id; Main = main; Info = info; Mechanics = mechanics; Safe = safe; ImportedAt = importedAt;
        Full = new(() => catalog.Enrich(SaveView.Create(main, info, mechanics, true), mechanics));
        SafeIntelligence = new(() => IntelligenceProjection.Create(safe, id.ToString(), importedAt, false));
        FullIntelligence = new(() => IntelligenceProjection.Create(Full.Value, id.ToString(), importedAt, true));
    }
}
