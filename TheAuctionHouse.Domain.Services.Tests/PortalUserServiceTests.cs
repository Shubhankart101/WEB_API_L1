using Microsoft.EntityFrameworkCore;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TheAuctionHouse.Data.EFCore.InMemory;
using TheAuctionHouse.Domain.Entities;
using TheAuctionHouse.Domain.Services;

namespace TheAuctionHouse.Domain.Services.Tests;

public class PortalUserServiceTests
{
    private static IAppUnitOfWork CreateUnitOfWork()
    {
        var options = new DbContextOptionsBuilder<InMemoryAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new InMemoryAppUnitOfWork(new InMemoryAppDbContext(options));
    }

    [Fact]
    public async Task SignUpAsync_WithValidRequest_ShouldCreateUser()
    {
        var unitOfWork = CreateUnitOfWork();
        var emailServiceMock = new Mock<IEmailService>();
        var service = new PortalUserService(unitOfWork, emailServiceMock.Object, "test-jwt-key-12345678901234567890");

        var result = await service.SignUpAsync(new SignUpRequest
        {
            Name = "Test User",
            EmailId = "test.user@domain.com",
            Password = "Password@123"
        });

        Assert.True(result.IsSuccess);

        var createdUser = await unitOfWork.PortalUserRepository.GetUserByEmailAsync("test.user@domain.com");
        Assert.NotNull(createdUser);
        Assert.NotEqual("Password@123", createdUser.HashedPassword);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ShouldReturnToken()
    {
        var unitOfWork = CreateUnitOfWork();
        var emailServiceMock = new Mock<IEmailService>();
        var service = new PortalUserService(unitOfWork, emailServiceMock.Object, "test-jwt-key-12345678901234567890");

        await service.SignUpAsync(new SignUpRequest
        {
            Name = "Login User",
            EmailId = "login.user@domain.com",
            Password = "Password@123"
        });

        var result = await service.LoginAsync(new LoginRequest
        {
            EmailId = "login.user@domain.com",
            Password = "Password@123"
        });

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Value));
    }

    [Fact]
    public async Task ResetPasswordAsync_WithWrongOldPassword_ShouldFail()
    {
        var unitOfWork = CreateUnitOfWork();
        var emailServiceMock = new Mock<IEmailService>();
        var service = new PortalUserService(unitOfWork, emailServiceMock.Object, "test-jwt-key-12345678901234567890");

        await service.SignUpAsync(new SignUpRequest
        {
            Name = "Reset User",
            EmailId = "reset.user@domain.com",
            Password = "Password@123"
        });

        var user = await unitOfWork.PortalUserRepository.GetUserByEmailAsync("reset.user@domain.com");

        var result = await service.ResetPasswordAsync(new ResetPasswordRequest
        {
            UserId = user!.Id,
            OldPassword = "WrongOldPassword",
            NewPassword = "NewPassword@123",
            ConfirmPassword = "NewPassword@123"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.Error.ErrorCode);
    }

    [Fact]
    public async Task GetUserProfileAsync_WhenUserExists_ShouldReturnProfile()
    {
        var unitOfWork = CreateUnitOfWork();
        var emailServiceMock = new Mock<IEmailService>();
        var service = new PortalUserService(unitOfWork, emailServiceMock.Object, "test-jwt-key-12345678901234567890");

        await service.SignUpAsync(new SignUpRequest
        {
            Name = "Profile User",
            EmailId = "profile.user@domain.com",
            Password = "Password@123"
        });

        var user = await unitOfWork.PortalUserRepository.GetUserByEmailAsync("profile.user@domain.com");
        var result = await service.GetUserProfileAsync(user!.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("Profile User", result.Value!.Name);
        Assert.Equal("profile.user@domain.com", result.Value.EmailId);
    }

    [Fact]
    public async Task LoginAsync_AdminUserToken_ShouldContainAdminRoleClaim()
    {
        var unitOfWork = CreateUnitOfWork();
        var emailServiceMock = new Mock<IEmailService>();
        var service = new PortalUserService(unitOfWork, emailServiceMock.Object, "test-jwt-key-12345678901234567890");

        await service.SignUpAsync(new SignUpRequest
        {
            Name = "Admin User",
            EmailId = "admin.user@domain.com",
            Password = "Password@123",
            IsAdmin = true
        });

        var loginResult = await service.LoginAsync(new LoginRequest
        {
            EmailId = "admin.user@domain.com",
            Password = "Password@123"
        });

        Assert.True(loginResult.IsSuccess);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(loginResult.Value);
        Assert.Contains(jwt.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == "Admin");
    }

    [Fact]
    public async Task SetUserAccessAsync_BlockedUserLogin_ShouldFailWithUnauthorized()
    {
        var unitOfWork = CreateUnitOfWork();
        var emailServiceMock = new Mock<IEmailService>();
        var service = new PortalUserService(unitOfWork, emailServiceMock.Object, "test-jwt-key-12345678901234567890");

        await service.SignUpAsync(new SignUpRequest
        {
            Name = "Admin User",
            EmailId = "admin.block@domain.com",
            Password = "Password@123",
            IsAdmin = true
        });

        await service.SignUpAsync(new SignUpRequest
        {
            Name = "Normal User",
            EmailId = "normal.user@domain.com",
            Password = "Password@123"
        });

        var admin = await unitOfWork.PortalUserRepository.GetUserByEmailAsync("admin.block@domain.com");
        var user = await unitOfWork.PortalUserRepository.GetUserByEmailAsync("normal.user@domain.com");

        var blockResult = await service.SetUserAccessAsync(admin!.Id, new SetUserAccessRequest
        {
            TargetUserId = user!.Id,
            IsBlocked = true
        });

        Assert.True(blockResult.IsSuccess);

        var loginResult = await service.LoginAsync(new LoginRequest
        {
            EmailId = "normal.user@domain.com",
            Password = "Password@123"
        });

        Assert.False(loginResult.IsSuccess);
        Assert.Equal(401, loginResult.Error.ErrorCode);
    }

    [Fact]
    public async Task AdminOperations_ShouldSupportResetDeleteAndAudit()
    {
        var unitOfWork = CreateUnitOfWork();
        var emailServiceMock = new Mock<IEmailService>();
        var service = new PortalUserService(unitOfWork, emailServiceMock.Object, "test-jwt-key-12345678901234567890");

        await service.SignUpAsync(new SignUpRequest
        {
            Name = "Admin Master",
            EmailId = "master.admin@domain.com",
            Password = "Password@123",
            IsAdmin = true
        });

        await service.SignUpAsync(new SignUpRequest
        {
            Name = "Target User",
            EmailId = "target.user@domain.com",
            Password = "Password@123"
        });

        var admin = await unitOfWork.PortalUserRepository.GetUserByEmailAsync("master.admin@domain.com");
        var target = await unitOfWork.PortalUserRepository.GetUserByEmailAsync("target.user@domain.com");

        var resetResult = await service.RegeneratePasswordAsync(admin!.Id, new AdminRegeneratePasswordRequest
        {
            TargetUserId = target!.Id,
            NewPassword = "ChangedPass@123"
        });
        Assert.True(resetResult.IsSuccess);

        var loginWithNewPassword = await service.LoginAsync(new LoginRequest
        {
            EmailId = "target.user@domain.com",
            Password = "ChangedPass@123"
        });
        Assert.True(loginWithNewPassword.IsSuccess);

        var activityResult = await service.GetUserActivityAsync(admin.Id, target.Id);
        Assert.True(activityResult.IsSuccess);
        Assert.Contains(activityResult.Value!, log => log.Action == "RegeneratePassword");

        var deleteResult = await service.DeleteUserAsync(admin.Id, target.Id);
        Assert.True(deleteResult.IsSuccess);

        var deletedUser = await unitOfWork.PortalUserRepository.GetUserByUserIdAsync(target.Id);
        Assert.Null(deletedUser);
    }
}
