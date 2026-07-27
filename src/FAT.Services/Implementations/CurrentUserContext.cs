using FAT.Services.Abstractions;
using FAT.Services.Dtos;

namespace FAT.Services.Implementations;

/// <summary>
/// Holds the signed-in user. Registered as a SINGLETON: the application has
/// exactly one session at a time. Owner: Member 1.
/// </summary>
public class CurrentUserContext : ICurrentUserContext
{
    public CurrentUserInfo? User { get; private set; }

    public bool IsAuthenticated => User is not null;

    public bool IsAdmin => User?.IsAdmin ?? false;

    public int? StudentId => User?.StudentId;

    public event EventHandler? UserChanged;

    public void SetUser(CurrentUserInfo user)
    {
        User = user ?? throw new ArgumentNullException(nameof(user));
        UserChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        User = null;
        UserChanged?.Invoke(this, EventArgs.Empty);
    }

    public int RequireStudentId()
        => StudentId ?? throw new InvalidOperationException(
            "This screen is for students only, but the signed-in account has no student profile.");
}
