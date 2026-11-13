using System.Text;
using System.Text.Json;

namespace GalacticIntelligence.GameData;

/// <summary>Loads explicit paths with read-only streams. Data errors are diagnostics; programmer configuration errors throw.</summary>
public static class ResolverLoader
{
    public static IdentifierRegistry Load(ResolverOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var mappings = new List<IdentifierMapping>();
        var diagnostics = new List<ResolverDiagnostic>();
        foreach (var input in options.LocalizationFiles)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(input.Path);
            foreach (var binding in input.Sections)
            {
                ArgumentNullException.ThrowIfNull(binding.Category);
                if (binding.Section < 0) throw new ArgumentOutOfRangeException(nameof(options), "Section numbers must be nonnegative.");
            }
            Read(input.Path, diagnostics, reader =>
            {
                var parsed = new LocalizationParser().Parse(reader, input.Path);
                diagnostics.AddRange(parsed.Diagnostics);
                foreach (var binding in input.Sections)
                {
                    if (!parsed.Sections.Contains(binding.Section))
                        diagnostics.Add(new("MissingSection", DiagnosticSeverity.Warning, $"Configured section {binding.Section} is absent.", input.Path, Category: binding.Category));
                    foreach (var entry in parsed.Entries.Where(e => e.Section == binding.Section))
                        mappings.Add(new(binding.Category, entry.Id, entry.Name,
                            new(SourceKind.VanillaLocalization, input.Path, entry.Section, entry.LineNumber), binding.CallerValidated));
                }
            });
        }
        foreach (var input in options.OverrideFiles)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(input.Path);
            if (input.Kind is not (SourceKind.ModOverride or SourceKind.UserOverride))
                throw new ArgumentException("Override files must be mod or user overrides.", nameof(options));
            Read(input.Path, diagnostics, reader => LoadOverrides(reader, input, mappings, diagnostics));
        }
        return new(mappings, diagnostics);
    }

    private static void Read(string path, List<ResolverDiagnostic> diagnostics, Action<TextReader> action)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
            action(reader);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DecoderFallbackException or JsonException or ArgumentException or NotSupportedException)
        {
            diagnostics.Add(new(error switch
            {
                FileNotFoundException or DirectoryNotFoundException => "FileNotFound",
                JsonException => "InvalidJson",
                DecoderFallbackException => "InvalidEncoding",
                _ => "FileReadError"
            }, DiagnosticSeverity.Error, error.Message, path));
        }
    }

    private static void LoadOverrides(TextReader reader, OverrideInput input, List<IdentifierMapping> mappings, List<ResolverDiagnostic> diagnostics)
    {
        using var document = JsonDocument.Parse(reader.ReadToEnd());
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var version) || !version.TryGetInt32Safe(out var number) || number != 1)
        {
            diagnostics.Add(new("UnsupportedOverrideSchema", DiagnosticSeverity.Error, "Expected schemaVersion 1; the entire override file was ignored.", input.Path));
            return;
        }
        if (!root.TryGetProperty("mappings", out var records) || records.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(new("InvalidOverrideDocument", DiagnosticSeverity.Error, "Expected a mappings array; the entire override file was ignored.", input.Path));
            return;
        }
        var index = 0;
        foreach (var record in records.EnumerateArray())
        {
            index++;
            if (record.ValueKind != JsonValueKind.Object ||
                !record.TryGetProperty("category", out var category) || category.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(category.GetString()) ||
                !record.TryGetProperty("id", out var id) || !id.TryGetInt32Safe(out var numericId) ||
                !record.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(name.GetString()) ||
                (record.TryGetProperty("callerValidated", out var validated) && validated.ValueKind is not (JsonValueKind.True or JsonValueKind.False)))
            {
                diagnostics.Add(new("MalformedOverride", DiagnosticSeverity.Warning, $"Mapping #{index} requires category, Int32 id, nonempty name, and optional boolean callerValidated; record ignored.", input.Path));
                continue;
            }
            mappings.Add(new(new(category.GetString()!), numericId, name.GetString()!, new(input.Kind, input.Path),
                validated.ValueKind == JsonValueKind.True));
        }
    }

    private static bool TryGetInt32Safe(this JsonElement element, out int value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value);
    }
}
