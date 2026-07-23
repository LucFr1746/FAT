using FAT.Data;
using FAT.Domain.Constants;
using FAT.Domain.Entities;
using FAT.Services.Abstractions;
using FAT.Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FAT.Services.Implementations;

/// <summary>Manage Semester - CRUD over the kỳ of the study path.</summary>
public sealed class TermService : ITermService
{
    private readonly FatDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public TermService(FatDbContext db, ICurrentUserContext currentUser)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<IReadOnlyList<TermDto>> GetAllAsync(
        bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        var query = _db.Terms.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        // The subject count is computed in the same query rather than per row:
        // a count query per kỳ is the N+1 that turns ten rows into eleven trips.
        return await query
            .OrderBy(t => t.TermNo)
            .Select(t => new TermDto(
                t.TermId,
                t.TermNo,
                t.TermName,
                t.Description,
                t.IsActive,
                t.CurriculumItems.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<TermDto?> GetByIdAsync(int termId, CancellationToken cancellationToken = default)
        => await _db.Terms
            .AsNoTracking()
            .Where(t => t.TermId == termId)
            .Select(t => new TermDto(
                t.TermId, t.TermNo, t.TermName, t.Description, t.IsActive, t.CurriculumItems.Count))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<int> CreateAsync(TermDto term, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Thêm kỳ học");

        ValidateTermNo(term.TermNo);
        var name = RequireName(term.TermName, term.TermNo);

        await EnsureTermNoIsFreeAsync(term.TermNo, excludeTermId: null, cancellationToken);

        var entity = new Term
        {
            TermNo = term.TermNo,
            TermName = name,
            Description = Trim(term.Description),
            IsActive = term.IsActive
        };

        _db.Terms.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return entity.TermId;
    }

    public async Task UpdateAsync(TermDto term, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Cập nhật kỳ học");

        ValidateTermNo(term.TermNo);
        var name = RequireName(term.TermName, term.TermNo);

        var entity = await _db.Terms.FirstOrDefaultAsync(t => t.TermId == term.TermId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy kỳ học có mã định danh {term.TermId}.");

        await EnsureTermNoIsFreeAsync(term.TermNo, term.TermId, cancellationToken);

        // Curriculum.TermNo is a foreign key onto Term.TermNo, so renumbering a
        // kỳ that subjects already point at would orphan every one of them.
        if (entity.TermNo != term.TermNo)
        {
            var inUse = await _db.CurriculumItems.AnyAsync(ci => ci.TermNo == entity.TermNo, cancellationToken);

            if (inUse)
            {
                throw new InvalidOperationException(
                    $"Không thể đổi số kỳ {entity.TermNo}: đang có môn học thuộc kỳ này. " +
                    "Hãy chuyển các môn sang kỳ khác trước.");
            }

            entity.TermNo = term.TermNo;
        }

        entity.TermName = name;
        entity.Description = Trim(term.Description);
        entity.IsActive = term.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(int termId, bool isActive, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Đổi trạng thái kỳ học");

        var entity = await _db.Terms.FirstOrDefaultAsync(t => t.TermId == termId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy kỳ học có mã định danh {termId}.");

        entity.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int termId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Xóa kỳ học");

        var entity = await _db.Terms.FirstOrDefaultAsync(t => t.TermId == termId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy kỳ học có mã định danh {termId}.");

        // The foreign key would refuse this anyway; catching it here turns a raw
        // constraint error into a sentence that says what to do about it.
        var subjectCount = await _db.CurriculumItems.CountAsync(ci => ci.TermNo == entity.TermNo, cancellationToken);

        if (subjectCount > 0)
        {
            throw new InvalidOperationException(
                $"Không thể xóa {entity.TermName}: đang có {subjectCount} môn học thuộc kỳ này. " +
                "Hãy dùng chức năng Ngừng hoạt động nếu chỉ muốn ẩn kỳ học.");
        }

        _db.Terms.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateTermNo(int termNo)
    {
        // Mirrors CK_Term_No. Zero is legal: OTP101 really is Kỳ 0.
        if (termNo < CatalogRules.MinTermNo || termNo > CatalogRules.MaxTermNo)
        {
            throw new ArgumentException(
                $"Số kỳ phải nằm trong khoảng {CatalogRules.MinTermNo} đến {CatalogRules.MaxTermNo}.",
                nameof(termNo));
        }
    }

    /// <summary>Falls back to the conventional name rather than rejecting a blank one.</summary>
    private static string RequireName(string? termName, int termNo)
    {
        var trimmed = termName?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return CatalogRules.GetTermName(termNo);
        }

        if (trimmed.Length > CatalogRules.TermNameMaxLength)
        {
            throw new ArgumentException(
                $"Tên kỳ học không được vượt quá {CatalogRules.TermNameMaxLength} ký tự.", nameof(termName));
        }

        return trimmed;
    }

    private static string? Trim(string? value)
    {
        var trimmed = value?.Trim();

        if (!string.IsNullOrEmpty(trimmed) && trimmed.Length > CatalogRules.DescriptionMaxLength)
        {
            throw new ArgumentException(
                $"Mô tả không được vượt quá {CatalogRules.DescriptionMaxLength} ký tự.", nameof(value));
        }

        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private async Task EnsureTermNoIsFreeAsync(
        int termNo, int? excludeTermId, CancellationToken cancellationToken)
    {
        var taken = await _db.Terms.AnyAsync(
            t => t.TermNo == termNo && (excludeTermId == null || t.TermId != excludeTermId),
            cancellationToken);

        if (taken)
        {
            throw new InvalidOperationException($"Kỳ số {termNo} đã tồn tại.");
        }
    }
}
