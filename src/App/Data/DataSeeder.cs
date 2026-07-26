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
/// Seeds the application database from CSV files under db/data/csv on startup
/// whenever the Curriculum table is empty.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedCurriculumIfEmptyAsync(FAT_DBContext db, CancellationToken cancellationToken = default)
    {
        if (db == null)
        {
            return;
        }

        // Seeder condition: Only run when no curriculum items exist in database
        if (await db.CurriculumItems.AnyAsync(cancellationToken))
        {
            return;
        }

        var csvFolderPath = FindCsvFolderPath();
        if (string.IsNullOrEmpty(csvFolderPath))
        {
            return;
        }

        var systemAdminContext = new SystemAdminUserContext();
        var importService = new FlmImportService(db, systemAdminContext);
        await importService.ImportAsync(csvFolderPath, ImportOptions.Default, cancellationToken);
    }

    private static string? FindCsvFolderPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "db", "data", "csv"),
            Path.Combine(Directory.GetCurrentDirectory(), "db", "data", "csv"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "db", "data", "csv"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "db", "data", "csv")
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (Directory.Exists(fullPath) && File.Exists(Path.Combine(fullPath, "subjects.csv")))
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
