using Data;
using Microsoft.EntityFrameworkCore;
using Services.Dtos;
using Services.Implementations;
using Xunit;

namespace Tests;

public class AuthServiceTests
{
    private static FAT_DBContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<FAT_DBContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var db = new FAT_DBContext(options);

        // Seed default roles and majors
        db.Roles.Add(new Domain.Entities.Role { RoleId = 1, RoleName = Domain.Constants.RoleNames.Admin, Description = "Admin" });
        db.Roles.Add(new Domain.Entities.Role { RoleId = 2, RoleName = Domain.Constants.RoleNames.Student, Description = "Student" });
        db.Majors.Add(new Domain.Entities.Major { MajorId = 1, MajorCode = "SE", MajorName = "Software Engineering", RequiredCredits = 150, TotalTerms = 9, IsActive = true });
        db.SaveChanges();

        return db;
    }

    [Fact]
    public async Task LoginAsync_ValidMssvCredentials_ReturnsSuccess()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Student@123", workFactor: 11);
        var user = new Domain.Entities.AppUser
        {
            Username = "SE170001",
            PasswordHash = passwordHash,
            RoleId = 2,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        var student = new Domain.Entities.Student
        {
            UserId = user.UserId,
            StudentCode = "SE170001",
            FullName = "SE170001",
            EnrollmentDate = DateTime.Today,
            MajorId = 1,
            IsProfileCompleted = false
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var authService = new AuthService(db);

        // Act
        var result = await authService.LoginAsync("SE170001", "Student@123");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.User);
        Assert.Equal("SE170001", result.User.Username);
        Assert.False(result.User.IsProfileCompleted);
    }

    [Fact]
    public async Task RegisterStudentAsync_ValidMssv_CreatesAccountWithIncompleteProfile()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var authService = new AuthService(db);
        var dto = new RegisterRequestDto(
            StudentCode: "SE170999",
            Password: "Password@123",
            ConfirmPassword: "Password@123",
            AcceptTerms: true
        );

        // Act
        var result = await authService.RegisterStudentAsync(dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.User);
        Assert.Equal("SE170999", result.User.StudentCode);
        Assert.False(result.User.IsProfileCompleted);

        // Verify DB
        var savedStudent = await db.Students.FirstOrDefaultAsync(s => s.StudentCode == "SE170999");
        Assert.NotNull(savedStudent);
        Assert.False(savedStudent.IsProfileCompleted);
    }

    [Fact]
    public async Task RegisterStudentAsync_DuplicateMssv_ReturnsFailure()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var authService = new AuthService(db);
        var dto = new RegisterRequestDto("SE170999", "Password@123", "Password@123", true);
        await authService.RegisterStudentAsync(dto);

        // Act - Register duplicate MSSV
        var duplicateDto = new RegisterRequestDto("SE170999", "Password@456", "Password@456", true);
        var result = await authService.RegisterStudentAsync(duplicateDto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("đã tồn tại", result.ErrorMessage);
    }

    [Fact]
    public async Task CompleteAcademicProfileAsync_UpdatesProfileAndSetsCompletedFlag()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var userService = new UserService(db);
        var authService = new AuthService(db);
        var dto = new RegisterRequestDto("SE180001", "Password@123", "Password@123", true);
        var regResult = await authService.RegisterStudentAsync(dto);

        // Act
        await userService.CompleteAcademicProfileAsync(
            studentId: regResult.User!.StudentId!.Value,
            fullName: "Trần Văn A",
            email: "a.tran@fpt.edu.vn",
            phone: "0912345678",
            majorId: 1,
            className: "SE1801",
            currentTermNo: 3);

        // Assert
        var profile = await userService.GetProfileAsync(regResult.User.StudentId.Value);
        Assert.NotNull(profile);
        Assert.True(profile.IsProfileCompleted);
        Assert.Equal("Trần Văn A", profile.FullName);
        Assert.Equal("a.tran@fpt.edu.vn", profile.Email);
        Assert.Equal("0912345678", profile.Phone);
        Assert.Equal("SE1801", profile.ClassName);
        Assert.Equal(1, profile.MajorId);
        Assert.Equal("Kỳ 3", profile.CurrentSemester);
    }

    [Fact]
    public async Task LoginWithGoogleAsync_MatchingEmail_LinksGoogleIdAndSucceeds()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var user = new Domain.Entities.AppUser
        {
            Username = "SE170002",
            RoleId = 2,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        var student = new Domain.Entities.Student
        {
            UserId = user.UserId,
            StudentCode = "SE170002",
            FullName = "Nguyễn Văn B",
            Email = "student.b@fpt.edu.vn",
            EnrollmentDate = DateTime.Today,
            MajorId = 1,
            IsProfileCompleted = true
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var authService = new AuthService(db);
        var googleInfo = new GoogleUserInfoDto(
            GoogleId: "1234567890987654321",
            Email: "student.b@fpt.edu.vn",
            FullName: "Nguyễn Văn B",
            PictureUrl: "https://example.com/avatar.jpg"
        );

        // Act
        var result = await authService.LoginWithGoogleAsync(googleInfo);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.User);
        Assert.Equal("SE170002", result.User.Username);
        Assert.Equal("https://example.com/avatar.jpg", result.User.AvatarUrl);

        var dbUser = await db.Users.FindAsync(user.UserId);
        Assert.Equal("1234567890987654321", dbUser?.GoogleId);
    }

    [Fact]
    public async Task LoginWithGoogleAsync_UnregisteredEmail_ReturnsAccountNotFound()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var authService = new AuthService(db);
        var googleInfo = new GoogleUserInfoDto(
            GoogleId: "9999999999",
            Email: "unknown.student@fpt.edu.vn",
            FullName: "Unknown Student",
            PictureUrl: null
        );

        // Act
        var result = await authService.LoginWithGoogleAsync(googleInfo);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("ACCOUNT_NOT_FOUND", result.ErrorMessage);
    }

    [Fact]
    public async Task LoginWithGoogleAsync_InactiveAccount_ReturnsFailure()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var user = new Domain.Entities.AppUser
        {
            Username = "SE170003",
            RoleId = 2,
            IsActive = false,
            GoogleId = "8888888888",
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var authService = new AuthService(db);
        var googleInfo = new GoogleUserInfoDto(
            GoogleId: "8888888888",
            Email: "locked.student@fpt.edu.vn",
            FullName: "Locked Student",
            PictureUrl: null
        );

        // Act
        var result = await authService.LoginWithGoogleAsync(googleInfo);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("khóa", result.ErrorMessage);
    }


    [Fact]
    public async Task SendResetOtpAsync_ValidUser_GeneratesOtp()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var user = new Domain.Entities.AppUser
        {
            Username = "SE170099",
            RoleId = 2,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        var student = new Domain.Entities.Student
        {
            UserId = user.UserId,
            StudentCode = "SE170099",
            FullName = "Test Reset Student",
            Email = "reset.test@fpt.edu.vn",
            EnrollmentDate = DateTime.Today,
            MajorId = 1,
            IsProfileCompleted = true
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var authService = new AuthService(db);

        // Act
        var result = await authService.SendResetOtpAsync("SE170099");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.MaskedEmail);
        Assert.NotNull(result.DevOtpCode);
        Assert.Equal(6, result.DevOtpCode.Length);
    }

    [Fact]
    public async Task ResetPasswordWithOtpAsync_ValidOtp_UpdatesPasswordHashAndAllowsLogin()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var user = new Domain.Entities.AppUser
        {
            Username = "SE170088",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPass@123"),
            RoleId = 2,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        var student = new Domain.Entities.Student
        {
            UserId = user.UserId,
            StudentCode = "SE170088",
            FullName = "Test Reset User",
            Email = "reset.user@fpt.edu.vn",
            EnrollmentDate = DateTime.Today,
            MajorId = 1,
            IsProfileCompleted = true
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var authService = new AuthService(db);
        var sendResult = await authService.SendResetOtpAsync("SE170088");
        Assert.True(sendResult.IsSuccess);
        string otpCode = sendResult.DevOtpCode!;

        // Act
        var resetResult = await authService.ResetPasswordWithOtpAsync("SE170088", otpCode, "NewSecret@2026");

        // Assert
        Assert.True(resetResult.IsSuccess);
        Assert.NotNull(resetResult.User);

        // Verify login works with new password
        var loginResult = await authService.LoginAsync("SE170088", "NewSecret@2026");
        Assert.True(loginResult.IsSuccess);
    }
}


