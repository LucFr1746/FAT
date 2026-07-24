using Data;
using Domain.Constants;
using Domain.Entities;
using Services.Abstractions;
using Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Services.Implementations;

/// <summary>CRUD over a subject's assessment timeline.</summary>
public sealed class AssessmentScheduleService : IAssessmentScheduleService
{
    private readonly FAT_DBContext _db;
    private readonly ICurrentUserContext _currentUser;

    public AssessmentScheduleService(FAT_DBContext db, ICurrentUserContext currentUser)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<IReadOnlyList<AssessmentScheduleDto>> GetByCourseAsync(
        int courseId, CancellationToken cancellationToken = default)
        => await _db.AssessmentSchedules
            .AsNoTracking()
            .Where(s => s.CourseId == courseId)
            .OrderBy(s => s.SessionNo)
            .Select(s => new AssessmentScheduleDto(
                s.AssessmentScheduleId,
                s.CourseId,
                s.SessionNo,
                s.WeekNo,
                s.Title,
                s.Description,
                s.ExpectedDate,
                s.TeachingType,
                s.AssessmentId,
                s.Assessment != null ? s.Assessment.Name : null))
            .ToListAsync(cancellationToken);

    public async Task<int> CreateAsync(
        AssessmentScheduleDto schedule, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Thêm lịch kiểm tra");

        var title = RequireTitle(schedule.Title);
        ValidateSessionNo(schedule.SessionNo);

        if (!await _db.Courses.AnyAsync(c => c.CourseId == schedule.CourseId, cancellationToken))
        {
            throw new InvalidOperationException($"Không tìm thấy môn học có mã định danh {schedule.CourseId}.");
        }

        await ValidateAssessmentLinkAsync(schedule, cancellationToken);
        await EnsureSessionIsFreeAsync(schedule.CourseId, schedule.SessionNo, excludeId: null, cancellationToken);

        var entity = new AssessmentSchedule
        {
            CourseId = schedule.CourseId,
            SessionNo = schedule.SessionNo,
            // Derived when the caller does not supply one, so the week column is
            // never blank just because a form field was skipped.
            WeekNo = schedule.WeekNo ?? CatalogRules.GetWeekNo(schedule.SessionNo),
            Title = title,
            Description = Trim(schedule.Description),
            ExpectedDate = schedule.ExpectedDate?.Date,
            TeachingType = Trim(schedule.TeachingType, 100),
            AssessmentId = schedule.AssessmentId
        };

        _db.AssessmentSchedules.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return entity.AssessmentScheduleId;
    }

    public async Task UpdateAsync(
        AssessmentScheduleDto schedule, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Cập nhật lịch kiểm tra");

        var title = RequireTitle(schedule.Title);
        ValidateSessionNo(schedule.SessionNo);

        var entity = await _db.AssessmentSchedules
                .FirstOrDefaultAsync(s => s.AssessmentScheduleId == schedule.AssessmentScheduleId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Không tìm thấy lịch kiểm tra có mã định danh {schedule.AssessmentScheduleId}.");

        await ValidateAssessmentLinkAsync(schedule, cancellationToken);
        await EnsureSessionIsFreeAsync(
            entity.CourseId, schedule.SessionNo, schedule.AssessmentScheduleId, cancellationToken);

        entity.SessionNo = schedule.SessionNo;
        entity.WeekNo = schedule.WeekNo ?? CatalogRules.GetWeekNo(schedule.SessionNo);
        entity.Title = title;
        entity.Description = Trim(schedule.Description);
        entity.ExpectedDate = schedule.ExpectedDate?.Date;
        entity.TeachingType = Trim(schedule.TeachingType, 100);
        entity.AssessmentId = schedule.AssessmentId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int assessmentScheduleId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Xóa lịch kiểm tra");

        var entity = await _db.AssessmentSchedules
                .FirstOrDefaultAsync(s => s.AssessmentScheduleId == assessmentScheduleId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Không tìm thấy lịch kiểm tra có mã định danh {assessmentScheduleId}.");

        _db.AssessmentSchedules.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string RequireTitle(string? title)
    {
        var trimmed = title?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new ArgumentException("Nội dung kiểm tra không được để trống.", nameof(title));
        }

        if (trimmed.Length > CatalogRules.MaterialTitleMaxLength)
        {
            throw new ArgumentException(
                $"Nội dung kiểm tra không được vượt quá {CatalogRules.MaterialTitleMaxLength} ký tự.", nameof(title));
        }

        return trimmed;
    }

    private static void ValidateSessionNo(int sessionNo)
    {
        // Mirrors CK_AssessmentSchedule_Session. Buổi numbering starts at 1,
        // unlike the kỳ numbering, which starts at 0.
        if (sessionNo < 1)
        {
            throw new ArgumentException("Số buổi học phải lớn hơn hoặc bằng 1.", nameof(sessionNo));
        }
    }

    /// <summary>
    /// Confirms a linked grade component belongs to the SAME subject.
    /// Without this a schedule row could point at another subject's Final exam,
    /// and the subject detail screen would show a checkpoint that grades nothing.
    /// </summary>
    private async Task ValidateAssessmentLinkAsync(
        AssessmentScheduleDto schedule, CancellationToken cancellationToken)
    {
        if (schedule.AssessmentId is null)
        {
            return;
        }

        var belongsToCourse = await _db.Assessments.AnyAsync(
            a => a.AssessmentId == schedule.AssessmentId && a.CourseId == schedule.CourseId,
            cancellationToken);

        if (!belongsToCourse)
        {
            throw new InvalidOperationException("Cột điểm được liên kết không thuộc môn học này.");
        }
    }

    private static string? Trim(string? value, int? maxLength = null)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return maxLength is not null && trimmed.Length > maxLength ? trimmed[..maxLength.Value] : trimmed;
    }

    private async Task EnsureSessionIsFreeAsync(
        int courseId, int sessionNo, int? excludeId, CancellationToken cancellationToken)
    {
        var taken = await _db.AssessmentSchedules.AnyAsync(
            s => s.CourseId == courseId
                 && s.SessionNo == sessionNo
                 && (excludeId == null || s.AssessmentScheduleId != excludeId),
            cancellationToken);

        if (taken)
        {
            throw new InvalidOperationException($"Buổi {sessionNo} đã có lịch kiểm tra trong môn học này.");
        }
    }
}
