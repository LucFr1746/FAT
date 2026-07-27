using System.Security.Cryptography;
using Data;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Services.Abstractions;
using Services.Dtos;

namespace Services.Implementations;

/// <summary>
/// Uploaded file materials - Member 5's "Upload / Download" side.
///
/// Unlike the FLM syllabus links (which are just a URL in SubjectMaterial), a
/// material here owns real bytes in <see cref="MaterialFile"/>: an admin uploads
/// a file, students download it. Metadata lists never pull the bytes - only
/// <see cref="DownloadAsync"/> touches MaterialFile - so the browse screen stays
/// cheap even when the store holds large files.
/// </summary>
public sealed class MaterialService : IMaterialService
{
    private readonly FAT_DBContext _db;
    private readonly ICurrentUserContext _currentUser;

    public MaterialService(FAT_DBContext db, ICurrentUserContext currentUser)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<IReadOnlyList<MaterialDto>> SearchAsync(
        MaterialFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = _db.Materials.AsNoTracking().AsQueryable();

        if (!filter.IncludeInactive)
        {
            query = query.Where(m => m.IsActive);
        }

        if (filter.CourseId is int courseId)
        {
            query = query.Where(m => m.CourseId == courseId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            query = query.Where(m => m.Category == filter.Category);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.Trim();
            query = query.Where(m =>
                EF.Functions.Like(m.Title, $"%{keyword}%")
                || EF.Functions.Like(m.FileName, $"%{keyword}%"));
        }

        return await query
            .OrderByDescending(m => m.UploadedAt)
            .Select(m => Project(m))
            .ToListAsync(cancellationToken);
    }

    public async Task<MaterialDto?> GetByIdAsync(int materialId, CancellationToken cancellationToken = default)
    {
        return await _db.Materials
            .AsNoTracking()
            .Where(m => m.MaterialId == materialId)
            .Select(m => Project(m))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MaterialDto>> GetByCourseAsync(
        int courseId, CancellationToken cancellationToken = default)
    {
        return await _db.Materials
            .AsNoTracking()
            .Where(m => m.CourseId == courseId && m.IsActive)
            .OrderByDescending(m => m.UploadedAt)
            .Select(m => Project(m))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> UploadAsync(
        MaterialUploadRequest request, int uploadedByUserId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Tải lên tài liệu");
        ArgumentNullException.ThrowIfNull(request);

        if (request.Content is null || request.Content.Length == 0)
        {
            throw new InvalidOperationException("Tệp rỗng, không thể tải lên.");
        }

        if (request.Content.LongLength > IMaterialService.MaxFileSizeBytes)
        {
            var maxMb = IMaterialService.MaxFileSizeBytes / (1024 * 1024);
            throw new InvalidOperationException($"Tệp vượt quá giới hạn {maxMb} MB.");
        }

        if (!MaterialCategories.IsValid(request.Category))
        {
            throw new InvalidOperationException($"Danh mục '{request.Category}' không hợp lệ.");
        }

        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Tiêu đề tài liệu không được để trống.");
        }

        if (request.CourseId is int courseId
            && !await _db.Courses.AnyAsync(c => c.CourseId == courseId, cancellationToken))
        {
            throw new InvalidOperationException("Không tìm thấy môn học được chọn.");
        }

        // A user-supplied name carrying "..\" becomes a vulnerability the moment
        // anything writes it to disk, so strip every path part now.
        var safeName = SanitizeFileName(request.FileName);

        // SHA-256 lets a second identical upload be caught rather than stored twice.
        var hash = Convert.ToHexString(SHA256.HashData(request.Content)).ToLowerInvariant();
        var duplicate = await _db.Materials.AnyAsync(
            m => m.CourseId == request.CourseId && m.ContentHash == hash && m.IsActive, cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException("Tệp có nội dung trùng với một tài liệu đã tải lên cho môn này.");
        }

        var material = new Material
        {
            CourseId = request.CourseId,
            Title = title,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Category = request.Category,
            FileName = safeName,
            ContentType = string.IsNullOrWhiteSpace(request.ContentType)
                ? "application/octet-stream"
                : request.ContentType,
            FileSizeBytes = request.Content.LongLength,
            ContentHash = hash,
            UploadedByUserId = uploadedByUserId,
            UploadedAt = DateTime.Now,
            DownloadCount = 0,
            IsActive = true,
            File = new MaterialFile { Content = request.Content }
        };

        _db.Materials.Add(material);
        await _db.SaveChangesAsync(cancellationToken);
        return material.MaterialId;
    }

    public async Task<MaterialDownload?> DownloadAsync(int materialId, CancellationToken cancellationToken = default)
    {
        var material = await _db.Materials
            .Include(m => m.File)
            .FirstOrDefaultAsync(m => m.MaterialId == materialId, cancellationToken);

        if (material?.File is null)
        {
            return null;
        }

        material.DownloadCount++;
        await _db.SaveChangesAsync(cancellationToken);

        return new MaterialDownload(material.FileName, material.ContentType, material.File.Content);
    }

    public async Task UpdateAsync(
        int materialId, string title, string? description, string category, int? courseId,
        CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Cập nhật tài liệu");

        if (!MaterialCategories.IsValid(category))
        {
            throw new InvalidOperationException($"Danh mục '{category}' không hợp lệ.");
        }

        var cleanTitle = title?.Trim();
        if (string.IsNullOrWhiteSpace(cleanTitle))
        {
            throw new InvalidOperationException("Tiêu đề tài liệu không được để trống.");
        }

        var material = await _db.Materials.FirstOrDefaultAsync(m => m.MaterialId == materialId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy tài liệu.");

        material.Title = cleanTitle;
        material.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        material.Category = category;
        material.CourseId = courseId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateAsync(int materialId, CancellationToken cancellationToken = default)
    {
        _currentUser.RequireAdmin("Ẩn tài liệu");

        var material = await _db.Materials.FirstOrDefaultAsync(m => m.MaterialId == materialId, cancellationToken);
        if (material is null)
        {
            return;
        }

        // Deactivate rather than delete, so download history and references survive.
        material.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static MaterialDto Project(Material m) => new(
        m.MaterialId,
        m.CourseId,
        m.Course?.CourseCode,
        m.Title,
        m.Description,
        m.Category,
        m.FileName,
        m.ContentType,
        m.FileSizeBytes,
        m.UploadedBy?.Username,
        m.UploadedAt,
        m.DownloadCount);

    /// <summary>
    /// Reduces a user-supplied name to a bare, safe file name: no directory
    /// parts, no characters the file system rejects, never empty.
    /// </summary>
    private static string SanitizeFileName(string? fileName)
    {
        var name = Path.GetFileName(fileName?.Trim() ?? string.Empty);

        if (string.IsNullOrWhiteSpace(name))
        {
            name = "material";
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name.Length > 255 ? name[..255] : name;
    }
}
