using FAT.Data;
using FAT.Services.Dtos;
using FAT.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FAT.Tests;

public class AuthServiceTests
{
    private static FatDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<FatDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var db = new FatDbContext(options);
        
        // Seed default roles
        db.Roles.Add(new Domain.Entities.Role { RoleId = 1, RoleName = Domain.Constants.RoleNames.Admin, Description = "Admin" });
        db.Roles.Add(new Domain.Entities.Role { RoleId = 2, RoleName = Domain.Constants.RoleNames.Student, Description = "Student" });
        db.Majors.Add(new Domain.Entities.Major { MajorId = 1, MajorCode = "SE", MajorName = "Software Engineering", RequiredCredits = 150, TotalTerms = 9, IsActive = true });
        db.SaveChanges();

        return db;
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsSuccess()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Student@123", workFactor: 11);
        var user = new Domain.Entities.AppUser
        {
            Username = "student01",
            PasswordHash = passwordHash,
            RoleId = 2,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var authService = new AuthService(db);

        // Act
        var result = await authService.LoginAsync("student01", "Student@123");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.User);
        Assert.Equal("student01", result.User.Username);
    }

    [Fact]
    public async Task LoginWithGoogleAsync_NonExistingUser_ReturnsAccountNotFound()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var authService = new AuthService(db);
        var googleUser = new GoogleUserInfoDto("google123", "newstudent@fpt.edu.vn", "New Student", null);

        // Act
        var result = await authService.LoginWithGoogleAsync(googleUser);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("ACCOUNT_NOT_FOUND", result.ErrorMessage);
    }

    [Fact]
    public async Task RegisterStudentAsync_ValidData_CreatesUserAndStudent()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var authService = new AuthService(db);
        var dto = new RegisterRequestDto(
            StudentCode: "SE170999",
            FullName: "Nguyễn Văn Test",
            Email: "test.student@fpt.edu.vn",
            Faculty: "Công nghệ Thông tin",
            MajorId: 1,
            Phone: "0912345678",
            Password: "Password@123",
            ConfirmPassword: "Password@123",
            AcceptTerms: true,
            GoogleId: "google999",
            AvatarUrl: "https://example.com/avatar.jpg"
        );

        // Act
        var result = await authService.RegisterStudentAsync(dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.User);
        Assert.Equal("SE170999", result.User.StudentCode);

        // Verify DB
        var savedStudent = await db.Students.FirstOrDefaultAsync(s => s.StudentCode == "SE170999");
        Assert.NotNull(savedStudent);
        Assert.Equal("test.student@fpt.edu.vn", savedStudent.Email);
    }
}
