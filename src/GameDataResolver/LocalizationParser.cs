using System.Globalization;
using System.Text.RegularExpressions;

namespace GalacticIntelligence.GameData;

public sealed record LocalizationEntry(int Section, int Id, string Name, IReadOnlyList<string> Fields, int LineNumber);
public sealed record LocalizationParseResult(
    IReadOnlyList<int> Sections, IReadOnlyList<LocalizationEntry> Entries,
    IReadOnlyList<ResolverDiagnostic> Diagnostics);

/// <summary>Parser for the reported line-oriented format, pending authentic-file validation.</summary>
public sealed class LocalizationParser
{
    private static readonly Regex Header = new(@"^\[(\d+)\](?:\s.*)?$", RegexOptions.CultureInvariant);
    private static readonly Regex Entry = new(@"^(\d+)\s+(.+)$", RegexOptions.CultureInvariant);

    /// <summary>Does not close the caller-owned reader. Invalid lines are diagnosed and skipped.</summary>
    public LocalizationParseResult Parse(TextReader reader, string source = "<reader>")
    {
        ArgumentNullException.ThrowIfNull(reader);
        var sections = new HashSet<int>();
        var entries = new List<LocalizationEntry>();
        var diagnostics = new List<ResolverDiagnostic>();
        var seen = new Dictionary<(int, int), LocalizationEntry>();
        int? section = null;
        var lineNumber = 0;
        void Warn(string code, string message) => diagnostics.Add(new(code, DiagnosticSeverity.Warning, message, source, lineNumber));
        string? raw;
        while ((raw = reader.ReadLine()) is not null)
        {
            lineNumber++;
            var line = raw.Trim().TrimStart('\uFEFF');
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';') || line.StartsWith("//", StringComparison.Ordinal)) continue;
            if (line.StartsWith('['))
            {
                var header = Header.Match(line);
                if (!header.Success || !int.TryParse(header.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                {
                    Warn("MalformedSection", "Invalid numbered section; entries are ignored until the next valid section.");
                    section = null;
                    continue;
                }
                section = number;
                if (!sections.Add(number)) Warn("DuplicateSection", $"Section {number} repeated; its records are combined with first-entry-wins semantics.");
                continue;
            }
            if (section is null)
            {
                Warn("EntryWithoutSection", "Record outside a valid numbered section was ignored.");
                continue;
            }
            var match = Entry.Match(line);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || !match.Groups[2].Value.Contains('|'))
            {
                Warn("MalformedEntry", "Expected a nonnegative Int32 ID, whitespace, and pipe-delimited data.");
                continue;
            }
            var fields = match.Groups[2].Value.Split('|');
            var name = fields[0].Trim();
            if (name.Length == 0)
            {
                Warn("MalformedEntry", "Entry has an empty display name.");
                continue;
            }
            var entry = new LocalizationEntry(section.Value, id, name, Array.AsReadOnly(fields), lineNumber);
            if (seen.TryGetValue((section.Value, id), out var previous))
            {
                Warn(previous.Name == name ? "DuplicateEntry" : "ConflictingEntry",
                    $"Section {section} ID {id} repeated (first at line {previous.LineNumber}); first entry retained.");
                continue;
            }
            seen.Add((section.Value, id), entry);
            entries.Add(entry);
        }
        return new(sections.Order().ToArray(), entries.AsReadOnly(), diagnostics.AsReadOnly());
    }
}
