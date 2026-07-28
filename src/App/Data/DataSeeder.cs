using System.IO;
using Data;
using Domain.Constants;
using Microsoft.EntityFrameworkCore;
using Services.Abstractions;
using Services.Dtos;
using Services.Implementations;
using Services.Import;

namespace Data;

/// <summary>
/// Seeds the application database from the JSON files under db/data/json on
/// startup whenever the Curriculum table is empty.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedCurriculumIfEmptyAsync(FAT_DBContext db, bool forceImport = false, CancellationToken cancellationToken = default)
    {
        if (db == null)
        {
            return;
        }

        // Seeder condition: Only run when no curriculum items exist in database (unless forceImport is true)
        if (!forceImport && await db.CurriculumItems.CountAsync(cancellationToken) > 35)
        {
            return;
        }

        var jsonFolderPath = FindJsonFolderPath();
        if (string.IsNullOrEmpty(jsonFolderPath))
        {
            return;
        }

        var systemAdminContext = new SystemAdminUserContext();
        var importService = new FlmImportService(db, systemAdminContext);
        await importService.ImportAsync(jsonFolderPath, ImportOptions.Default, cancellationToken);
    }

    private static string? FindJsonFolderPath()
    {
        var fileCandidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "db", "data", "BIT_SE_K19D_K20A.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "db", "data", "BIT_SE_K19D_K20A.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "db", "data", "BIT_SE_K19D_K20A.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "db", "data", "BIT_SE_K19D_K20A.json"),
            Path.Combine(AppContext.BaseDirectory, "db", "data", "json", "BIT_SE_K19D_K20A.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "db", "data", "json", "BIT_SE_K19D_K20A.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "db", "data", "json", "BIT_SE_K19D_K20A.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "db", "data", "json", "BIT_SE_K19D_K20A.json")
        };

        foreach (var candidate in fileCandidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        var folderCandidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "db", "data", "json"),
            Path.Combine(Directory.GetCurrentDirectory(), "db", "data", "json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "db", "data", "json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "db", "data", "json")
        };

        foreach (var candidate in folderCandidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (Directory.Exists(fullPath) && File.Exists(Path.Combine(fullPath, "subjects.json")))
            {
                return fullPath;
            }
        }

        return null;
    }

    private sealed class SystemAdminUserContext : ICurrentUserContext
    {
        public CurrentUserInfo? User { get; } = new CurrentUserInfo(
            UserId: 0,
            Username: "system_seeder",
            RoleName: RoleNames.Admin,
            IsAdmin: true,
            StudentId: null,
            StudentCode: null,
            FullName: "System Seeder");

        public bool IsAuthenticated => true;
        public bool IsAdmin => true;
        public int? StudentId => null;
#pragma warning disable CS0067
        public event EventHandler? UserChanged;
#pragma warning restore CS0067

        public void SetUser(CurrentUserInfo user) { }
        public void Clear() { }
        public int RequireStudentId() => throw new InvalidOperationException("System seeder has no student id.");
    }
}
