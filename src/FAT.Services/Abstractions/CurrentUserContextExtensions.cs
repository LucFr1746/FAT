namespace FAT.Services.Abstractions;

/// <summary>
/// Authorization guards shared by every service.
///
/// WHY THIS EXISTS: hiding a button is not authorization. The catalog screens
/// bind their visibility to <see cref="ICurrentUserContext.IsAdmin"/>, but a
/// view model resolved by any other path would reach the service anyway, so the
/// service checks for itself. Having one guard rather than a hand-written `if`
/// in forty methods means the check cannot be forgotten in one of them.
/// </summary>
public static class CurrentUserContextExtensions
{
    /// <summary>Throws unless an Admin is signed in.</summary>
    public static void RequireAdmin(this ICurrentUserContext context, string operation)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.IsAuthenticated)
        {
            throw new UnauthorizedAccessException(
                $"Bạn cần đăng nhập để thực hiện thao tác '{operation}'.");
        }

        if (!context.IsAdmin)
        {
            throw new UnauthorizedAccessException(
                $"Chỉ tài khoản Admin mới được phép thực hiện thao tác '{operation}'.");
        }
    }

    /// <summary>
    /// Throws unless the signed-in student owns <paramref name="studentId"/>.
    ///
    /// The guard against a student reading another student's record: without it,
    /// one wrong id from a view is all it takes to expose someone else's grades.
    /// Admins are allowed through so support screens keep working.
    /// </summary>
    public static void RequireSelfOrAdmin(this ICurrentUserContext context, int studentId, string operation)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.IsAuthenticated)
        {
            throw new UnauthorizedAccessException(
                $"Bạn cần đăng nhập để thực hiện thao tác '{operation}'.");
        }

        if (context.IsAdmin || context.StudentId == studentId)
        {
            return;
        }

        throw new UnauthorizedAccessException(
            $"Bạn không có quyền truy cập dữ liệu của sinh viên khác ('{operation}').");
    }
}
