using Data;
using Domain.Constants;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Services.Abstractions;
using Services.Dtos;

namespace Services.Implementations;

/// <summary>
/// The student's own curriculum view, and the retake flow.
/// See <see cref="IStudentCurriculumService"/> for the hide-don't-disable rule.
/// </summary>
public sealed class StudentCurriculumService : IStudentCurriculumService
{
    private readonly FAT_DBContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IPrerequisiteService _prerequisiteService;

    public StudentCurriculumService(
        FAT_DBContext db,
        ICurrentUserContext currentUser,
        IPrerequisiteService prerequisiteService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _prerequisiteService = prerequisiteService ?? throw new ArgumentNullException(nameof(prerequisiteService));
    }

    public async Task<StudentTermCurriculumDto> GetTermCurriculumAsync(
        int studentId, int termNo, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "Xem chương trình học");

        var student = await _db.Students
            .AsNoTracking()
            .Where(s => s.StudentId == studentId)
            .Select(s => new
            {
                s.MajorId,
                s.CurrentTermNo,
                s.CurrentSemester,
                MajorCode = s.Major != null ? s.Major.MajorCode : "",
                MajorName = s.Major != null ? s.Major.MajorName : ""
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy sinh viên có mã định danh {studentId}.");

        if (termNo <= 0)
        {
            if (student.CurrentTermNo.HasValue && student.CurrentTermNo.Value > 0)
            {
                termNo = student.CurrentTermNo.Value;
            }
            else if (!string.IsNullOrWhiteSpace(student.CurrentSemester) &&
                     student.CurrentSemester.StartsWith("Kỳ ", StringComparison.OrdinalIgnoreCase) &&
                     int.TryParse(student.CurrentSemester.Substring(3).Trim(), out var parsedTerm) &&
                     parsedTerm >= 1 && parsedTerm <= 9)
            {
                termNo = parsedTerm;
            }
            else
            {
                var maxEnrolledTerm = await (
                    from e in _db.Enrollments.AsNoTracking()
                    where e.StudentId == studentId
                    join ci in _db.CurriculumItems.AsNoTracking() on new { MajorId = student.MajorId, e.CourseId } equals new { ci.MajorId, ci.CourseId }
                    select (int?)ci.TermNo
                ).MaxAsync(cancellationToken);

                termNo = (maxEnrolledTerm.HasValue && maxEnrolledTerm.Value > 0)
                    ? Math.Min(maxEnrolledTerm.Value, CatalogRules.MaxTermNo)
                    : 1;
            }
        }

        var termName = await _db.Terms
            .AsNoTracking()
            .Where(t => t.TermNo == termNo)
            .Select(t => t.TermName)
            .FirstOrDefaultAsync(cancellationToken) ?? CatalogRules.GetTermName(termNo);

        var items = await _db.CurriculumItems
            .AsNoTracking()
            .Where(ci => ci.MajorId == student.MajorId && ci.TermNo == termNo && (ci.Course == null || ci.Course.IsActive))
            .OrderBy(ci => ci.DisplayOrder)
            .ThenBy(ci => ci.Course != null ? ci.Course.CourseCode : "")
            .Select(ci => new
            {
                ci.CourseId,
                CourseCode = ci.Course != null ? ci.Course.CourseCode : "",
                CourseName = ci.Course != null ? ci.Course.CourseName : "",
                Credits = ci.Course != null ? ci.Course.Credits : 0,
                CountsTowardGpa = ci.Course != null && ci.Course.CountsTowardGpa,
                Description = ci.Course != null ? ci.Course.Description : null,
                PrerequisiteText = ci.Course != null ? ci.Course.PrerequisiteText : null,
                ci.TermNo,
                ci.IsMandatory,
                MaterialCount = ci.Course != null ? ci.Course.SubjectMaterials.Count(m => m.IsActive) : 0,
                AssessmentCount = ci.Course != null ? ci.Course.Assessments.Count : 0,
                PrerequisiteCodes = ci.Course != null
                    ? ci.Course.Prerequisites.Select(p => p.RequiredCourse != null ? p.RequiredCourse.CourseCode : "").ToList()
                    : new List<string>()
            })
            .ToListAsync(cancellationToken);

        // Compute overall major progress
        var majorCourses = await _db.CurriculumItems
            .AsNoTracking()
            .Where(ci => ci.MajorId == student.MajorId && (ci.Course == null || ci.Course.IsActive))
            .Select(ci => new { ci.CourseId, Credits = ci.Course != null ? ci.Course.Credits : 0 })
            .ToListAsync(cancellationToken);

        var totalSubjects = majorCourses.Count;
        var totalCredits = majorCourses.Sum(c => c.Credits);

        var passedEnrollments = await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Passed)
            .Select(e => new { e.CourseId, Credits = e.Course != null ? e.Course.Credits : 0 })
            .ToListAsync(cancellationToken);

        var completedSubjects = passedEnrollments.Select(e => e.CourseId).Distinct().Count();
        var completedCredits = passedEnrollments.Sum(e => e.Credits);
        var progressPercentage = totalCredits > 0
            ? Math.Round((double)completedCredits / totalCredits * 100, 1)
            : 0.0;

        var progress = new CurriculumProgressDto(
            CompletedSubjects: completedSubjects,
            TotalSubjects: totalSubjects,
            CompletedCredits: completedCredits,
            TotalCredits: totalCredits,
            ProgressPercentage: progressPercentage);

        if (items.Count == 0)
        {
            return new StudentTermCurriculumDto(
                student.MajorId, student.MajorCode, student.MajorName, termNo, termName, [], 0, [], progress);
        }

        var courseIds = items.Select(i => i.CourseId).ToList();

        // One batched call, not one per subject.
        var eligibility = await _prerequisiteService.CanEnrollManyAsync(
            studentId, courseIds, cancellationToken);

        var myEnrollments = await GetMyStandingAsync(studentId, courseIds, cancellationToken);

        var visible = new List<StudentSubjectDto>();
        var hiddenCodes = new List<string>();

        foreach (var item in items)
        {
            myEnrollments.TryGetValue(item.CourseId, out var standing);

            var canTake = !eligibility.TryGetValue(item.CourseId, out var check) || check.CanEnroll;
            var alreadyEngaged = standing is not null;

            // All subjects remain visible in the term list. Subjects with unmet prerequisites
            // are marked with IsEligible = false so they display greyed out in the UI.
            var isEligible = canTake || alreadyEngaged;

            if (!isEligible)
            {
                hiddenCodes.Add(item.CourseCode);
            }

            visible.Add(new StudentSubjectDto(
                CourseId: item.CourseId,
                CourseCode: item.CourseCode,
                CourseName: item.CourseName,
                Credits: item.Credits,
                CountsTowardGpa: item.CountsTowardGpa,
                TermNo: item.TermNo,
                IsMandatory: item.IsMandatory,
                Description: item.Description,
                PrerequisiteText: item.PrerequisiteText,
                PrerequisiteCodes: item.PrerequisiteCodes,
                MyStatus: standing?.Status,
                MyFinalScore: standing?.FinalScore,
                MyAttemptCount: standing?.AttemptCount ?? 0,
                MaterialCount: item.MaterialCount,
                AssessmentCount: item.AssessmentCount,
                IsEligible: isEligible));
        }

        return new StudentTermCurriculumDto(
            student.MajorId,
            student.MajorCode,
            student.MajorName,
            termNo,
            termName,
            visible,
            0,
            [],
            progress);
    }

