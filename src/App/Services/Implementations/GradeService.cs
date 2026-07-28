using Data;
using Domain.Constants;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Services.Abstractions;
using Services.Dtos;
using System.Text;

namespace Services.Implementations;

/// <summary>
/// Grade entry, final-score settlement and transcript aggregation.
/// All persisted values use the existing Enrollment, Assessment, Grade and
/// GradeScale tables; no calculated aggregate is stored outside Enrollment.
/// </summary>
public sealed class GradeService : IGradeService
{
    private const decimal MaximumScore = 10m;
    private static readonly Encoding StrictUtf8Encoding =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly Encoding Windows1252Encoding = CreateWindows1252Encoding();

    private readonly FAT_DBContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IPrerequisiteService _prerequisites;
    private readonly IGpaService _gpaService;

    public GradeService(
        FAT_DBContext db,
        ICurrentUserContext currentUser,
        IPrerequisiteService prerequisites,
        IGpaService gpaService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _prerequisites = prerequisites ?? throw new ArgumentNullException(nameof(prerequisites));
        _gpaService = gpaService ?? throw new ArgumentNullException(nameof(gpaService));
    }

    public async Task<TranscriptDto> GetTranscriptAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "Xem bảng điểm");

        var student = await _db.Students
            .AsNoTracking()
            .Where(s => s.StudentId == studentId)
            .Select(s => new
            {
                s.StudentId,
                s.StudentCode,
                s.FullName,
                MajorName = s.Major != null ? s.Major.MajorName : null
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                $"Không tìm thấy sinh viên có mã định danh {studentId}.");

        var rows = await GetStudentGradesAsync(studentId, cancellationToken);
        var semesterGpas = (await _gpaService.GetGpaBySemesterAsync(studentId, cancellationToken))
            .ToDictionary(s => s.SemesterId);

        var semesters = rows
            .Where(r => r.IsEnrolled)
            .GroupBy(r => new
            {
                r.SemesterId,
                r.SemesterCode,
                r.SemesterName,
                r.SemesterDisplayOrder
            })
            .OrderBy(g => g.Key.SemesterDisplayOrder)
            .Select(g =>
            {
                semesterGpas.TryGetValue(g.Key.SemesterId, out var semesterGpa);

                var items = g
                    .OrderBy(r => r.CourseCode)
                    .ThenBy(r => r.AttemptNo)
                    .Select(r => new TranscriptItemDto(
                        r.EnrollmentId,
                        r.CourseCode,
                        r.CourseName,
                        r.Credits,
                        r.FinalScore,
                        r.LetterGrade,
                        r.GradePoint,
                        r.Status,
                        r.IsCounted,
                        r.AttemptNo))
                    .ToList();

                return new SemesterTranscriptDto(
                    g.Key.SemesterId,
                    g.Key.SemesterCode,
                    g.Key.SemesterName,
                    g.Key.SemesterDisplayOrder,
                    IsCurrent: g.Any(r => r.SemesterIsCurrent),
                    items,
                    semesterGpa?.Gpa,
                    semesterGpa?.EarnedCredits ?? 0);
            })
            .ToList();

        return new TranscriptDto(
            student.StudentId,
            student.StudentCode,
            student.FullName,
            semesters,
            student.MajorName);
    }

    public async Task<IReadOnlyList<GradeCourseDto>> GetStudentGradesAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "Xem điểm");

        var student = await _db.Students
            .AsNoTracking()
            .Where(s => s.StudentId == studentId)
            .Select(s => new
            {
                s.StudentId,
                s.MajorId,
                RequiresComponentGrades = s.CurrentTermNo.HasValue
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                $"Không tìm thấy sinh viên có mã định danh {studentId}.");

        var curriculumItems = await _db.CurriculumItems
            .AsNoTracking()
            .AsSplitQuery()
            .Include(ci => ci.Term)
            .Include(ci => ci.Course)
            .Where(ci => ci.MajorId == student.MajorId
                         && ci.Term != null
                         && ci.Term.IsActive
                         && ci.Course != null
                         && ci.Course.IsActive)
            .OrderBy(ci => ci.TermNo)
            .ThenBy(ci => ci.DisplayOrder)
            .ThenBy(ci => ci.Course!.CourseCode)
            .ToListAsync(cancellationToken);

        var enrollments = await _db.Enrollments
            .AsNoTracking()
            .AsSplitQuery()
            .Include(e => e.Course)
            .Include(e => e.Semester)
            .Include(e => e.Grades)
            .Where(e => e.StudentId == studentId)
            .OrderByDescending(e => e.Semester!.DisplayOrder)
            .ThenBy(e => e.Course!.CourseCode)
            .ThenByDescending(e => e.AttemptNo)
            .ToListAsync(cancellationToken);

        var courseIds = curriculumItems
            .Select(ci => ci.CourseId)
            .Concat(enrollments.Select(e => e.CourseId))
            .Distinct()
            .ToList();

        // Project only the columns Grade needs. Some existing FAT databases do
        // not have the newer Assessment.PartCount column yet; materializing the
        // full entity would make EF select that unrelated column and prevent
        // users from viewing or entering scores.
        var assessmentDefinitions = await _db.Assessments
            .AsNoTracking()
            .Where(a => courseIds.Contains(a.CourseId))
            .OrderBy(a => a.DisplayOrder)
            .ThenBy(a => a.Name)
            .Select(a => new GradeAssessmentDefinition(
                a.AssessmentId,
                a.CourseId,
                a.Name,
                a.Weight,
                a.MinScoreToPass,
                a.DisplayOrder))
            .ToListAsync(cancellationToken);

        var assessmentsByCourseId = assessmentDefinitions
            .GroupBy(a => a.CourseId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<GradeAssessmentDefinition>)g.ToList());

        var curriculumByCourseId = curriculumItems
            .GroupBy(ci => ci.CourseId)
            .ToDictionary(g => g.Key, g => g.OrderBy(ci => ci.TermNo).First());

        var results = enrollments
            .Select(enrollment =>
            {
                var termNo = curriculumByCourseId.TryGetValue(
                    enrollment.CourseId, out var curriculum)
                    ? curriculum.TermNo
                    : -1;
                assessmentsByCourseId.TryGetValue(
                    enrollment.CourseId, out var courseAssessments);

                return MapGradeCourse(
                    enrollment,
                    termNo,
                    curriculum?.Term?.TermName,
                    curriculum?.DisplayOrder ?? 0,
                    courseAssessments ?? [],
                    student.RequiresComponentGrades);
            })
            .ToList();

        var enrolledCourseIds = enrollments
            .Select(e => e.CourseId)
            .ToHashSet();

        results.AddRange(curriculumItems
            .Where(ci => !enrolledCourseIds.Contains(ci.CourseId))
            .Select(ci =>
            {
                assessmentsByCourseId.TryGetValue(ci.CourseId, out var courseAssessments);
                return MapCurriculumCourse(ci, courseAssessments ?? []);
            }));

        return results
            .OrderBy(r => r.CurriculumTermNo >= 0 ? r.CurriculumTermNo : int.MaxValue)
            .ThenBy(r => r.CurriculumDisplayOrder)
            .ThenBy(r => r.CourseCode)
            .ThenBy(r => r.SemesterDisplayOrder)
            .ThenBy(r => r.AttemptNo)
            .ToList();
    }

    public async Task<IReadOnlyList<GradeTermOptionDto>> GetTermOptionsAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "Xem học kỳ");

        var majorId = await _db.Students
            .AsNoTracking()
            .Where(s => s.StudentId == studentId)
            .Select(s => (int?)s.MajorId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                $"Không tìm thấy sinh viên có mã định danh {studentId}.");

        var curriculumTermNumbers = _db.CurriculumItems
            .AsNoTracking()
            .Where(ci => ci.MajorId == majorId
                         && ci.Course != null
                         && ci.Course.IsActive)
            .Select(ci => ci.TermNo)
            .Distinct();

        var terms = await _db.Terms
            .AsNoTracking()
            .Where(t => t.IsActive && curriculumTermNumbers.Contains(t.TermNo))
            .OrderBy(t => t.TermNo)
            .Select(t => new { t.TermNo, t.TermName })
            .ToListAsync(cancellationToken);

        return terms
            .Select(t => new GradeTermOptionDto(
                t.TermNo,
                NormalizeDatabaseText(t.TermName)))
            .ToList();
    }

    public async Task<IReadOnlyList<GradeSemesterOptionDto>> GetSemesterOptionsAsync(
        CancellationToken cancellationToken = default)
        => await _db.Semesters
            .AsNoTracking()
            .OrderByDescending(s => s.IsCurrent)
            .ThenByDescending(s => s.DisplayOrder)
            .Select(s => new GradeSemesterOptionDto(
                s.SemesterId,
                s.SemesterCode,
                s.SemesterName,
                s.DisplayOrder,
                s.IsCurrent))
            .ToListAsync(cancellationToken);

    public async Task<int> UpsertStudentGradeAsync(
        int studentId,
        int enrollmentId,
        int courseId,
        int semesterId,
        int assessmentId,
        decimal score,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "Cập nhật điểm");
        ValidateScore(score);

        if (enrollmentId > 0)
        {
            var enrollment = await _db.Enrollments
                .AsNoTracking()
                .Where(e => e.EnrollmentId == enrollmentId)
                .Select(e => new { e.StudentId, e.CourseId })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Không tìm thấy lượt học có mã định danh {enrollmentId}.");

            if (enrollment.StudentId != studentId || enrollment.CourseId != courseId)
            {
                throw new InvalidOperationException(
                    "Lượt học đã chọn không thuộc sinh viên hoặc môn học hiện tại.");
            }

            await UpsertGradeAsync(
                enrollmentId, assessmentId, score, cancellationToken);
            return enrollmentId;
        }

        var belongsToCurriculum = await _db.CurriculumItems
            .AsNoTracking()
            .AnyAsync(ci => ci.CourseId == courseId
                            && ci.Term != null
                            && ci.Term.IsActive
                            && ci.Major!.Students.Any(s => s.StudentId == studentId),
                cancellationToken);

        if (!belongsToCurriculum)
        {
            throw new InvalidOperationException(
                "Môn học không thuộc chương trình hiện tại của sinh viên.");
        }

        var assessmentExists = await _db.Assessments
            .AsNoTracking()
            .AnyAsync(a => a.AssessmentId == assessmentId
                           && a.CourseId == courseId,
                cancellationToken);

        if (!assessmentExists)
        {
            throw new ArgumentException(
                "Assessment không tồn tại hoặc không thuộc môn học đã chọn.",
                nameof(assessmentId));
        }

        var newEnrollmentId = await EnrollAsync(
            studentId, courseId, semesterId, cancellationToken);
        await UpsertGradeAsync(
            newEnrollmentId, assessmentId, score, cancellationToken);

        return newEnrollmentId;
    }

    public async Task<int> UpsertStudentGradeAsync(
        int studentId,
        int enrollmentId,
        int courseId,
        int assessmentId,
        decimal score,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "Cập nhật điểm");
        ValidateScore(score);

        if (enrollmentId > 0)
        {
            return await UpsertStudentGradeAsync(
                studentId,
                enrollmentId,
                courseId,
                semesterId: 0,
                assessmentId,
                score,
                cancellationToken);
        }

        var currentSemesterId = await _db.Semesters
            .AsNoTracking()
            .Where(s => s.IsCurrent)
            .OrderByDescending(s => s.DisplayOrder)
            .Select(s => (int?)s.SemesterId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Chưa có học kỳ hiện tại trong hệ thống. Không thể tạo lượt học để nhập điểm.");

        return await UpsertStudentGradeAsync(
            studentId,
            enrollmentId,
            courseId,
            currentSemesterId,
            assessmentId,
            score,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Grade>> GetGradesAsync(
        int enrollmentId, CancellationToken cancellationToken = default)
    {
        var studentId = await RequireEnrollmentOwnerAsync(enrollmentId, "Xem điểm thành phần", cancellationToken);
        _currentUser.RequireSelfOrAdmin(studentId, "Xem điểm thành phần");

        var grades = await _db.Grades
            .AsNoTracking()
            .Where(g => g.EnrollmentId == enrollmentId)
            .ToListAsync(cancellationToken);

        var assessmentIds = grades.Select(g => g.AssessmentId).Distinct().ToList();
        var assessments = await _db.Assessments
            .AsNoTracking()
            .Where(a => assessmentIds.Contains(a.AssessmentId))
            .Select(a => new Assessment
            {
                AssessmentId = a.AssessmentId,
                CourseId = a.CourseId,
                Name = a.Name,
                Weight = a.Weight,
                MinScoreToPass = a.MinScoreToPass,
                DisplayOrder = a.DisplayOrder
            })
            .ToDictionaryAsync(a => a.AssessmentId, cancellationToken);

        foreach (var grade in grades)
        {
            assessments.TryGetValue(grade.AssessmentId, out var assessment);
            grade.Assessment = assessment;
        }

        return grades
            .OrderBy(g => g.Assessment?.DisplayOrder ?? int.MaxValue)
            .ThenBy(g => g.Assessment?.Name)
            .ToList();
    }

    public async Task UpsertGradeAsync(
        int enrollmentId,
        int assessmentId,
        decimal score,
        CancellationToken cancellationToken = default)
    {
        ValidateScore(score);

        var enrollment = await _db.Enrollments
            .FirstOrDefaultAsync(e => e.EnrollmentId == enrollmentId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Không tìm thấy lượt học có mã định danh {enrollmentId}.");

        _currentUser.RequireSelfOrAdmin(enrollment.StudentId, "Cập nhật điểm");

        if (enrollment.Status == EnrollmentStatus.Withdrawn)
        {
            throw new InvalidOperationException("Không thể nhập điểm cho môn đã rút.");
        }

        var assessmentExists = await _db.Assessments.AnyAsync(
            a => a.AssessmentId == assessmentId && a.CourseId == enrollment.CourseId,
            cancellationToken);

        if (!assessmentExists)
        {
            throw new ArgumentException(
                "Assessment không tồn tại hoặc không thuộc môn học đã chọn.",
                nameof(assessmentId));
        }

        var grade = await _db.Grades.FirstOrDefaultAsync(
            g => g.EnrollmentId == enrollmentId && g.AssessmentId == assessmentId,
            cancellationToken);

        if (grade is null)
        {
            _db.Grades.Add(new Grade
            {
                EnrollmentId = enrollmentId,
                AssessmentId = assessmentId,
                Score = score,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            grade.Score = score;
            grade.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await RecalculateFinalScoreCoreAsync(enrollmentId, cancellationToken);
    }

    public async Task DeleteGradeAsync(
        int enrollmentId,
        int assessmentId,
        CancellationToken cancellationToken = default)
    {
        var studentId = await RequireEnrollmentOwnerAsync(enrollmentId, "Xóa điểm", cancellationToken);
        _currentUser.RequireSelfOrAdmin(studentId, "Xóa điểm");

        var grade = await _db.Grades.FirstOrDefaultAsync(
            g => g.EnrollmentId == enrollmentId && g.AssessmentId == assessmentId,
            cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy điểm cần xóa.");

        _db.Grades.Remove(grade);
        await _db.SaveChangesAsync(cancellationToken);
        await RecalculateFinalScoreCoreAsync(enrollmentId, cancellationToken);
    }

    public async Task RecalculateFinalScoreAsync(
        int enrollmentId, CancellationToken cancellationToken = default)
    {
        var studentId = await RequireEnrollmentOwnerAsync(
            enrollmentId, "Tính lại điểm tổng kết", cancellationToken);
        _currentUser.RequireSelfOrAdmin(studentId, "Tính lại điểm tổng kết");

        await RecalculateFinalScoreCoreAsync(enrollmentId, cancellationToken);
    }

    public async Task<int> EnrollAsync(
        int studentId,
        int courseId,
        int semesterId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireSelfOrAdmin(studentId, "Đăng ký môn học");

        if (!await _db.Students.AnyAsync(s => s.StudentId == studentId, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Không tìm thấy sinh viên có mã định danh {studentId}.");
        }

        if (!await _db.Courses.AnyAsync(c => c.CourseId == courseId && c.IsActive, cancellationToken))
        {
            throw new InvalidOperationException("Môn học không tồn tại hoặc đã ngừng hoạt động.");
        }

        if (!await _db.Semesters.AnyAsync(s => s.SemesterId == semesterId, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Không tìm thấy học kỳ có mã định danh {semesterId}.");
        }

        if (await _db.Enrollments.AnyAsync(
                e => e.StudentId == studentId
                     && e.CourseId == courseId
                     && e.SemesterId == semesterId,
                cancellationToken))
        {
            throw new InvalidOperationException("Môn học đã được đăng ký trong học kỳ này.");
        }

        var prerequisiteCheck = await _prerequisites.CanEnrollAsync(
            studentId, courseId, cancellationToken);

        if (!prerequisiteCheck.CanEnroll)
        {
            throw new InvalidOperationException(prerequisiteCheck.BuildReason());
        }

        var previousAttempts = await _db.Enrollments
            .Where(e => e.StudentId == studentId && e.CourseId == courseId)
            .ToListAsync(cancellationToken);

        foreach (var attempt in previousAttempts)
        {
            attempt.IsCounted = false;
        }

        var enrollment = new Enrollment
        {
            StudentId = studentId,
            CourseId = courseId,
            SemesterId = semesterId,
            Status = EnrollmentStatus.Studying,
            IsCounted = true,
            AttemptNo = previousAttempts.Count == 0
                ? 1
                : previousAttempts.Max(e => e.AttemptNo) + 1,
            CreatedAt = DateTime.UtcNow
        };

        _db.Enrollments.Add(enrollment);
        await _db.SaveChangesAsync(cancellationToken);

        return enrollment.EnrollmentId;
    }

    public async Task WithdrawAsync(
        int enrollmentId, CancellationToken cancellationToken = default)
    {
        var enrollment = await _db.Enrollments
            .FirstOrDefaultAsync(e => e.EnrollmentId == enrollmentId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Không tìm thấy lượt học có mã định danh {enrollmentId}.");

        _currentUser.RequireSelfOrAdmin(enrollment.StudentId, "Rút môn học");

        if (enrollment.Status != EnrollmentStatus.Studying)
        {
            throw new InvalidOperationException("Chỉ có thể rút môn đang học.");
        }

        enrollment.Status = EnrollmentStatus.Withdrawn;
        enrollment.FinalScore = null;
        enrollment.LetterGrade = null;
        enrollment.GradePoint = null;
        enrollment.IsCounted = false;
        enrollment.UpdatedAt = DateTime.UtcNow;

        var previous = await _db.Enrollments
            .Where(e => e.StudentId == enrollment.StudentId
                        && e.CourseId == enrollment.CourseId
                        && e.EnrollmentId != enrollment.EnrollmentId)
            .OrderByDescending(e => e.AttemptNo)
            .FirstOrDefaultAsync(cancellationToken);

        if (previous is not null)
        {
            previous.IsCounted = true;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task RecalculateFinalScoreCoreAsync(
        int enrollmentId, CancellationToken cancellationToken)
    {
        var enrollment = await _db.Enrollments
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.EnrollmentId == enrollmentId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Không tìm thấy lượt học có mã định danh {enrollmentId}.");

        var assessments = await _db.Assessments
            .AsNoTracking()
            .Where(a => a.CourseId == enrollment.CourseId)
            .OrderBy(a => a.DisplayOrder)
            .Select(a => new GradeAssessmentDefinition(
                a.AssessmentId,
                a.CourseId,
                a.Name,
                a.Weight,
                a.MinScoreToPass,
                a.DisplayOrder))
            .ToListAsync(cancellationToken);

        var grades = await _db.Grades
            .AsNoTracking()
            .Where(g => g.EnrollmentId == enrollmentId)
            .ToDictionaryAsync(g => g.AssessmentId, cancellationToken);

        if (assessments.Count == 0 || assessments.Any(a => !grades.ContainsKey(a.AssessmentId)))
        {
            enrollment.FinalScore = null;
            enrollment.LetterGrade = null;
            enrollment.GradePoint = null;
            enrollment.Status = EnrollmentStatus.Studying;
            enrollment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var finalScore = AcademicRules.RoundFinalScore(
            assessments.Sum(a => grades[a.AssessmentId].Score * a.Weight));

        var violatesComponentMinimum = assessments.Any(
            a => a.MinScoreToPass.HasValue
                 && grades[a.AssessmentId].Score < a.MinScoreToPass.Value);

        var gradeScale = (await _db.GradeScales
                .AsNoTracking()
                .OrderBy(s => s.MinScore)
                .ToListAsync(cancellationToken))
            .FirstOrDefault(s => s.Contains(finalScore))
            ?? throw new InvalidOperationException(
                $"Không có thang quy đổi phù hợp cho điểm tổng kết {finalScore:0.0}.");

        var passScore = enrollment.Course?.MinAvgMarkToPass ?? AcademicRules.PassScore;

        enrollment.FinalScore = finalScore;
        enrollment.LetterGrade = gradeScale.LetterGrade;
        enrollment.GradePoint = gradeScale.GradePoint;
        enrollment.Status = finalScore >= passScore && !violatesComponentMinimum
            ? EnrollmentStatus.Passed
            : EnrollmentStatus.Failed;
        enrollment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> RequireEnrollmentOwnerAsync(
        int enrollmentId,
        string operation,
        CancellationToken cancellationToken)
    {
        var studentId = await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.EnrollmentId == enrollmentId)
            .Select(e => (int?)e.StudentId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                $"Không tìm thấy lượt học có mã định danh {enrollmentId}.");

        _currentUser.RequireSelfOrAdmin(studentId, operation);
        return studentId;
    }

    private static void ValidateScore(decimal score)
    {
        if (score < 0m || score > MaximumScore)
        {
            throw new ArgumentOutOfRangeException(
                nameof(score),
                $"Điểm phải nằm trong khoảng 0 đến {MaximumScore:0}.");
        }
    }

    private static GradeCourseDto MapGradeCourse(
        Enrollment enrollment,
        int curriculumTermNo,
        string? curriculumTermName,
        int curriculumDisplayOrder,
        IReadOnlyList<GradeAssessmentDefinition> assessmentDefinitions,
        bool requiresComponentGrades)
    {
        var course = enrollment.Course;
        var semester = enrollment.Semester;
        var assessments = MapAssessments(assessmentDefinitions, enrollment.Grades);
        var hasCompleteComponentGrades =
            assessments.Count > 0 && assessments.All(a => a.HasScore);
        var canUsePersistedResult =
            !requiresComponentGrades || hasCompleteComponentGrades;

        return new GradeCourseDto(
            enrollment.EnrollmentId,
            enrollment.CourseId,
            course?.CourseCode ?? string.Empty,
            course?.CourseName ?? string.Empty,
            Math.Max(0, course?.Credits ?? 0),
            enrollment.SemesterId,
            semester?.SemesterCode ?? string.Empty,
            semester?.SemesterName ?? string.Empty,
            semester?.DisplayOrder ?? 0,
            semester?.IsCurrent ?? false,
            canUsePersistedResult
                ? enrollment.Status
                : EnrollmentStatus.Studying,
            canUsePersistedResult ? enrollment.FinalScore : null,
            canUsePersistedResult ? enrollment.LetterGrade : null,
            canUsePersistedResult ? enrollment.GradePoint : null,
            course?.CountsTowardGpa ?? false,
            enrollment.IsCounted,
            enrollment.AttemptNo,
            assessments,
            curriculumTermNo,
            NormalizeDatabaseText(curriculumTermName),
            curriculumDisplayOrder);
    }

    private static GradeCourseDto MapCurriculumCourse(
        Curriculum curriculum,
        IReadOnlyList<GradeAssessmentDefinition> assessmentDefinitions)
    {
        var course = curriculum.Course;

        return new GradeCourseDto(
            EnrollmentId: 0,
            CourseId: curriculum.CourseId,
            CourseCode: course?.CourseCode ?? string.Empty,
            CourseName: course?.CourseName ?? string.Empty,
            Credits: Math.Max(0, course?.Credits ?? 0),
            SemesterId: 0,
            SemesterCode: string.Empty,
            SemesterName: string.Empty,
            SemesterDisplayOrder: 0,
            SemesterIsCurrent: false,
            Status: EnrollmentStatus.Studying,
            FinalScore: null,
            LetterGrade: null,
            GradePoint: null,
            CountsTowardGpa: course?.CountsTowardGpa ?? false,
            IsCounted: false,
            AttemptNo: 0,
            Assessments: MapAssessments(assessmentDefinitions, []),
            CurriculumTermNo: curriculum.TermNo,
            CurriculumTermName: NormalizeDatabaseText(curriculum.Term?.TermName),
            CurriculumDisplayOrder: curriculum.DisplayOrder);
    }

    private static IReadOnlyList<GradeAssessmentDto> MapAssessments(
        IEnumerable<GradeAssessmentDefinition> assessments,
        IEnumerable<Grade> grades)
    {
        var gradesByAssessment = grades
            .GroupBy(g => g.AssessmentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First());

        return assessments
            .OrderBy(a => a.DisplayOrder)
            .ThenBy(a => a.Name)
            .Select(a =>
            {
                gradesByAssessment.TryGetValue(a.AssessmentId, out var grade);

                return new GradeAssessmentDto(
                    a.AssessmentId,
                    a.Name ?? string.Empty,
                    a.Weight,
                    a.MinScoreToPass,
                    a.DisplayOrder,
                    grade?.GradeId,
                    grade?.Score);
            })
            .ToList();
    }

    private static string NormalizeDatabaseText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value ?? string.Empty;
        }

        var normalized = value;
        for (var pass = 0; pass < 3; pass++)
        {
            var currentScore = GetMojibakeScore(normalized);
            if (currentScore == 0)
            {
                break;
            }

            try
            {
                var repaired = StrictUtf8Encoding.GetString(
                    Windows1252Encoding.GetBytes(normalized));

                if (GetMojibakeScore(repaired) >= currentScore)
                {
                    break;
                }

                normalized = repaired;
            }
            catch (EncoderFallbackException)
            {
                break;
            }
            catch (DecoderFallbackException)
            {
                break;
            }
        }

        return normalized;
    }

    private static int GetMojibakeScore(string value)
    {
        var score = value.Count(c => c is >= '\u0080' and <= '\u009F');
        score += value.Count(c => c is 'Ã' or 'Â' or 'Ä' or 'Æ');
        score += CountOccurrences(value, "á»");
        score += CountOccurrences(value, "áº");
        return score;
    }

    private static int CountOccurrences(string value, string marker)
    {
        var count = 0;
        var startIndex = 0;

        while ((startIndex = value.IndexOf(
                   marker,
                   startIndex,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += marker.Length;
        }

        return count;
    }

    private static Encoding CreateWindows1252Encoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(
            1252,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
    }

    private sealed record GradeAssessmentDefinition(
        int AssessmentId,
        int CourseId,
        string Name,
        decimal Weight,
        decimal? MinScoreToPass,
        int DisplayOrder);
}
