namespace GalacticIntelligence;

// User-supplied catalogs are scenarios, not verified game rules. No recipes ship with the app.
public sealed record ResourceAmount(Identity Item, double Amount);
public sealed record ProductionRecipe(string Id, string Profile, string GameVersion, string Source,
    IReadOnlyList<ResourceAmount> Inputs, IReadOnlyList<ResourceAmount> Outputs);
public sealed record ProductionOrder(string RecipeId, int Cycles);
public sealed record IndustryRequest(string Profile, string GameVersion, IReadOnlyList<ProductionRecipe> Recipes,
    IReadOnlyList<ProductionOrder> Orders, IReadOnlyList<ResourceAmount> Available);
public sealed record ResourceBalance(Identity Item, double Consumed, double Produced, double? Available, double? Shortfall);
public sealed record IndustryPlan(IReadOnlyList<ResourceBalance> Resources, IReadOnlyList<string> Warnings);
public static class IndustryPlanner
{
    public static IndustryPlan Plan(IndustryRequest request)
    {
        if (request.Recipes is null || request.Orders is null || request.Available is null ||
            request.Recipes.Any(r => r is null || r.Inputs is null || r.Outputs is null || string.IsNullOrWhiteSpace(r.Id)) ||
            request.Orders.Any(o => o is null) || string.IsNullOrWhiteSpace(request.Profile) || string.IsNullOrWhiteSpace(request.GameVersion))
            throw new ArgumentException("Scenario requires profile, version, recipes, orders and available resource arrays.");
        if (request.Recipes.Count > 100 || request.Orders.Count > 100 || request.Available.Count > 1000)
            throw new ArgumentException("Scenario exceeds supported size.");
        if (request.Recipes.Select(r => r.Id).Distinct().Count() != request.Recipes.Count)
            throw new ArgumentException("Recipe IDs must be unique.");
        void Validate(IEnumerable<ResourceAmount> amounts) {
            var list = amounts.ToArray();
            if (list.Length > 1000 || list.Any(a => a is null || a.Item is null || a.Item.Namespace != "commodity" || a.Item.Id < 0 || !double.IsFinite(a.Amount) || a.Amount < 0 || a.Amount > 1e12) ||
                list.Select(a => (a.Item.Namespace, a.Item.Id)).Distinct().Count() != list.Length)
                throw new ArgumentException("Resource amounts require unique nonnegative commodity IDs and finite quantities up to 1e12.");
        }
        Validate(request.Available);
        var recipes = request.Recipes.ToDictionary(r => r.Id);
        foreach (var recipe in request.Recipes) {
            if (string.IsNullOrWhiteSpace(recipe.Source) || recipe.Profile != request.Profile || recipe.GameVersion != request.GameVersion)
                throw new ArgumentException("Recipe provenance must match the scenario profile and version and include a source.");
            Validate(recipe.Inputs); Validate(recipe.Outputs);
            if (recipe.Outputs.Count == 0 || !recipe.Outputs.Any(a => a.Amount > 0)) throw new ArgumentException("Recipe requires a positive output.");
        }
        var flows = new Dictionary<(string, int), (Identity Item, double Consumed, double Produced)>();
        foreach (var order in request.Orders) {
            if (order.Cycles < 0 || order.Cycles > 1000000 || !recipes.TryGetValue(order.RecipeId, out var recipe))
                throw new ArgumentException("Order must reference a catalog recipe and 0–1000000 cycles.");
            foreach (var (amounts, input) in new[] { (recipe.Inputs, true), (recipe.Outputs, false) })
            foreach (var amount in amounts) {
                var key = (amount.Item.Namespace, amount.Item.Id);
                var flow = flows.GetValueOrDefault(key, (Item: amount.Item, Consumed: 0d, Produced: 0d));
                flows[key] = (flow.Item, flow.Consumed + (input ? amount.Amount * order.Cycles : 0), flow.Produced + (input ? 0 : amount.Amount * order.Cycles));
            }
        }
        var available = request.Available.ToDictionary(a => (a.Item.Namespace, a.Item.Id), a => a.Amount);
        return new(flows.Select(p => new ResourceBalance(p.Value.Item, p.Value.Consumed, p.Value.Produced,
            available.TryGetValue(p.Key, out var stock) ? stock : null,
            available.ContainsKey(p.Key) ? Math.Max(0, p.Value.Consumed - p.Value.Produced - available[p.Key]) : null)).ToArray(),
            ["User-supplied scenario only; recipes and ownership are not verified by the application.",
             "Aggregate resource balance only. Production order, startup inputs, rates, station capacities and transport routes are not modeled.",
             "Missing available amounts are unknown. Station market stock is never assumed to be your production inventory."]);
    }
}
