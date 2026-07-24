using System.Text.Json;

namespace FAT.Services.Import;

/// <summary>
/// Reads a JSON array of flat objects into dictionaries keyed by property name.
///
/// The FLM export is a list of records with the same shape, so this only needs
/// to handle "array of objects with string values" - no nested arrays, no
/// numbers-as-numbers. Every value is read as a string so the rest of the
/// import pipeline (<see cref="FlmValueParser"/>) does not need two code paths
/// for "value from CSV" vs "value from JSON".
/// </summary>
public static class JsonFile
{
    /// <summary>Lookups are case-insensitive so a property that arrives as "SubjectCode" or "subjectcode" both resolve.</summary>
    public static async Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var rows = new List<IReadOnlyDictionary<string, string>>(document.RootElement.GetArrayLength());

        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                row[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Null => string.Empty,
                    _ => property.Value.GetRawText(),
                };
            }

            rows.Add(row);
        }

        return rows;
    }
}
