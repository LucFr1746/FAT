using FAT.Data;
using FAT.Domain.Constants;
using FAT.Domain.Entities;
using FAT.Services.Abstractions;
using FAT.Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FAT.Services.Implementations;

/// <summary>CRUD over a subject's readings and links.</summary>
public sealed class SubjectMaterialService : ISubjectMaterialService
{
    private readonly FatDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public SubjectMaterialService(FatDbContext db, ICurrentUserContext currentUser)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<IReadOnlyList<SubjectMaterialDto>> GetByCourseAsync(
        int courseId, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _db.SubjectMaterials.AsNoTracking().Where(m => m.CourseId == courseId);

        if (!includeInactive)
        {
            query = query.Where(m => m.IsActive);
        }

        return await query
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.Title)
            .Select(m => new SubjectMaterialDto(
                m.SubjectMaterialId, m.CourseId, m.Title, m.Description, m.Url,
                m.Author, m.Publisher, m.Isbn, m.DisplayOrder, m.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CreateAsync(
        SubjectMaterialDto material, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Thêm tài liệu môn học");

        var title = RequireTitle(material.Title);
        var url = ValidateUrl(material.Url);

        if (!await _db.Courses.AnyAsync(c => c.CourseId == material.CourseId, cancellationToken))
        {
            throw new InvalidOperationException($"Không tìm thấy môn học có mã định danh {material.CourseId}.");
        }

        await EnsureTitleIsFreeAsync(material.CourseId, title, excludeId: null, cancellationToken);

        var entity = new SubjectMaterial
        {
            CourseId = material.CourseId,
            Title = title,
            Description = Trim(material.Description),
            Url = url,
            Author = Trim(material.Author, 200),
            Publisher = Trim(material.Publisher, 200),
            Isbn = Trim(material.Isbn, 50),
            DisplayOrder = material.DisplayOrder > 0
                ? material.DisplayOrder
                : await GetNextDisplayOrderAsync(material.CourseId, cancellationToken),
            IsActive = material.IsActive
        };

        _db.SubjectMaterials.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return entity.SubjectMaterialId;
    }

    public async Task UpdateAsync(
        SubjectMaterialDto material, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Cập nhật tài liệu môn học");

        var title = RequireTitle(material.Title);
        var url = ValidateUrl(material.Url);

        var entity = await _db.SubjectMaterials
                .FirstOrDefaultAsync(m => m.SubjectMaterialId == material.SubjectMaterialId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Không tìm thấy tài liệu có mã định danh {material.SubjectMaterialId}.");

        await EnsureTitleIsFreeAsync(entity.CourseId, title, material.SubjectMaterialId, cancellationToken);

        entity.Title = title;
        entity.Description = Trim(material.Description);
        entity.Url = url;
        entity.Author = Trim(material.Author, 200);
        entity.Publisher = Trim(material.Publisher, 200);
        entity.Isbn = Trim(material.Isbn, 50);
        entity.DisplayOrder = material.DisplayOrder;
        entity.IsActive = material.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int subjectMaterialId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Xóa tài liệu môn học");

        var entity = await _db.SubjectMaterials
                .FirstOrDefaultAsync(m => m.SubjectMaterialId == subjectMaterialId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Không tìm thấy tài liệu có mã định danh {subjectMaterialId}.");

        // A hard delete is safe here: nothing references a reading, and an
        // administrator removing a wrong link expects it to be gone.
        _db.SubjectMaterials.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderAsync(
        int courseId, IReadOnlyList<int> orderedIds, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Sắp xếp tài liệu môn học");

        if (orderedIds is null || orderedIds.Count == 0)
        {
            return;
        }

        var materials = await _db.SubjectMaterials
            .Where(m => m.CourseId == courseId)
            .ToListAsync(cancellationToken);

        for (var index = 0; index < orderedIds.Count; index++)
        {
            var material = materials.FirstOrDefault(m => m.SubjectMaterialId == orderedIds[index]);

            if (material is not null)
            {
                material.DisplayOrder = index;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string RequireTitle(string? title)
    {
        var trimmed = title?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new ArgumentException("Tiêu đề tài liệu không được để trống.", nameof(title));
        }

        if (trimmed.Length > CatalogRules.MaterialTitleMaxLength)
        {
            throw new ArgumentException(
                $"Tiêu đề tài liệu không được vượt quá {CatalogRules.MaterialTitleMaxLength} ký tự.", nameof(title));
        }

        return trimmed;
    }

    /// <summary>
    /// Accepts only absolute http/https links.
    ///
    /// A relative path or a "javascript:" URL would be opened by the shell when
    /// the student clicks it - only the two web schemes are safe to hand over.
    /// </summary>
    private static string? ValidateUrl(string? url)
    {
        var trimmed = url?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > CatalogRules.UrlMaxLength)
        {
            throw new ArgumentException(
                $"Đường dẫn không được vượt quá {CatalogRules.UrlMaxLength} ký tự.", nameof(url));
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "Đường dẫn tài liệu phải là một địa chỉ http hoặc https hợp lệ.", nameof(url));
        }

        return trimmed;
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

    private async Task<int> GetNextDisplayOrderAsync(int courseId, CancellationToken cancellationToken)
    {
        var max = await _db.SubjectMaterials
            .Where(m => m.CourseId == courseId)
            .Select(m => (int?)m.DisplayOrder)
            .MaxAsync(cancellationToken);

        return (max ?? -1) + 1;
    }

    private async Task EnsureTitleIsFreeAsync(
        int courseId, string title, int? excludeId, CancellationToken cancellationToken)
    {
        var taken = await _db.SubjectMaterials.AnyAsync(
            m => m.CourseId == courseId
                 && m.Title == title
                 && (excludeId == null || m.SubjectMaterialId != excludeId),
            cancellationToken);

        if (taken)
        {
            throw new InvalidOperationException($"Tài liệu '{title}' đã tồn tại trong môn học này.");
        }
    }
}
