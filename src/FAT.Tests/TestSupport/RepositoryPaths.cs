namespace FAT.Tests.TestSupport;

/// <summary>
/// Locates files that live in the repository rather than next to the test
/// assembly.
///
/// Walks up from the test binaries looking for FAT.sln instead of hard-coding
/// "..\..\..\..\..": the number of levels changes with the target framework and
/// the build configuration, and a wrong count only shows up as a confusing
/// FileNotFound at run time.
/// </summary>
public static class RepositoryPaths
{
    private static readonly Lazy<string?> RootLazy = new(FindRepositoryRoot);

    /// <summary>Repository root, or null when the tests run from a published copy.</summary>
    public static string? Root => RootLazy.Value;

    /// <summary>db/data - the committed FLM export.</summary>
    public static string? FlmDataFolder =>
        Root is null ? null : Path.Combine(Root, "db", "data");

    /// <summary>db/data/flm_chuong_trinh_hoc.xlsx.</summary>
    public static string? FlmWorkbook =>
        FlmDataFolder is null ? null : Path.Combine(FlmDataFolder, "flm_chuong_trinh_hoc.xlsx");

    /// <summary>db/data/json - the same export as JSON.</summary>
    public static string? FlmJsonFolder =>
        FlmDataFolder is null ? null : Path.Combine(FlmDataFolder, "json");

    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FAT.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
