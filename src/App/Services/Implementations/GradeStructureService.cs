using Data;
using Domain.Constants;
using Domain.Entities;
using Services.Abstractions;
using Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Services.Implementations;

/// <summary>
/// CRUD over a subject's grade components.
///
/// The weights of one subject must total 100%. Nothing at run time notices when
/// they do not: the final score is SUM(Score * Weight), so a structure adding up
/// to 90% silently caps that subject at 9.0 for everyone taking it.
/// </summary>
public sealed class GradeStructureService : IGradeStructureService
{
    private readonly FAT_DBContext _db;
    private readonly ICurrentUserContext _currentUser;

    public GradeStructureService(FAT_DBContext db, ICurrentUserContext currentUser)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<IReadOnlyList<AssessmentDto>> GetByCourseAsync(
        int courseId, CancellationToken cancellationToken = default)
        => await _db.Assessments
            .AsNoTracking()
            .Where(a => a.CourseId == courseId)
            .OrderBy(a => a.DisplayOrder)
            .ThenBy(a => a.Name)
            .Select(a => new AssessmentDto(
                a.AssessmentId, a.CourseId, a.Name, a.Weight, a.MinScoreToPass, a.DisplayOrder))
            .ToListAsync(cancellationToken);

    public async Task<int> CreateAsync(
        AssessmentDto assessment, bool allowUnbalanced = true, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Thêm cột điểm");

        var name = RequireName(assessment.Name);
        ValidateValues(assessment);

        if (!await _db.Courses.AnyAsync(c => c.CourseId == assessment.CourseId, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Không tìm thấy môn học có mã định danh {assessment.CourseId}.");
        }

        await EnsureNameIsFreeAsync(assessment.CourseId, name, excludeAssessmentId: null, cancellationToken);

        var entity = new Assessment
        {
            CourseId = assessment.CourseId,
            Name = name,
            Weight = Normalize(assessment.Weight),
            MinScoreToPass = assessment.MinScoreToPass,
            DisplayOrder = assessment.DisplayOrder
        };

        _db.Assessments.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        await EnforceBalanceAsync(assessment.CourseId, allowUnbalanced, cancellationToken);

        return entity.AssessmentId;
    }

    public async Task UpdateAsync(
        AssessmentDto assessment, bool allowUnbalanced = true, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Cập nhật cột điểm");

        var name = RequireName(assessment.Name);
        ValidateValues(assessment);

        var entity = await _db.Assessments
                .FirstOrDefaultAsync(a => a.AssessmentId == assessment.AssessmentId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Không tìm thấy cột điểm có mã định danh {assessment.AssessmentId}.");

        await EnsureNameIsFreeAsync(entity.CourseId, name, assessment.AssessmentId, cancellationToken);

        entity.Name = name;
        entity.Weight = Normalize(assessment.Weight);
        entity.MinScoreToPass = assessment.MinScoreToPass;
        entity.DisplayOrder = assessment.DisplayOrder;

        await _db.SaveChangesAsync(cancellationToken);

        await EnforceBalanceAsync(entity.CourseId, allowUnbalanced, cancellationToken);
    }

    public async Task DeleteAsync(int assessmentId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Xóa cột điểm");

        var entity = await _db.Assessments
                .FirstOrDefaultAsync(a => a.AssessmentId == assessmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy cột điểm có mã định danh {assessmentId}.");

        // Scores already recorded against this component would go with it, and
        // every affected final score would change without anyone being told.
        var gradeCount = await _db.Grades.CountAsync(g => g.AssessmentId == assessmentId, cancellationToken);

        if (gradeCount > 0)
        {
            throw new InvalidOperationException(
                $"Không thể xóa cột điểm '{entity.Name}': đã có {gradeCount} điểm thành phần được ghi nhận.");
        }

        _db.Assessments.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<GradeStructureValidationDto> ValidateWeightsAsync(
        int courseId, CancellationToken cancellationToken = default)
    {
        var totalWeight = await _db.Assessments
            .Where(a => a.CourseId == courseId)
            .SumAsync(a => (decimal?)a.Weight, cancellationToken) ?? 0m;

        return GradeStructureValidationDto.FromWeights(courseId, totalWeight);
    }

    /// <summary>
    /// Rejects an unbalanced structure once the caller says it is finished.
    ///
    /// Runs AFTER the save so the check sees the real stored total, then throws
    /// - the surrounding admin screen reports the message and offers to fix the
    /// weights. Building a structure row by row passes allowUnbalanced, because
    /// otherwise the very first component would be refused for not already
    /// totalling 100%.
    /// </summary>
    private async Task EnforceBalanceAsync(
        int courseId, bool allowUnbalanced, CancellationToken cancellationToken)
    {
        if (allowUnbalanced)
        {
            return;
        }

        var validation = await ValidateWeightsAsync(courseId, cancellationToken);

        if (!validation.IsBalanced)
        {
            throw new InvalidOperationException(string.Join(" ", validation.Errors));
        }
    }

    private static string RequireName(string? name)
    {
        var trimmed = name?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new ArgumentException("Tên cột điểm không được để trống.", nameof(name));
        }

        if (trimmed.Length > CatalogRules.AssessmentNameMaxLength)
        {
            throw new ArgumentException(
                $"Tên cột điểm không được vượt quá {CatalogRules.AssessmentNameMaxLength} ký tự.", nameof(name));
        }

        return trimmed;
    }

    private static void ValidateValues(AssessmentDto assessment)
    {
        // Mirrors CK_Assessment_Weight.
        if (assessment.Weight <= 0m || assessment.Weight > 1m)
        {
            throw new ArgumentException(
                "Trọng số phải lớn hơn 0% và không vượt quá 100%.", nameof(assessment));
        }

        if (assessment.MinScoreToPass is < 0 or > 10)
        {
            throw new ArgumentException(
                "Điểm tối thiểu của cột điểm phải nằm trong khoảng 0 đến 10.", nameof(assessment));
        }
    }

    /// <summary>Rounds to the stored precision so comparisons and totals agree.</summary>
    private static decimal Normalize(decimal weight)
        => Math.Round(weight, CatalogRules.AssessmentWeightDecimals, MidpointRounding.AwayFromZero);

    private async Task EnsureNameIsFreeAsync(
        int courseId, string name, int? excludeAssessmentId, CancellationToken cancellationToken)
    {
        var taken = await _db.Assessments.AnyAsync(
            a => a.CourseId == courseId
                 && a.Name == name
                 && (excludeAssessmentId == null || a.AssessmentId != excludeAssessmentId),
            cancellationToken);

        if (taken)
        {
            throw new InvalidOperationException($"Cột điểm '{name}' đã tồn tại trong môn học này.");
        }
    }
}
