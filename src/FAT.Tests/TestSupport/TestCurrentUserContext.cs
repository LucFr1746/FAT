using FAT.Services.Abstractions;
using FAT.Services.Dtos;

namespace FAT.Tests.TestSupport;

/// <summary>
/// A signed-in user, chosen by the test.
///
/// Every catalog service guards its methods with RequireAdmin/RequireSelfOrAdmin,
/// so tests need a way to be an admin, a specific student, or nobody - the last
/// one being how the authorization tests prove the guard actually fires.
/// </summary>
public sealed class TestCurrentUserContext : ICurrentUserContext
{
    public CurrentUserInfo? User { get; private set; }

    public bool IsAuthenticated => User is not null;
    public bool IsAdmin => User?.IsAdmin ?? false;
    public int? StudentId => User?.StudentId;

    public event EventHandler? UserChanged;

    public void SetUser(CurrentUserInfo user)
    {
        User = user;
        UserChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        User = null;
        UserChanged?.Invoke(this, EventArgs.Empty);
    }

    public int RequireStudentId()
        => StudentId ?? throw new InvalidOperationException("No student is signed in.");

    /// <summary>An administrator.</summary>
    public static TestCurrentUserContext Admin()
    {
        var context = new TestCurrentUserContext();
        context.SetUser(new CurrentUserInfo(
            UserId: 1, Username: "admin", RoleName: "Admin",
            IsAdmin: true, StudentId: null, StudentCode: null, FullName: "Administrator"));
        return context;
    }

    /// <summary>A student, with the given profile id.</summary>
    public static TestCurrentUserContext Student(int studentId)
    {
        var context = new TestCurrentUserContext();
        context.SetUser(new CurrentUserInfo(
            UserId: 100 + studentId, Username: $"student{studentId}", RoleName: "Student",
            IsAdmin: false, StudentId: studentId, StudentCode: $"SE{studentId:D5}",
            FullName: $"Student {studentId}"));
        return context;
    }

    /// <summary>Nobody signed in.</summary>
    public static TestCurrentUserContext Anonymous() => new();
}
