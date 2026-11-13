using System.Formats.Nrbf;

namespace Decoder;

public sealed record Link(int Ref);
public sealed record Node(int Id, string Type, string Kind, Dictionary<string, object?>? Fields,
    int[]? Shape, object?[]? Items, object? Value);
public sealed record Graph(int Root, IReadOnlyList<Node> Records)
{
    public Node Resolve(Link link) => Records[link.Ref - 1];
}

public static class SaveDecoder
{
    public const int MaxBytes = 32 * 1024 * 1024;

    public static Graph Read(Stream stream)
    {
        if (!stream.CanSeek || stream.Length - stream.Position > MaxBytes)
            throw new InvalidDataException("Provide a seekable save of at most 32 MiB.");
        if (!NrbfDecoder.StartsWithPayloadHeader(stream))
            throw new InvalidDataException("Expected an NRBF save header. This file format is unsupported.");
        var root = NrbfDecoder.Decode(stream, out var map, leaveOpen: true);
        if (stream.Position != stream.Length)
            throw new InvalidDataException("Unexpected data after the NRBF payload.");
        var ids = new Dictionary<SerializationRecord, int>(ReferenceEqualityComparer.Instance);
        var pending = new List<SerializationRecord>();
        Link Reference(SerializationRecord record)
        {
            if (!ids.TryGetValue(record, out var id))
            {
                id = pending.Count + 1;
                ids.Add(record, id);
                pending.Add(record);
            }
            return new Link(id);
        }
        object? ConvertValue(object? value) => value is SerializationRecord record ? Reference(record) : value;
        var rootId = Reference(root).Ref;
        foreach (var record in map.Values)
            if (record is ClassRecord or ArrayRecord or PrimitiveTypeRecord)
                Reference(record);

        var nodes = new List<Node>();
        long cells = 0;
        for (var index = 0; index < pending.Count; index++)
        {
            var record = pending[index];
            var type = record.TypeName.AssemblyQualifiedName;
            var id = index + 1;
            switch (record)
            {
                case ClassRecord data:
                    nodes.Add(new(id, type, "object",
                        data.MemberNames.ToDictionary(name => name, name => ConvertValue(data.GetRawValue(name))),
                        null, null, null));
                    break;
                case ArrayRecord data:
                    var shape = data.Lengths.ToArray();
                    long count = shape.Contains(0) ? 0 : 1;
                    foreach (var length in shape)
                    {
                        count *= length;
                        if (count > 4_000_000)
                            throw new InvalidDataException("The save contains an oversized array.");
                    }
                    cells += count;
                    if (cells > 8_000_000)
                        throw new InvalidDataException("The save exceeds the total array element limit.");
                    var items = (data is SZArrayRecord<SerializationRecord> records ? records.GetArray() : data.GetArray(data.TypeNameMatches(typeof(string[]))
                        ? typeof(string[]) : ArrayType(data, shape.Length))).Cast<object?>().Select(ConvertValue).ToArray();
                    nodes.Add(new(id, type, "array", null, shape, items, null));
                    break;
                case PrimitiveTypeRecord primitive:
                    nodes.Add(new(id, type, "primitive", null, null, null, primitive.Value));
                    break;
                default:
                    throw new InvalidDataException($"Unsupported value record {record.RecordType}.");
            }
        }
        return new Graph(rootId, nodes);
    }

    private static Type ArrayType(ArrayRecord record, int rank)
    {
        Type Make(Type element) => rank == 1 ? element.MakeArrayType() : element.MakeArrayType(rank);
        foreach (var element in new[] { typeof(bool), typeof(byte), typeof(sbyte), typeof(char),
            typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(float), typeof(double), typeof(decimal), typeof(DateTime), typeof(TimeSpan), typeof(string) })
        {
            var array = Make(element);
            if (record.TypeNameMatches(array))
                return array;
        }
        return Make(typeof(SerializationRecord));
    }
}
