using Domain.Constants;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Services.Dtos;
using Services.Import;

namespace Services.Implementations;

/// <summary>
/// The per-entity upserts of the FLM import.
///
/// Every method here follows the same shape: load the existing rows for the keys
/// in the file ONCE into a dictionary, then match in memory. Querying per row
/// would mean tens of thousands of round trips for a file this size - the
/// classic N+1 - and would turn a two-second import into several minutes.
/// </summary>
public sealed partial class FlmImportService
{
    /// <summary>
    /// Matches how SQL Server actually compares these pairs: the database's
    /// default collation (SQL_Latin1_General_CP1_CI_AS) is case-INSENSITIVE, so
    /// UQ_Assessment_Name and UQ_SubjectMaterial_Title treat "Final Exam" and
    /// "Final exam" as the same row. A plain (int, string) tuple key does not -
    /// it uses ordinal, case-SENSITIVE equality - so without this comparer the
    /// in-memory "does it already exist" check and the database's own unique
    /// constraint can disagree, and disagree in exactly the direction that
    /// turns a matching update into a duplicate-key insert.
    /// </summary>
    private sealed class CourseNamePairComparer : IEqualityComparer<(int CourseId, string Name)>
    {
        public static readonly CourseNamePairComparer Instance = new();

        public bool Equals((int CourseId, string Name) x, (int CourseId, string Name) y)
            => x.CourseId == y.CourseId && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((int CourseId, string Name) obj)
            => HashCode.Combine(obj.CourseId, obj.Name.ToUpperInvariant());
    }

    /// <summary>Creates or updates the programmes, returning code -&gt; MajorId.</summary>
    private async Task<Dictionary<string, int>> UpsertMajorsAsync(
        FlmDataSet data,
        ImportSession session,
        CancellationToken cancellationToken)
    {
        var codes = data.Subjects
            .Select(s => s.MajorCode)
            .Concat(data.Curricula.Select(c => c.MajorCode))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existing = await _db.Majors
            .Where(m => codes.Contains(m.MajorCode))
            .ToDictionaryAsync(m => m.MajorCode, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var code in codes)
        {
            var truncatedCode = code.Length > CatalogRules.MajorCodeMaxLength
                ? code[..CatalogRules.MajorCodeMaxLength]
                : code;

            if (existing.TryGetValue(truncatedCode, out var major))
            {
                session.Majors.CountSkipped();
            }
            else
            {
                major = new Major
                {
                    MajorCode = truncatedCode,
                    MajorName = data.Curricula
                        .FirstOrDefault(c => c.MajorCode.Equals(code, StringComparison.OrdinalIgnoreCase))?.MajorName
                        ?? truncatedCode,
                    // Placeholder values: CK_Major_Credit demands both be > 0 at
                    // insert time. MajorCreditCalculator replaces them with the
                    // real totals at the end of the import.
                    RequiredCredits = 1,
                    TotalTerms = 1,
                    IsActive = true
                };

                _db.Majors.Add(major);
                existing[truncatedCode] = major;
                session.Majors.CountCreated();
            }

            result[code] = major.MajorId;
        }

        // Flush so the new majors have real identity values before the
        // curriculum links below reference them.
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var code in codes)
        {
            var truncatedCode = code.Length > CatalogRules.MajorCodeMaxLength
                ? code[..CatalogRules.MajorCodeMaxLength]
                : code;
            result[code] = existing[truncatedCode].MajorId;
        }

