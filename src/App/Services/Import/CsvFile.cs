using System.Text;

namespace Services.Import;

/// <summary>
/// A minimal RFC 4180 CSV reader.
///
/// Hand-written rather than taken from a package because the need is narrow -
/// read a header row, then rows keyed by column name - and the FLM files use
/// exactly two awkward features, both of which are handled here: quoted fields
/// containing commas, and quoted fields containing NEWLINES (subject
/// descriptions run to several paragraphs). A naive String.Split would tear
/// those records apart.
/// </summary>
public static class CsvFile
{
    /// <summary>
    /// Reads a CSV into dictionaries keyed by header name.
    ///
    /// Lookups are case-insensitive so a header that arrives as "SubjectCode"
    /// or "subjectcode" both resolve.
    /// </summary>
    public static async Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        // detectEncodingFromByteOrderMarks strips the UTF-8 BOM that Excel
        // writes; without it the first header key would be "﻿curriculum"
        // and every lookup on that column would miss.
        var content = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);
        var records = ParseRecords(content);

        if (records.Count == 0)
        {
            return [];
        }

        var headers = records[0];
        var rows = new List<IReadOnlyDictionary<string, string>>(records.Count - 1);

        for (var i = 1; i < records.Count; i++)
        {
            var fields = records[i];

            // A trailing newline yields one empty record; it is not a row.
            if (fields.Count == 1 && string.IsNullOrWhiteSpace(fields[0]))
            {
                continue;
            }

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Count; c++)
            {
                row[headers[c].Trim().TrimStart('﻿')] = c < fields.Count ? fields[c] : string.Empty;
            }

            rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    /// Splits raw text into records of fields, honouring quotes.
    ///
    /// A single pass over the characters with an "inside quotes" flag; that
    /// flag is what lets a newline sit inside a field without ending the record.
    /// </summary>
    private static List<List<string>> ParseRecords(string content)
    {
        var records = new List<List<string>>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < content.Length; i++)
        {
            var ch = content[i];

            if (inQuotes)
            {
                if (ch != '"')
                {
                    field.Append(ch);
                    continue;
                }

                // A doubled quote inside a quoted field is one literal quote.
                if (i + 1 < content.Length && content[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                    continue;
                }

                inQuotes = false;
                continue;
            }

            switch (ch)
            {
                case '"':
                    inQuotes = true;
                    break;

                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    break;

                case '\r':
                    // Swallowed; the \n that follows ends the record.
                    break;

                case '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    records.Add(fields);
                    fields = [];
                    break;

                default:
                    field.Append(ch);
                    break;
            }
        }

        // Whatever is left when the text ends is the final record.
        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            records.Add(fields);
        }

        return records;
    }
}