    public async Task<StudentSubjectDetailDto?> GetSubjectDetailAsync(
        int studentId, int courseId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "Xem chi tiết môn học");

        var course = await _db.Courses
            .AsNoTracking()
            .Where(c => c.CourseId == courseId)
            .Select(c => new
            {
                c.CourseId,
                c.CourseCode,
                c.CourseName,
                c.Credits,
                c.CountsTowardGpa,
                c.Description,
                c.PrerequisiteText,
                PrerequisiteCodes = c.Prerequisites.Select(p => p.RequiredCourse!.CourseCode).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (course is null)
        {
            return null;
        }

        var termNo = await _db.CurriculumItems
            .AsNoTracking()
            .Where(ci => ci.CourseId == courseId && ci.Major!.Students.Any(s => s.StudentId == studentId))
            .Select(ci => (int?)ci.TermNo)
            .FirstOrDefaultAsync(cancellationToken);

        var schedulePartCounts = (await _db.AssessmentSchedules
            .AsNoTracking()
            .Where(s => s.CourseId == courseId && s.AssessmentId != null)
            .GroupBy(s => s.AssessmentId!.Value)
            .Select(g => new { AssessmentId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken))
            .ToDictionary(g => g.AssessmentId, g => g.Count);

        var gradeStructureRaw = await _db.Assessments
            .AsNoTracking()
            .Where(a => a.CourseId == courseId)
            .OrderBy(a => a.DisplayOrder)
            .Select(a => new
            {
                a.AssessmentId, a.CourseId, a.Name, a.Weight, a.MinScoreToPass, a.DisplayOrder
            })
            .ToListAsync(cancellationToken);

        var gradeStructure = gradeStructureRaw
            .Select(a =>
            {
                schedulePartCounts.TryGetValue(a.AssessmentId, out var count);
                return new AssessmentDto(
                    a.AssessmentId, a.CourseId, a.Name, a.Weight, a.MinScoreToPass, a.DisplayOrder,
                    PartCount: count > 0 ? count : 1);
            })
            .ToList();

        var linkMaterials = await _db.SubjectMaterials
            .AsNoTracking()
            .Where(m => m.CourseId == courseId && m.IsActive)
            .OrderBy(m => m.DisplayOrder)
            .Select(m => new SubjectMaterialDto(
                m.SubjectMaterialId, m.CourseId, m.Title, m.Description, m.Url,
                m.Author, m.Publisher, m.Isbn, m.DisplayOrder, m.IsActive, null, null))
            .ToListAsync(cancellationToken);

        var fileMaterials = await _db.Materials
            .AsNoTracking()
            .Where(m => m.CourseId == courseId && m.IsActive && m.File != null)
            .OrderByDescending(m => m.UploadedAt)
            .Select(m => new SubjectMaterialDto(
                0, m.CourseId!.Value, m.Title, m.Description, null,
                null, null, null, 99, m.IsActive, m.MaterialId, m.FileName))
            .ToListAsync(cancellationToken);

        var materials = linkMaterials.Concat(fileMaterials).ToList();

        var schedule = await _db.AssessmentSchedules
            .AsNoTracking()
            .Where(s => s.CourseId == courseId)
            .OrderBy(s => s.SessionNo)
            .Select(s => new AssessmentScheduleDto(
                s.AssessmentScheduleId, s.CourseId, s.SessionNo, s.WeekNo, s.Title,
                s.Description, s.ExpectedDate, s.TeachingType,
                s.AssessmentId, s.Assessment != null ? s.Assessment.Name : null))
            .ToListAsync(cancellationToken);

        var standing = await GetMyStandingAsync(studentId, [courseId], cancellationToken);
        standing.TryGetValue(courseId, out var mine);

        var check = await _prerequisiteService.CanEnrollAsync(studentId, courseId, cancellationToken);

        var subject = new StudentSubjectDto(
            course.CourseId, course.CourseCode, course.CourseName, course.Credits,
            course.CountsTowardGpa, termNo ?? 0, IsMandatory: true,
            course.Description, course.PrerequisiteText, course.PrerequisiteCodes,
            mine?.Status, mine?.FinalScore, mine?.AttemptCount ?? 0,
            materials.Count, gradeStructure.Count);

        return new StudentSubjectDetailDto(
            subject,
            gradeStructure,
            materials,
            schedule,
            check,
            GradeStructureValidationDto.FromWeights(courseId, gradeStructure.Sum(a => a.Weight)));
    }

    public async Task SetMajorAsync(
        int studentId, int majorId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "Chọn ngành học");

        var student = await _db.Students.FirstOrDefaultAsync(s => s.StudentId == studentId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy sinh viên có mã định danh {studentId}.");

        var major = await _db.Majors.FirstOrDefaultAsync(m => m.MajorId == majorId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy ngành học có mã định danh {majorId}.");

        if (!major.IsActive)
        {
            throw new InvalidOperationException($"Ngành '{major.MajorCode}' hiện không còn hoạt động.");
        }

        if (student.MajorId != majorId)
        {
            student.MajorId = majorId;

            // The old kỳ belongs to the old programme's study path and means
            // nothing in the new one, so it is cleared rather than carried over.
            student.CurrentTermNo = null;
            student.CurrentSemester = null;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetCurrentTermAsync(
        int studentId, int termNo, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "Chọn kỳ học hiện tại");

        if (termNo < CatalogRules.MinTermNo || termNo > CatalogRules.MaxTermNo)
        {
            throw new ArgumentException(
                $"Kỳ học phải nằm trong khoảng {CatalogRules.MinTermNo} đến {CatalogRules.MaxTermNo}.",
                nameof(termNo));
        }

        var student = await _db.Students.FirstOrDefaultAsync(s => s.StudentId == studentId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy sinh viên có mã định danh {studentId}.");

        var term = await _db.Terms.FirstOrDefaultAsync(t => t.TermNo == termNo, cancellationToken);

        student.CurrentTermNo = termNo;

        // Both columns are written HERE, in one place, so the Profile screen -
        // which binds to the string - never disagrees with the curriculum
        // screen, which reads the number.
        student.CurrentSemester = term?.TermName ?? CatalogRules.GetTermName(termNo);

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RetakeCandidateDto>> GetRetakeCandidatesAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "Xem danh sách môn học lại");

        var attempts = await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId)
            .Select(e => new
            {
                e.CourseId,
                e.Course!.CourseCode,
                e.Course.CourseName,
                e.Course.Credits,
                e.Status,
                e.FinalScore,
                e.AttemptNo,
                e.Semester!.SemesterCode,
                e.Semester.DisplayOrder
            })
            .ToListAsync(cancellationToken);

        return attempts
            .GroupBy(a => new { a.CourseId, a.CourseCode, a.CourseName, a.Credits })
            // A subject passed on any attempt is finished, whatever happened
            // before it.
            .Where(g => g.Any(a => a.Status == EnrollmentStatus.Failed)
                        && !g.Any(a => a.Status == EnrollmentStatus.Passed)
                        && !g.Any(a => a.Status == EnrollmentStatus.Studying))
            .Select(g =>
            {
                var latest = g.OrderByDescending(a => a.DisplayOrder).First();

                return new RetakeCandidateDto(
                    g.Key.CourseId,
                    g.Key.CourseCode,
                    g.Key.CourseName,
                    g.Key.Credits,
                    latest.FinalScore,
                    g.Max(a => a.AttemptNo),
                    latest.SemesterCode);
            })
            .OrderBy(c => c.CourseCode)
            .ToList();
    }

    public async Task<int> AddRetakeAsync(
        int studentId, int courseId, int semesterId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "Đăng ký học lại");

        var course = await _db.Courses.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CourseId == courseId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy môn học có mã định danh {courseId}.");

        if (!await _db.Semesters.AnyAsync(s => s.SemesterId == semesterId, cancellationToken))
        {
            throw new InvalidOperationException($"Không tìm thấy học kỳ có mã định danh {semesterId}.");
        }

        var attempts = await _db.Enrollments
            .Where(e => e.StudentId == studentId && e.CourseId == courseId)
            .ToListAsync(cancellationToken);

        if (attempts.Count == 0)
        {
            throw new InvalidOperationException(
                $"Không thể học lại môn '{course.CourseCode}': sinh viên chưa từng đăng ký môn này.");
        }

        if (attempts.Any(a => a.Status == EnrollmentStatus.Passed))
        {
            throw new InvalidOperationException(
                $"Không thể học lại môn '{course.CourseCode}': sinh viên đã qua môn này.");
        }

        if (attempts.Any(a => a.Status == EnrollmentStatus.Studying))
        {
            throw new InvalidOperationException(
                $"Không thể học lại môn '{course.CourseCode}': môn này đang trong quá trình học.");
        }

        // UQ_Enrollment_Unique is on (StudentId, CourseId, SemesterId), so the
        // same subject cannot be failed and retaken inside ONE semester. Saying
        // so plainly beats letting a unique-index violation reach the user.
        if (attempts.Any(a => a.SemesterId == semesterId))
        {
            throw new InvalidOperationException(
                $"Môn '{course.CourseCode}' đã có lượt đăng ký trong học kỳ này. " +
                "Hãy chọn một học kỳ khác để đăng ký học lại.");
        }

        // Only the newest attempt may count toward the GPA - this is what stops
        // a retaken subject being averaged in twice.
        foreach (var attempt in attempts)
        {
            attempt.IsCounted = false;
            attempt.UpdatedAt = DateTime.UtcNow;
        }

        var retake = new Enrollment
        {
            StudentId = studentId,
            CourseId = courseId,
            SemesterId = semesterId,
            Status = EnrollmentStatus.Studying,
            IsCounted = true,
            AttemptNo = attempts.Max(a => a.AttemptNo) + 1,
            CreatedAt = DateTime.UtcNow
        };

        _db.Enrollments.Add(retake);

        // Demoting the old attempts and adding the new one is ONE unit of work.
        // Split apart, a failure in between leaves either two counted attempts
        // (inflated GPA) or none (the subject vanishes from it).
        await _db.SaveChangesAsync(cancellationToken);

        return retake.EnrollmentId;
    }

    /// <summary>
    /// The student's standing on a set of subjects, collapsed to one row each.
    ///
    /// A retaken subject is judged on its BEST outcome, so a later pass hides an
    /// earlier failure rather than the other way round.
    /// </summary>
    private async Task<Dictionary<int, StudentStanding>> GetMyStandingAsync(
        int studentId, IReadOnlyList<int> courseIds, CancellationToken cancellationToken)
    {
        var rows = await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && courseIds.Contains(e.CourseId))
            .Select(e => new { e.CourseId, e.Status, e.FinalScore, e.AttemptNo, e.IsCounted })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.CourseId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var best = g.FirstOrDefault(r => r.Status == EnrollmentStatus.Passed)
                               ?? g.FirstOrDefault(r => r.Status == EnrollmentStatus.Studying)
                               ?? g.OrderByDescending(r => r.AttemptNo).First();

                    return new StudentStanding(best.Status, best.FinalScore, g.Max(r => r.AttemptNo));
                });
    }

    private sealed record StudentStanding(EnrollmentStatus Status, decimal? FinalScore, int AttemptCount);
}
