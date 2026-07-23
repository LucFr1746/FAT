using FAT.Data;
using FAT.Domain.Constants;
using FAT.Services.Abstractions;
using FAT.Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FAT.Services.Implementations;

/// <summary>BCrypt-based authentication. Owner: Member 1.</summary>
public class AuthService : IAuthService
{
    /// <summary>
    /// Decoy hash used when no account matches the username.
    ///
    /// Returning immediately for a missing account would make "wrong username"
    /// noticeably faster than "wrong password", since BCrypt is deliberately
    /// slow. Timing that difference is enough to enumerate valid usernames.
    /// Verifying against a decoy keeps both paths equally expensive.
    /// </summary>
    private const string DecoyHash = "$2a$11$JJQiWDIKwyl.f89GLxktb.lx2BSbc.XhflOzX9V993TDFW0fQsAzW";

    /// <summary>
    /// One message for EVERY failed sign-in.
    /// It deliberately does not distinguish a bad username from a bad password,
    /// because distinguishing them confirms which accounts exist.
    /// </summary>
    private const string InvalidCredentialsMessage = "Incorrect username or password.";

    private readonly FatDbContext _db;

    public AuthService(FatDbContext db) => _db = db;

    public async Task<LoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return LoginResult.Failure("Please enter both a username and a password.");
        }

        var normalized = username.Trim();

        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Student)
            .SingleOrDefaultAsync(u => u.Username == normalized, cancellationToken);

        if (user is null)
        {
            BCrypt.Net.BCrypt.Verify(password, DecoyHash);
            return LoginResult.Failure(InvalidCredentialsMessage);
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return LoginResult.Failure(InvalidCredentialsMessage);
        }

        // The IsActive check comes AFTER password verification on purpose:
        // checking it first would disclose that an account exists to someone
        // who does not know its password.
        if (!user.IsActive)
        {
            return LoginResult.Failure("This account is locked. Please contact an administrator.");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var roleName = user.Role?.RoleName ?? RoleNames.Student;

        return LoginResult.Success(new CurrentUserInfo(
            UserId: user.UserId,
            Username: user.Username,
            RoleName: roleName,
            IsAdmin: string.Equals(roleName, RoleNames.Admin, StringComparison.Ordinal),
            StudentId: user.Student?.StudentId,
            StudentCode: user.Student?.StudentCode,
            FullName: user.Student?.FullName ?? user.Username));
    }

    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            throw new ArgumentException("The new password must be at least 8 characters long.", nameof(newPassword));
        }

        var user = await _db.Users.SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user is null || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            return false;
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 11);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
