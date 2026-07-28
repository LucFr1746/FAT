using System.IO;
using System.Text.Json;
using Domain.Constants;

namespace Services.Import;

/// <summary>
/// Reads a single curriculum specification JSON file (such as BIT_SE_K19D_K20A.json).
/// </summary>
public sealed class SingleCurriculumJsonDataReader : IFlmDataReader
{
    public string SourceName => "Single Curriculum JSON (.json)";

    public bool CanRead(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) ||
            !string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var text = File.ReadAllText(path);
            return text.Contains("\"curriculum_info\"") && text.Contains("\"subjects\"");
        }
        catch
        {
            return false;
        }
    }

    public async Task<FlmDataSet> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Không tìm thấy tệp JSON: {path}", path);
        }

        using var stream = File.OpenRead(path);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;

        string curriculumCode = "BIT_SE_K19D_K20A";
        string curriculumName = "Software Engineering";

        if (root.TryGetProperty("curriculum_info", out var info))
        {
            if (info.TryGetProperty("code", out var codeElem))
            {
                curriculumCode = codeElem.GetString() ?? curriculumCode;
            }
            if (info.TryGetProperty("name_vi", out var nameViElem))
            {
                curriculumName = nameViElem.GetString() ?? curriculumName;
            }
            else if (info.TryGetProperty("name_en", out var nameEnElem))
            {
                curriculumName = nameEnElem.GetString() ?? curriculumName;
            }
        }

        var (majorCode, majorName) = FlmValueParser.MajorFromCurriculum(curriculumCode);
        if (string.IsNullOrWhiteSpace(majorName))
        {
            majorName = curriculumName;
        }

        var curricula = new List<FlmCurriculumRow>
        {
            new(majorCode, majorName)
        };

        var subjects = new List<FlmSubjectRow>();
        var assessments = new List<FlmAssessmentRow>();
        var seenAssessmentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (root.TryGetProperty("subjects", out var subjectsElem) && subjectsElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in subjectsElem.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!item.TryGetProperty("code", out var subjectCodeElem))
                {
                    continue;
                }

                var subjectCode = FlmValueParser.Clean(subjectCodeElem.GetString());
                if (subjectCode is null)
                {
                    continue;
                }

                int termNo = item.TryGetProperty("semester", out var semElem) ? semElem.GetInt32() : 1;
                int credit = item.TryGetProperty("credit", out var credElem) ? credElem.GetInt32() : 0;

                string? nameVi = item.TryGetProperty("name_vi", out var nVi) ? nVi.GetString() : null;
                string? nameEn = item.TryGetProperty("name_en", out var nEn) ? nEn.GetString() : null;
                var subjectName = FlmValueParser.Truncate(FlmValueParser.Clean(nameVi) ?? FlmValueParser.Clean(nameEn) ?? subjectCode, CatalogRules.CourseNameMaxLength)!;

                string? prereq = item.TryGetProperty("prerequisite", out var preElem) && preElem.ValueKind == JsonValueKind.String
                    ? FlmValueParser.Truncate(preElem.GetString(), CatalogRules.DescriptionMaxLength)
                    : null;

                bool countsGpa = credit > 0 && termNo > 0;

                subjects.Add(new FlmSubjectRow(
                    MajorCode: majorCode,
                    SubjectCode: subjectCode.ToUpperInvariant(),
                    SubjectName: subjectName,
                    TermNo: termNo,
                    Credits: credit,
                    CountsTowardGpa: countsGpa,
                    PrerequisiteText: prereq,
                    Description: null,
                    SyllabusCode: null,
                    MinAvgMarkToPass: null));

                if (item.TryGetProperty("assessment_plan", out var planElem) && planElem.ValueKind == JsonValueKind.Array)
                {
                    int displayOrder = 1;
                    foreach (var component in planElem.EnumerateArray())
                    {
                        if (!component.TryGetProperty("category", out var catElem))
                        {
                            continue;
                        }

                        var category = FlmValueParser.Truncate(catElem.GetString(), CatalogRules.AssessmentNameMaxLength);
                        if (category is null)
                        {
                            continue;
                        }

                        var key = $"{subjectCode}|{category}";
                        if (!seenAssessmentKeys.Add(key))
                        {
                            continue;
                        }

                        decimal weightPercent = component.TryGetProperty("weight_percent", out var wElem)
                            ? wElem.GetDecimal()
                            : 0m;

                        string? criteria = component.TryGetProperty("completion_criteria", out var cElem)
                            ? cElem.GetString()
                            : null;

                        string? typeStr = component.TryGetProperty("type", out var tElem)
                            ? tElem.GetString()
                            : null;

                        assessments.Add(new FlmAssessmentRow(
                            SubjectCode: subjectCode.ToUpperInvariant(),
                            Category: category,
                            Type: FlmValueParser.Truncate(typeStr, 100),
                            WeightPercent: weightPercent,
                            CompletionCriteria: criteria,
                            IsSubComponent: false,
                            DisplayOrder: displayOrder++));
                    }
                }
            }
        }

        return new FlmDataSet(curricula, subjects, assessments, [], []);
    }
}
