using FAT.Data;
using FAT.Domain.Constants;
using FAT.Domain.Entities;
using FAT.Domain.Enums;
using FAT.Services.Abstractions;
using FAT.Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FAT.Services.Implementations;

/// <summary>
/// Manage Major, Manage Semester (calendar) and Manage Subject - the write side
/// of the catalog. Curriculum links live in <see cref="CurriculumAdminService"/>
/// and the kỳ of the study path in <see cref="TermService"/>.
///
/// Prerequisite handling is in CatalogAdminService.Prerequisites.cs.
/// </summary>
public sealed partial class CatalogAdminService : ICatalogAdminService
{
    private readonly FatDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly ICurriculumAdminService _curriculumAdmin;

    public CatalogAdminService(
        FatDbContext db,
        ICurrentUserContext currentUser,
        ICurriculumAdminService curriculumAdmin)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _curriculumAdmin = curriculumAdmin ?? throw new ArgumentNullException(nameof(curriculumAdmin));
    }

    // =========================================================================
    // Manage Major
    // =========================================================================

    public async Task<IReadOnlyList<MajorDto>> GetMajorsAsync(
        MajorFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _db.Majors.AsNoTracking();

        if (filter.IsActive is not null)
        {
            query = query.Where(m => m.IsActive == filter.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.Trim();
            query = query.Where(m => EF.Functions.Like(m.MajorCode, $"%{keyword}%")
                                  || EF.Functions.Like(m.MajorName, $"%{keyword}%"));
        }

        return await query
            .OrderBy(m => m.MajorCode)
            .Select(m => new MajorDto(
                m.MajorId, m.MajorCode, m.MajorName, m.RequiredCredits, m.TotalTerms, m.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CreateMajorAsync(MajorDto major, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Thêm ngành học");

        var code = Require(major.MajorCode, "Mã ngành", CatalogRules.MajorCodeMaxLength);
        var name = Require(major.MajorName, "Tên ngành", CatalogRules.MajorNameMaxLength);

        await EnsureMajorCodeIsFreeAsync(code, excludeMajorId: null, cancellationToken);

        var entity = new Major
        {
            MajorCode = code,
            MajorName = name,
            // CK_Major_Credit requires both to be positive. The real totals
            // arrive the moment the curriculum has any subjects in it.
            RequiredCredits = Math.Max(1, major.RequiredCredits),
            TotalTerms = Math.Max(1, major.TotalTerms),
            IsActive = major.IsActive
        };

        _db.Majors.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return entity.MajorId;
    }

    public async Task UpdateMajorAsync(MajorDto major, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Cập nhật ngành học");

        var code = Require(major.MajorCode, "Mã ngành", CatalogRules.MajorCodeMaxLength);
        var name = Require(major.MajorName, "Tên ngành", CatalogRules.MajorNameMaxLength);

        var entity = await _db.Majors.FirstOrDefaultAsync(m => m.MajorId == major.MajorId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy ngành học có mã định danh {major.MajorId}.");

        await EnsureMajorCodeIsFreeAsync(code, major.MajorId, cancellationToken);

        entity.MajorCode = code;
        entity.MajorName = name;
        entity.IsActive = major.IsActive;
        entity.TotalTerms = Math.Max(1, major.TotalTerms);

        // RequiredCredits is deliberately NOT taken from the form: it is derived
        // from the curriculum, and letting an administrator type a different
        // number is exactly how it drifts out of step with the subject list.
        await MajorCreditCalculator.SyncAsync(_db, entity.MajorId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateMajorAsync(int majorId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Ngừng hoạt động ngành học");

        var entity = await _db.Majors.FirstOrDefaultAsync(m => m.MajorId == majorId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy ngành học có mã định danh {majorId}.");

        // Blocked rather than cascaded: a student whose programme has been
        // switched off cannot see their curriculum, and nothing in the UI
        // explains why.
        var activeStudents = await _db.Students
            .CountAsync(s => s.MajorId == majorId && s.Status == StudentStatus.Active, cancellationToken);

        if (activeStudents > 0)
        {
            throw new InvalidOperationException(
                $"Không thể ngừng hoạt động ngành '{entity.MajorCode}': còn {activeStudents} sinh viên đang theo học.");
        }

        entity.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
    }

    // =========================================================================
    // Manage Semester (the CALENDAR term - FA25, SP26)
    // =========================================================================

    public async Task<int> CreateSemesterAsync(SemesterDto semester, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Thêm học kỳ");

        var code = Require(semester.SemesterCode, "Mã học kỳ", CatalogRules.SemesterCodeMaxLength);
        var name = Require(semester.SemesterName, "Tên học kỳ", CatalogRules.SemesterNameMaxLength);

        ValidateSemesterDates(semester);
        await EnsureSemesterIsUniqueAsync(code, semester.DisplayOrder, excludeSemesterId: null, cancellationToken);

        var entity = new Semester
        {
            SemesterCode = code,
            SemesterName = name,
            StartDate = semester.StartDate,
            EndDate = semester.EndDate,
            DisplayOrder = semester.DisplayOrder,
            IsCurrent = false
        };

        _db.Semesters.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        // Routed through SetCurrentSemesterAsync so the "exactly one current"
        // rule is applied in one place instead of two.
        if (semester.IsCurrent)
        {
            await SetCurrentSemesterAsync(entity.SemesterId, cancellationToken);
        }

        return entity.SemesterId;
    }

    public async Task UpdateSemesterAsync(SemesterDto semester, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Cập nhật học kỳ");

        var code = Require(semester.SemesterCode, "Mã học kỳ", CatalogRules.SemesterCodeMaxLength);
        var name = Require(semester.SemesterName, "Tên học kỳ", CatalogRules.SemesterNameMaxLength);

        ValidateSemesterDates(semester);

        var entity = await _db.Semesters
                .FirstOrDefaultAsync(s => s.SemesterId == semester.SemesterId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy học kỳ có mã định danh {semester.SemesterId}.");

        await EnsureSemesterIsUniqueAsync(code, semester.DisplayOrder, semester.SemesterId, cancellationToken);

        entity.SemesterCode = code;
        entity.SemesterName = name;
        entity.StartDate = semester.StartDate;
        entity.EndDate = semester.EndDate;
        entity.DisplayOrder = semester.DisplayOrder;

        await _db.SaveChangesAsync(cancellationToken);

        if (semester.IsCurrent && !entity.IsCurrent)
        {
            await SetCurrentSemesterAsync(entity.SemesterId, cancellationToken);
        }
    }

    public async Task SetCurrentSemesterAsync(int semesterId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Đặt học kỳ hiện tại");

        var target = await _db.Semesters.FirstOrDefaultAsync(s => s.SemesterId == semesterId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy học kỳ có mã định danh {semesterId}.");

        // Clearing the old flag and setting the new one must be ONE unit of
        // work: the database assumes exactly one semester is current, and
        // db/02_seed_master.sql asserts it. Two separate saves leave a window
        // with zero - or two - current semesters.
        var previouslyCurrent = await _db.Semesters
            .Where(s => s.IsCurrent && s.SemesterId != semesterId)
            .ToListAsync(cancellationToken);

        foreach (var semester in previouslyCurrent)
        {
            semester.IsCurrent = false;
        }

        target.IsCurrent = true;
        await _db.SaveChangesAsync(cancellationToken);
    }

    // =========================================================================
    // Shared validation helpers
    // =========================================================================

    /// <summary>Trims, checks for emptiness and enforces the column width in one step.</summary>
    private static string Require(string? value, string fieldName, int maxLength)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new ArgumentException($"{fieldName} không được để trống.", fieldName);
        }

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"{fieldName} không được vượt quá {maxLength} ký tự.", fieldName);
        }

        return trimmed;
    }

    /// <summary>
    /// Checks the unique index in C# so the user gets a sentence rather than a
    /// SqlException. The index remains the backstop for concurrent inserts.
    /// </summary>
    private async Task EnsureMajorCodeIsFreeAsync(
        string majorCode, int? excludeMajorId, CancellationToken cancellationToken)
    {
        var taken = await _db.Majors.AnyAsync(
            m => m.MajorCode == majorCode && (excludeMajorId == null || m.MajorId != excludeMajorId),
            cancellationToken);

        if (taken)
        {
            throw new InvalidOperationException($"Mã ngành '{majorCode}' đã tồn tại.");
        }
    }

    private static void ValidateSemesterDates(SemesterDto semester)
    {
        // Mirrors CK_Semester_Dates.
        if (semester.EndDate <= semester.StartDate)
        {
            throw new ArgumentException("Ngày kết thúc học kỳ phải sau ngày bắt đầu.", nameof(semester));
        }

        if (semester.DisplayOrder < 1)
        {
            throw new ArgumentException("Thứ tự học kỳ phải lớn hơn hoặc bằng 1.", nameof(semester));
        }
    }

    private async Task EnsureSemesterIsUniqueAsync(
        string code, int displayOrder, int? excludeSemesterId, CancellationToken cancellationToken)
    {
        var codeTaken = await _db.Semesters.AnyAsync(
            s => s.SemesterCode == code && (excludeSemesterId == null || s.SemesterId != excludeSemesterId),
            cancellationToken);

        if (codeTaken)
        {
            throw new InvalidOperationException($"Mã học kỳ '{code}' đã tồn tại.");
        }

        // DisplayOrder is the true chronological order and is uniquely indexed;
        // a duplicate would make the GPA trend chart order undefined.
        var orderTaken = await _db.Semesters.AnyAsync(
            s => s.DisplayOrder == displayOrder && (excludeSemesterId == null || s.SemesterId != excludeSemesterId),
            cancellationToken);

        if (orderTaken)
        {
            throw new InvalidOperationException($"Thứ tự học kỳ '{displayOrder}' đã được sử dụng.");
        }
    }
}
