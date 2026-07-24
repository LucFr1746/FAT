using Services.Dtos;

namespace Services.Abstractions;

/// <summary>
/// Holds the signed-in user for the lifetime of the session.
/// FROZEN CONTRACT - owner: Member 1.
///
/// EVERY view model must read StudentId from here and must NEVER pass a
/// StudentId around as a navigation parameter. If screens are allowed to decide
/// whose data they show, one wrong argument is all it takes for a student to
/// read another student's grades.
/// </summary>
public interface ICurrentUserContext
{
    CurrentUserInfo? User { get; }

    bool IsAuthenticated { get; }
    bool IsAdmin { get; }

    /// <summary>The signed-in student's id; null when an Admin is signed in.</summary>
    int? StudentId { get; }

    /// <summary>Raised on sign-in and sign-out so the shell can rebuild its menu.</summary>
    event EventHandler? UserChanged;

    void SetUser(CurrentUserInfo user);
    void Clear();

    /// <summary>
    /// Returns the StudentId or throws if there is none. Use this on
    /// student-only screens so the problem surfaces immediately instead of
    /// silently rendering an empty transcript.
    /// </summary>
    int RequireStudentId();
}
