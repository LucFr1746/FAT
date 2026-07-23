namespace FAT.Services.Import;

/// <summary>
/// Turns an FLM export on disk into a <see cref="FlmDataSet"/>.
///
/// Two implementations exist - the workbook and the CSV folder - and the import
/// service never knows which one it got. Adding a third source later means
/// adding one class here and nothing else.
/// </summary>
public interface IFlmDataReader
{
    /// <summary>Name shown in the UI, e.g. "Excel (.xlsx)".</summary>
    string SourceName { get; }

    /// <summary>Whether this reader handles the given file or folder.</summary>
    bool CanRead(string path);

    /// <summary>
    /// Reads the whole export into memory.
    ///
    /// Loading it all at once is deliberate: the largest source is a few
    /// megabytes, and the upsert has to resolve subjects before curriculum
    /// links anyway, so streaming would buy nothing and cost a second pass.
    /// </summary>
    Task<FlmDataSet> ReadAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>
/// Shared parsing helpers. Both readers face the same messy values - "10.0%",
/// "Có"/"Không", ">0" - so the conversions live here rather than being written
/// twice and drifting apart.
/// </summary>
public static class FlmValueParser
{
    /// <summary>
    /// Reads FLM's "Tính GPA" column. Anything that is not an explicit "Không"
    /// counts toward the GPA, because that is the normal case and a blank cell
    /// means "nothing special about this subject".
    /// </summary>
    public static bool ParseCountsGpa(string? value)
        => !string.Equals(value?.Trim(), "Không", StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads "10.0%" or "10" as the decimal 10.0. Returns 0 when unreadable.</summary>
    public static decimal ParsePercent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0m;
        }

        var cleaned = value.Replace("%", string.Empty).Replace(",", ".").Trim();

        // InvariantCulture on purpose: the file always uses a dot, but a
        // developer machine set to vi-VN would otherwise read "10.0" as 100.
        return decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result
            : 0m;
    }

    /// <summary>Reads an integer, tolerating blanks and stray text. Returns null when unreadable.</summary>
    public static int? ParseIntOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value.Trim(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    /// <summary>
    /// Reads the "Completion Criteria" column into a per-component minimum.
    ///
    /// ">0" means "just turn something in", which is NOT a minimum score - it
    /// must come back as null, otherwise every on-going component would gain a
    /// pass threshold it does not have.
    /// </summary>
    public static decimal? ParseCompletionCriteria(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith('>') || trimmed.StartsWith('≥'))
        {
            return null;
        }

        return decimal.TryParse(trimmed, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result)
            && result is >= 0 and <= 10
            ? result
            : null;
    }

    /// <summary>Trims and collapses a cell to null when it holds nothing useful.</summary>
    public static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    /// <summary>Shortens a value to fit a column, so an import never fails on a long cell.</summary>
    public static string? Truncate(string? value, int maxLength)
    {
        var cleaned = Clean(value);
        if (cleaned is null || cleaned.Length <= maxLength)
        {
            return cleaned;
        }

        return cleaned[..maxLength];
    }
}
