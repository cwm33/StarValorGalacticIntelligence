namespace GalacticIntelligence;

public sealed class GateRoutes
{
    private readonly HashSet<int> sectors;
    private readonly ILookup<int, int> exits;

    public GateRoutes(IEnumerable<Sector> sectors, IEnumerable<JumpConnection> connections)
    {
        this.sectors = sectors.Select(s => s.Id).ToHashSet();
        exits = connections.Where(c => c.Confidence == "Verified" && c.From != c.To &&
            this.sectors.Contains(c.From) && this.sectors.Contains(c.To))
            .Select(c => (c.From, c.To)).Distinct().OrderBy(c => c.To).ToLookup(c => c.From, c => c.To);
    }

    public IEnumerable<int> Exits(int sector) => exits[sector];

    // Stop at the requested depth for the advisor's local reach calculation.
    public IEnumerable<(int Sector, int Jumps)> Reach(int from, int maxJumps)
    {
        if (!sectors.Contains(from)) yield break;
        var visited = new HashSet<int> { from };
        var queue = new Queue<(int Sector, int Jumps)>();
        queue.Enqueue((from, 0));
        while (queue.TryDequeue(out var step))
        {
            yield return step;
            if (step.Jumps >= maxJumps) continue;
            foreach (var neighbor in exits[step.Sector])
                if (visited.Add(neighbor)) queue.Enqueue((neighbor, step.Jumps + 1));
        }
    }

    public int[]? Find(int from, int to)
    {
        if (!sectors.Contains(from) || !sectors.Contains(to)) return null;
        var previous = new Dictionary<int, int> { [from] = from };
        var queue = new Queue<int>();
        queue.Enqueue(from);
        while (queue.TryDequeue(out var current))
        {
            if (current == to)
            {
                var path = new List<int> { to };
                while (path[^1] != from) path.Add(previous[path[^1]]);
                path.Reverse();
                return path.ToArray();
            }
            foreach (var neighbor in exits[current])
                if (previous.TryAdd(neighbor, current)) queue.Enqueue(neighbor);
        }
        return null;
    }
}
