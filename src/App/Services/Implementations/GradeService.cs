using Data;
using Domain.Constants;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Services.Abstractions;
using Services.Dtos;

namespace Services.Implementations;

/// <summary>
/// Grade entry, final-score settlement and transcript aggregation.
/// All persisted values use the existing Enrollment, Assessment, Grade and
/// GradeScale tables; no calculated aggregate is stored outside Enrollment.
/// </summary>
public sealed class GradeService : IGradeService
{
    private const decimal MaximumScore = 10m;

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

        var exists = await _db.Students
            .AsNoTracking()
            .AnyAsync(s => s.StudentId == studentId, cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException(
                $"Không tìm thấy sinh viên có mã định danh {studentId}.");
        }

        var enrollments = await _db.Enrollments
            .AsNoTracking()
            .AsSplitQuery()
            .Include(e => e.Course)
                .ThenInclude(c => c!.Assessments)
            .Include(e => e.Semester)
            .Include(e => e.Grades)
            .Where(e => e.StudentId == studentId)
            .OrderByDescending(e => e.Semester!.DisplayOrder)
            .ThenBy(e => e.Course!.CourseCode)
            .ThenByDescending(e => e.AttemptNo)
            .ToListAsync(cancellationToken);

        return enrollments.Select(MapGradeCourse).ToList();
    }

    public async Task<IReadOnlyList<Grade>> GetGradesAsync(
        int enrollmentId, CancellationToken cancellationToken = default)
    {
        var studentId = await RequireEnrollmentOwnerAsync(enrollmentId, "Xem điểm thành phần", cancellationToken);
        _currentUser.RequireSelfOrAdmin(studentId, "Xem điểm thành phần");

        return await _db.Grades
            .AsNoTracking()
            .Include(g => g.Assessment)
            .Where(g => g.EnrollmentId == enrollmentId)
            .OrderBy(g => g.Assessment!.DisplayOrder)
            .ThenBy(g => g.Assessment!.Name)
            .ToListAsync(cancellationToken);
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

    private static GradeCourseDto MapGradeCourse(Enrollment enrollment)
    {
        var gradesByAssessment = enrollment.Grades
            .GroupBy(g => g.AssessmentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First());

        var assessments = (enrollment.Course?.Assessments ?? [])
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

        var course = enrollment.Course;
        var semester = enrollment.Semester;

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
            enrollment.Status,
            enrollment.FinalScore,
            enrollment.LetterGrade,
            enrollment.GradePoint,
            course?.CountsTowardGpa ?? false,
            enrollment.IsCounted,
            enrollment.AttemptNo,
            assessments);
    }
}