        return result;
    }

    /// <summary>
    /// Creates or updates the subjects, returning code -&gt; CourseId.
    ///
    /// One Course per subject code, even though the file lists the subject once
    /// per programme that teaches it. That is safe because the FLM data has no
    /// subject code with conflicting credits or GPA flag - only the TERM varies
    /// between programmes, and the term lives on the curriculum link.
    /// </summary>
    private async Task<Dictionary<string, int>> UpsertCoursesAsync(
        FlmDataSet data,
        ImportOptions options,
        ImportSession session,
        CancellationToken cancellationToken)
    {
        // Collapse the repeats, preferring the richest row: the one that
        // actually carries a description and the highest credit value.
        var bySubject = data.Subjects
            .Where(s => s.Credits >= CatalogRules.MinCredits && s.Credits <= CatalogRules.MaxCredits)
            .GroupBy(s => s.SubjectCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Code = g.Key,
                Best = g.OrderByDescending(s => s.Description is not null)
                        .ThenByDescending(s => s.PrerequisiteText is not null)
                        .ThenByDescending(s => s.Credits)
                        .First(),
                MaxCredits = g.Max(s => s.Credits)
            })
            .ToList();

        var codes = bySubject.Select(x => x.Code).ToList();

        var existing = await _db.Courses
            .Where(c => codes.Contains(c.CourseCode))
            .ToDictionaryAsync(c => c.CourseCode, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var item in bySubject)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var source = item.Best;

            if (existing.TryGetValue(item.Code, out var course))
            {
                if (!options.UpdateExisting)
                {
                    session.Subjects.CountSkipped();
                    continue;
                }

                var modified = ApplyCourseValues(course, source, item.MaxCredits);
                session.Subjects.CountUpsert(isNew: false, wasModified: modified);
                continue;
            }

            course = new Course { CourseCode = item.Code, IsActive = true };
            ApplyCourseValues(course, source, item.MaxCredits);

            _db.Courses.Add(course);
            existing[item.Code] = course;
            session.Subjects.CountCreated();
        }

        await _db.SaveChangesAsync(cancellationToken);

        return existing.ToDictionary(kv => kv.Key, kv => kv.Value.CourseId, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Copies the file's values onto a Course.
    /// Returns whether anything actually changed, so an unchanged row is
    /// reported as skipped rather than as a phantom update.
    /// </summary>
    private static bool ApplyCourseValues(Course course, FlmSubjectRow source, int credits)
    {
        var modified = false;

        void Set<T>(T current, T next, Action<T> assign)
        {
            if (!EqualityComparer<T>.Default.Equals(current, next))
            {
                assign(next);
                modified = true;
            }
        }

        Set(course.CourseName, source.SubjectName, v => course.CourseName = v);
        Set(course.Credits, credits, v => course.Credits = v);
        Set(course.CountsTowardGpa, source.CountsTowardGpa, v => course.CountsTowardGpa = v);

        // Only overwrite the optional fields when the file has something to say,
        // so a re-import from a sparser source does not wipe values an
        // administrator filled in by hand.
        if (source.Description is not null)
        {
            Set(course.Description, source.Description, v => course.Description = v);
        }

        if (source.PrerequisiteText is not null)
        {
            Set(course.PrerequisiteText, source.PrerequisiteText, v => course.PrerequisiteText = v);
        }

        if (source.SyllabusCode is not null)
        {
            Set(course.SyllabusCode, source.SyllabusCode, v => course.SyllabusCode = v);
        }

        if (source.MinAvgMarkToPass is not null)
        {
            Set(course.MinAvgMarkToPass, source.MinAvgMarkToPass, v => course.MinAvgMarkToPass = v);
        }

        return modified;
    }

    /// <summary>
    /// Creates any kỳ the file uses but the database does not have yet.
    ///
    /// Must run before the curriculum links: Curriculum.TermNo is a foreign key
    /// onto Term.TermNo, so a missing kỳ fails the whole insert.
    /// </summary>
    private async Task EnsureTermsAsync(FlmDataSet data, CancellationToken cancellationToken)
    {
        var termNos = data.Subjects
            .Select(s => s.TermNo)
            .Where(t => t >= CatalogRules.MinTermNo && t <= CatalogRules.MaxTermNo)
            .Distinct()
            .ToList();

        var existing = await _db.Terms
            .Where(t => termNos.Contains(t.TermNo))
            .Select(t => t.TermNo)
            .ToListAsync(cancellationToken);

        var missing = termNos.Except(existing).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        foreach (var termNo in missing)
        {
            _db.Terms.Add(new Term
            {
                TermNo = termNo,
                TermName = CatalogRules.GetTermName(termNo),
                IsActive = true
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Places each subject into each programme's study path.</summary>
    private async Task UpsertCurriculumLinksAsync(
        FlmDataSet data,
        IReadOnlyDictionary<string, int> majorsByCode,
        IReadOnlyDictionary<string, int> coursesByCode,
        ImportOptions options,
        ImportSession session,
        CancellationToken cancellationToken)
    {
        var majorIds = majorsByCode.Values.Distinct().ToList();

        var existing = await _db.CurriculumItems
            .Where(ci => majorIds.Contains(ci.MajorId))
            .ToDictionaryAsync(ci => (ci.MajorId, ci.CourseId), cancellationToken);

        // Position within the term follows the order the file lists them in,
        // which is the order the programme sheet was authored in.
        var orderPerTerm = new Dictionary<(int MajorId, int TermNo), int>();

        // The same subject can appear in several cohorts of one programme (e.g.
        // BIT_SE_K19 and BIT_SE_K20) that DISAGREE on the term. Once cohort codes
        // collapse to a single major, only one term can survive, so the FIRST
        // listing (the older cohort) wins and later duplicates are ignored - which
        // keeps the term stable instead of last-write-wins.
        var handledThisRun = new HashSet<(int MajorId, int CourseId)>();

        foreach (var row in data.Subjects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (row.TermNo < CatalogRules.MinTermNo || row.TermNo > CatalogRules.MaxTermNo)
            {
                session.CurriculumLinks.CountSkipped();
                continue;
            }

            if (!majorsByCode.TryGetValue(row.MajorCode, out var majorId) ||
                !coursesByCode.TryGetValue(row.SubjectCode, out var courseId))
            {
                session.CurriculumLinks.CountSkipped();
                continue;
            }

            if (!handledThisRun.Add((majorId, courseId)))
            {
                session.CurriculumLinks.CountSkipped();
                continue;
            }

            orderPerTerm.TryGetValue((majorId, row.TermNo), out var order);
            orderPerTerm[(majorId, row.TermNo)] = order + 1;

            if (existing.TryGetValue((majorId, courseId), out var link))
            {
                if (!options.UpdateExisting)
                {
                    session.CurriculumLinks.CountSkipped();
                    continue;
                }

                var modified = link.TermNo != row.TermNo || link.DisplayOrder != order;
                link.TermNo = row.TermNo;
                link.DisplayOrder = order;
                session.CurriculumLinks.CountUpsert(isNew: false, wasModified: modified);
                continue;
            }

            var created = new Curriculum
            {
                MajorId = majorId,
                CourseId = courseId,
                TermNo = row.TermNo,
                DisplayOrder = order,
                IsMandatory = true
            };

            _db.CurriculumItems.Add(created);
            existing[(majorId, courseId)] = created;
            session.CurriculumLinks.CountCreated();
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Creates or updates the grade structure of each subject.</summary>
    private async Task UpsertAssessmentsAsync(
        FlmDataSet data,
        IReadOnlyDictionary<string, int> coursesByCode,
        ImportOptions options,
        ImportSession session,
        CancellationToken cancellationToken)
    {
        // Sub-components are detail rows under a top-level component; importing
        // them as components of their own would push the weight total past 100%.
        var rows = data.Assessments.Where(a => !a.IsSubComponent).ToList();
        var courseIds = rows
            .Where(a => coursesByCode.ContainsKey(a.SubjectCode))
            .Select(a => coursesByCode[a.SubjectCode])
            .Distinct()
            .ToList();

        var existing = await _db.Assessments
            .Where(a => courseIds.Contains(a.CourseId))
            .ToDictionaryAsync(a => (a.CourseId, a.Name), CourseNamePairComparer.Instance, cancellationToken);

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!coursesByCode.TryGetValue(row.SubjectCode, out var courseId))
            {
                session.Assessments.CountSkipped();
                continue;
            }

            var weight = Math.Round(
                row.WeightPercent / 100m, CatalogRules.AssessmentWeightDecimals, MidpointRounding.AwayFromZero);

            // CK_Assessment_Weight demands 0 < Weight <= 1; a zero-weight row
            // would abort the whole transaction, so drop it with a warning.
            if (weight <= 0m || weight > 1m)
            {
                session.Warn($"Bỏ qua cột điểm '{row.Category}' của môn {row.SubjectCode}: " +
                             $"trọng số {row.WeightPercent:0.#}% không hợp lệ.");
                session.Assessments.CountSkipped();
                continue;
            }

            var minScore = FlmValueParser.ParseCompletionCriteria(row.CompletionCriteria);

            if (existing.TryGetValue((courseId, row.Category), out var assessment))
            {
                if (!options.UpdateExisting)
                {
                    session.Assessments.CountSkipped();
                    continue;
                }

                var modified = assessment.Weight != weight
                               || assessment.PartCount != row.PartCount
                               || assessment.MinScoreToPass != minScore
                               || assessment.DisplayOrder != row.DisplayOrder;

                assessment.Weight = weight;
                assessment.PartCount = row.PartCount;
                assessment.MinScoreToPass = minScore;
                assessment.DisplayOrder = row.DisplayOrder;
                session.Assessments.CountUpsert(isNew: false, wasModified: modified);
                continue;
            }

            var created = new Assessment
            {
                CourseId = courseId,
                Name = row.Category,
                Weight = weight,
                PartCount = row.PartCount,
                MinScoreToPass = minScore,
                DisplayOrder = row.DisplayOrder
            };

            _db.Assessments.Add(created);
            existing[(courseId, row.Category)] = created;
            session.Assessments.CountCreated();
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Creates or updates the syllabus bibliography of each subject.</summary>
    private async Task UpsertMaterialsAsync(
        FlmDataSet data,
        IReadOnlyDictionary<string, int> coursesByCode,
        ImportOptions options,
        ImportSession session,
        CancellationToken cancellationToken)
    {
        var courseIds = data.Materials
            .Where(m => coursesByCode.ContainsKey(m.SubjectCode))
            .Select(m => coursesByCode[m.SubjectCode])
            .Distinct()
            .ToList();

        var existing = await _db.SubjectMaterials
            .Where(m => courseIds.Contains(m.CourseId))
            .ToDictionaryAsync(m => (m.CourseId, m.Title), CourseNamePairComparer.Instance, cancellationToken);

        foreach (var row in data.Materials)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!coursesByCode.TryGetValue(row.SubjectCode, out var courseId))
            {
                session.Materials.CountSkipped();
                continue;
            }

            if (existing.TryGetValue((courseId, row.Title), out var material))
            {
                if (!options.UpdateExisting)
                {
                    session.Materials.CountSkipped();
                    continue;
                }

                var modified = material.Url != row.Url
                               || material.Author != row.Author
                               || material.Publisher != row.Publisher
                               || material.Isbn != row.Isbn;

                material.Url = row.Url;
                material.Author = row.Author;
                material.Publisher = row.Publisher;
                material.Isbn = row.Isbn;
                material.Description ??= row.Note;
                session.Materials.CountUpsert(isNew: false, wasModified: modified);
                continue;
            }

            var created = new SubjectMaterial
            {
                CourseId = courseId,
                Title = row.Title,
                Description = row.Note,
                Url = row.Url,
                Author = row.Author,
                Publisher = row.Publisher,
                Isbn = row.Isbn,
                DisplayOrder = row.DisplayOrder,
                IsActive = true
            };

            _db.SubjectMaterials.Add(created);
            existing[(courseId, row.Title)] = created;
            session.Materials.CountCreated();
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Creates or updates the assessment timeline of each subject.</summary>
    private async Task UpsertSchedulesAsync(
        FlmDataSet data,
        IReadOnlyDictionary<string, int> coursesByCode,
        ImportOptions options,
        ImportSession session,
        CancellationToken cancellationToken)
    {
        var courseIds = data.Schedules
            .Where(s => coursesByCode.ContainsKey(s.SubjectCode))
            .Select(s => coursesByCode[s.SubjectCode])
            .Distinct()
            .ToList();

        var existing = await _db.AssessmentSchedules
            .Where(s => courseIds.Contains(s.CourseId))
            .ToDictionaryAsync(s => (s.CourseId, s.SessionNo), cancellationToken);

        foreach (var row in data.Schedules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!coursesByCode.TryGetValue(row.SubjectCode, out var courseId))
            {
                session.Schedules.CountSkipped();
                continue;
            }

            var weekNo = CatalogRules.GetWeekNo(row.SessionNo);

            if (existing.TryGetValue((courseId, row.SessionNo), out var schedule))
            {
                if (!options.UpdateExisting)
                {
                    session.Schedules.CountSkipped();
                    continue;
                }

                var modified = schedule.Title != row.Title || schedule.TeachingType != row.TeachingType;

                schedule.Title = row.Title;
                schedule.TeachingType = row.TeachingType;
                schedule.WeekNo = weekNo;
                // ExpectedDate is deliberately left alone: FLM has no dates, and
                // overwriting a date an administrator entered would undo their work.
                session.Schedules.CountUpsert(isNew: false, wasModified: modified);
                continue;
            }

            var created = new AssessmentSchedule
            {
                CourseId = courseId,
                SessionNo = row.SessionNo,
                WeekNo = weekNo,
                Title = row.Title,
                TeachingType = row.TeachingType
            };

            _db.AssessmentSchedules.Add(created);
            existing[(courseId, row.SessionNo)] = created;
            session.Schedules.CountCreated();
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Parses the free-text prerequisite column into Prerequisite rows.
    ///
    /// Adds only; never deletes. An administrator may have corrected a
    /// prerequisite the parser could not read, and a re-import must not throw
    /// that correction away.
    /// </summary>
    private async Task UpsertPrerequisitesAsync(
        FlmDataSet data,
        IReadOnlyDictionary<string, int> coursesByCode,
        ImportSession session,
        CancellationToken cancellationToken)
    {
        var withText = data.Subjects
            .Where(s => !string.IsNullOrWhiteSpace(s.PrerequisiteText))
            .GroupBy(s => s.SubjectCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (withText.Count == 0)
        {
            return;
        }

        var courseIds = withText
            .Where(s => coursesByCode.ContainsKey(s.SubjectCode))
            .Select(s => coursesByCode[s.SubjectCode])
            .ToList();

        var existingPairs = (await _db.Prerequisites
                .Where(p => courseIds.Contains(p.CourseId))
                .Select(p => new { p.CourseId, p.RequiredCourseId })
                .ToListAsync(cancellationToken))
            .Select(p => (p.CourseId, p.RequiredCourseId))
            .ToHashSet();

        // The cycle check needs the WHOLE graph, not just the courses in this
        // file: a new edge can close a loop through a course the import never
        // touches.
        var allEdges = (await _db.Prerequisites
                .Select(p => new { p.CourseId, p.RequiredCourseId })
                .ToListAsync(cancellationToken))
            .Select(p => (p.CourseId, p.RequiredCourseId))
            .ToList();

        foreach (var subject in withText)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!coursesByCode.TryGetValue(subject.SubjectCode, out var courseId))
            {
                continue;
            }

            var parsed = PrerequisiteTextParser.Parse(subject.PrerequisiteText);

            if (!parsed.HasRequirements)
            {
                // No codes at all - a prose rule. Course.PrerequisiteText still
                // holds the sentence, so nothing is lost.
                if (parsed.OriginalText is not null)
                {
                    session.Warn($"Môn {subject.SubjectCode}: không nhận dạng được mã môn tiên quyết " +
                                 $"từ \"{Shorten(parsed.OriginalText)}\" - đã lưu nguyên văn để đối chiếu thủ công.");
                }

                continue;
            }

            if (!parsed.IsFullyParsed)
            {
                session.Warn($"Môn {subject.SubjectCode}: điều kiện tiên quyết chỉ được nhận dạng một phần " +
                             $"từ \"{Shorten(parsed.OriginalText!)}\".");
            }

            // GroupNo numbering: 0 for a plain requirement, and a distinct
            // positive number for each set of alternatives on this course.
            var choiceGroupNo = 0;

            foreach (var group in parsed.Groups)
            {
                var groupNo = group.IsChoice ? ++choiceGroupNo : 0;

                foreach (var requiredCode in group.Alternatives)
                {
                    if (!coursesByCode.TryGetValue(requiredCode, out var requiredCourseId))
                    {
                        session.Warn($"Môn {subject.SubjectCode}: không tìm thấy môn tiên quyết " +
                                     $"'{requiredCode}' trong dữ liệu.");
                        session.Prerequisites.CountSkipped();
                        continue;
                    }

                    if (requiredCourseId == courseId ||
                        !existingPairs.Add((courseId, requiredCourseId)))
                    {
                        session.Prerequisites.CountSkipped();
                        continue;
                    }

                    if (PrerequisiteGraph.WouldCreateCycle(allEdges, courseId, requiredCourseId))
                    {
                        session.Warn($"Bỏ qua điều kiện '{subject.SubjectCode} cần {requiredCode}': " +
                                     "sẽ tạo thành vòng lặp trong cây môn tiên quyết.");
                        session.Prerequisites.CountSkipped();
                        continue;
                    }

                    _db.Prerequisites.Add(new Prerequisite
                    {
                        CourseId = courseId,
                        RequiredCourseId = requiredCourseId,
                        Type = Domain.Enums.PrerequisiteType.Prerequisite,
                        GroupNo = groupNo
                    });

                    allEdges.Add((courseId, requiredCourseId));
                    session.Prerequisites.CountCreated();
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Keeps a quoted source value short enough to read in a warning list.</summary>
    private static string Shorten(string value)
        => value.Length <= 60 ? value : value[..60] + "...";
}
