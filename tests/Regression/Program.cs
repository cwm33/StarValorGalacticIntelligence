using Decoder;
using System.Security.Cryptography;
using System.Text;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

// Handwritten NRBF: an object array containing a self-reference, a null and a string.
using var fixture = new MemoryStream();
using (var writer = new BinaryWriter(fixture, Encoding.UTF8, leaveOpen: true))
{
    writer.Write((byte)0);
    foreach (var value in new[] { 1, -1, 1, 0 }) writer.Write(value);
    writer.Write((byte)16);
    writer.Write(1);
    writer.Write(3);
    writer.Write((byte)9);
    writer.Write(1);
    writer.Write((byte)10);
    writer.Write((byte)6);
    writer.Write(2);
    writer.Write("fixture");
    writer.Write((byte)11);
}
fixture.Position = 0;
var synthetic = SaveDecoder.Read(fixture);
var array = synthetic.Records[synthetic.Root - 1].Items!;
Require(array.Length == 3 && array[0] is Link cycle && cycle.Ref == synthetic.Root &&
    array[1] is null && array[2] is Link text && Equals(synthetic.Resolve(text).Value, "fixture"),
    "Object array identity, null and string storage.");
fixture.Position = 1;
try
{
    SaveDecoder.Read(fixture);
    throw new Exception("Malformed header accepted.");
}
catch (InvalidDataException) { }
Console.WriteLine("Synthetic decoder checks passed.");

if (args.Length % 2 != 0) throw new ArgumentException("Supply main and Info paths in pairs.");
for (var i = 0; i < args.Length; i += 2)
{
    var paths = args[i..(i + 2)];
    byte[] Hash(string path)
    {
        using var file = File.OpenRead(path);
        return SHA256.HashData(file);
    }
    Graph Decode(string path)
    {
        using var file = File.OpenRead(path);
        return SaveDecoder.Read(file);
    }
    var before = paths.Select(Hash).ToArray();
    var main = Decode(paths[0]);
    var info = Decode(paths[1]);
    var view = SaveView.Create(main, info, "Unknown", false);
    var full = SaveView.Create(main, info, "Galaxy Reforged", true);
    Require(view.Metadata["shipModelID"] is int && view.Metadata["credits"] is float &&
        view.Metadata["level"] is int, "Expected player metadata.");
    Require(view.Tables["Stations"].Count > 0 && view.Tables["Station stock"].Count > 0 &&
        view.Tables["Faction relations"].Count > 0, "Expected stations, stock and faction relations.");
    Require(view.Tables["Sectors"].All(row => Equals(row["discovered"], true)) &&
        full.Tables["Sectors"].Count == view.Tables["Sectors"].Count + view.HiddenSectors,
        "Sector discovery gating.");
    var visible = view.Tables["Sectors"].Select(row => row["sectorIndex"]).ToHashSet();
    Require(view.Tables["Stations"].All(row => visible.Contains(row["sectorIndex"])), "Station discovery gating.");
    var modern = view.Metadata["baseBuilding"] is true;
    Require(view.Tables.ContainsKey(modern ? "Sector prices" : "Codex prices"), "Expected observed market layout.");
    Require(before.Zip(paths.Select(Hash)).All(pair => pair.First.SequenceEqual(pair.Second)), "Save bytes changed.");
    Console.WriteLine($"{Path.GetFileName(paths[0])}: {main.Records.Count} records; {view.Tables["Sectors"].Count} discovered sectors; checks passed.");
}
