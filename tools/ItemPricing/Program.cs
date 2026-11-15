using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Decoder;

if (args.Length != 4)
    throw new ArgumentException("Supply save, equipment extract, output and profile.");

using var save = File.OpenRead(args[0]);
var graph = SaveDecoder.Read(save);
save.Position = 0;
var saveSha256 = Convert.ToHexString(SHA256.HashData(save));
var root = graph.Records[graph.Root - 1];
var gameVersion = Convert.ToString(Field(root, "savedVersion"), CultureInfo.InvariantCulture);
if (gameVersion != "2.251")
    throw new InvalidDataException("This extractor supports save version 2.251.");

var catalog = Child(root, "weaponList");
var weapons = Child(catalog, "_items")?.Items;
if (weapons is null || Field(catalog, "_size") is not int length || length < 0 || length > weapons.Length)
    throw new InvalidDataException("Missing or invalid saved weapon definitions.");

var rarityMultipliers = new Dictionary<int, double> { [0] = 0.4, [1] = 1, [2] = 2, [3] = 5, [4] = 10, [5] = 12 };
var items = new List<object>();
for (var index = 0; index < length; index++)
{
    if (weapons[index] is not Link reference) continue;
    var weapon = graph.Resolve(reference);
    if (!weapon.Type.StartsWith("TWeapon,", StringComparison.Ordinal)) continue;
    try
    {
        var value = WeaponBase(weapon);
        if (float.IsFinite(value) && value >= 0)
            items.Add(new { @namespace = "weapon", id = index, baseValue = (double)value, rarityMultipliers });
    }
    catch (InvalidDataException e)
    {
        Console.Error.WriteLine($"Weapon #{index}: {e.Message}");
    }
}

var equipment = JsonSerializer.Deserialize<Dictionary<int, double>>(File.ReadAllText(args[1]))
    ?? throw new InvalidDataException("Missing equipment prices.");
foreach (var (id, value) in equipment)
    items.Add(new { @namespace = "equipment", id, baseValue = value, rarityMultipliers });

var result = new
{
    schemaVersion = 1,
    gameVersion,
    mechanicsProfile = args[3],
    saveSha256,
    source = "Installed 2.251d equipment assets and this save's weapon definitions",
    confidence = "Installed baseline",
    items
};
using var output = new FileStream(args[2], FileMode.CreateNew);
JsonSerializer.Serialize(output, result, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine($"Private catalog rows: {items.Count}");

static object? Field(Node? node, string name) => node?.Fields?.GetValueOrDefault(name);
Node? Child(Node? node, string name) => Field(node, name) is Link link ? graph.Resolve(link) : null;
float Number(Node node, string field)
{
    var value = Child(node, field) is { } child
        ? Field(child, "value__") ?? child.Value
        : Field(node, field);
    return value switch
    {
        float f => f, int i => i, byte b => b, sbyte sb => sb, short s => s,
        _ => throw new InvalidDataException($"Unsupported numeric field {field}.")
    };
}

float WeaponBase(Node weapon)
{
    var component = Number(weapon, "compType");
    if (component == 5) return 100f; // LightObject
    var burst = Number(weapon, "burst");
    var criticalFactor = 1f + Math.Clamp(Number(weapon, "critChance"), 0f, 100f) * 0.01f;
    var damage = Number(weapon, "damage");
    var interval = Number(weapon, "rateOfFire");
    var dps = burst == 0
        ? damage / interval * criticalFactor
        : damage * (burst + 1) / (interval + Number(weapon, "shortCooldown") * burst) * criticalFactor;
    var value = (dps + dps / Number(weapon, "space")) * (15f + Number(weapon, "range") * 0.25f);
    if (component == 2) value *= 1.2f; // BeamWeaponObject
    if (Number(weapon, "type") == 9) value *= 1.2f; // Torpedo
    return Number(weapon, "damageType") switch
    {
        2 => value * 0.7f, // ShieldsOnly
        3 => value * 1.2f, // IgnoreShields
        5 => value * 6f, // Repair
        _ => value
    };
}
