using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.Tasks;
using TheAuctionHouse.Common.ErrorHandling;
using TheAuctionHouse.Common.Validation;
using TheAuctionHouse.Domain.Entities;
using TheAuctionHouse.Domain.ServiceContracts;
using System.ComponentModel.DataAnnotations;
using System.Collections.Concurrent;
using System.Linq;

namespace TheAuctionHouse.Domain.Services;

public class PortalUserService : IPortalUserService
{
    private IAppUnitOfWork _appUnitOfWork;
    private IEmailService _emailService;
    private readonly string _jwtKey;
    private static readonly ConcurrentQueue<UserAuditLogResponse> _auditLogs = new();

    public PortalUserService(IAppUnitOfWork appUnitOfWork, IEmailService emailService, string jwtKey)
    {
        _appUnitOfWork = appUnitOfWork;
        _emailService = emailService;
        _jwtKey = jwtKey;
    }

    public async Task<Result<bool>> SignUpAsync(SignUpRequest signUpRequest)
    {
        if (string.IsNullOrWhiteSpace(signUpRequest.Name))
        {
            return Result<bool>.Failure(Error.BadRequest("Name is required."));
        }

        if (!new EmailAddressAttribute().IsValid(signUpRequest.EmailId))
        {
            return Result<bool>.Failure(Error.BadRequest("Valid email is required."));
        }

        if (string.IsNullOrWhiteSpace(signUpRequest.Password) || signUpRequest.Password.Length < 6)
        {
            return Result<bool>.Failure(Error.BadRequest("Password should be at least 6 characters."));
        }

        var existingUser = await _appUnitOfWork.PortalUserRepository.GetUserByEmailAsync(signUpRequest.EmailId);
        if (existingUser is not null)
        {
            return Result<bool>.Failure(Error.BadRequest("Email is already registered."));
        }

        var isFirstUser = !(await _appUnitOfWork.PortalUserRepository.GetAllAsync()).Any();

        var portalUser = new PortalUser
        {
            Name = signUpRequest.Name.Trim(),
            EmailId = signUpRequest.EmailId.Trim(),
            HashedPassword = HashPassword(signUpRequest.Password),
            IsAdmin = isFirstUser && signUpRequest.IsAdmin,
            IsBlocked = false,
            WalletBalence = 0,
            WalletBalenceBlocked = 0
        };

        await _appUnitOfWork.PortalUserRepository.AddAsync(portalUser);
        await _appUnitOfWork.SaveChangesAsync();
        await _appUnitOfWork.WalletRepository.AddAsync(new Wallet
        {
            UserId = portalUser.Id,
            Amount = 0,
            BlockedAmount = 0
        });
        await _appUnitOfWork.SaveChangesAsync();

        RecordAudit(portalUser.Id, portalUser.Id, "SignUp", $"User registered. IsAdmin={portalUser.IsAdmin}.");

        return Result<bool>.Success(true);
    }

    public async Task<Result<string>> LoginAsync(LoginRequest loginRequest)
    {
        if (!new EmailAddressAttribute().IsValid(loginRequest.EmailId) || string.IsNullOrWhiteSpace(loginRequest.Password))
        {
            return Result<string>.Failure(Error.BadRequest("Valid credentials are required."));
        }

        var user = await _appUnitOfWork.PortalUserRepository.GetUserByEmailAsync(loginRequest.EmailId);
        if (user is null || user.HashedPassword != HashPassword(loginRequest.Password))
        {
            return Result<string>.Failure(Error.BadRequest("Invalid email or password."));
        }

        if (user.IsBlocked)
        {
            return Result<string>.Failure(new Error(401, "User access is blocked."));
        }

        var token = GenerateJwtToken(user);
        RecordAudit(user.Id, user.Id, "Login", "User logged in successfully.");
        return Result<string>.Success(token);
    }

    public async Task<Result<bool>> LogoutAsync(int UserId)
    {
        var user = await _appUnitOfWork.PortalUserRepository.GetUserByUserIdAsync(UserId);
        if (user is null)
        {
            return Result<bool>.Failure(Error.NotFound("User not found."));
        }

        RecordAudit(UserId, UserId, "Logout", "User logged out.");
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ForgotPasswordAsync(ForgotPasswordRequest forgotPasswordRequest)
    {
        var validationError = Error.ValidationFailures();
        if (!ValidationHelper.Validate(forgotPasswordRequest, validationError))
        {
            return Result<bool>.Failure(validationError);
        }

        var user = await _appUnitOfWork.PortalUserRepository.GetUserByEmailAsync(forgotPasswordRequest.EmailId);
        if (user is null)
        {
            return Result<bool>.Failure(Error.NotFound("User not found."));
        }

        await _emailService.SendEmailAsync(forgotPasswordRequest.EmailId, "Password Reset | The Auction House", string.Empty, true);
        RecordAudit(user.Id, user.Id, "ForgotPassword", "Password reset mail requested.");
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ResetPasswordAsync(ResetPasswordRequest resetPasswordRequest)
    {
        if (string.IsNullOrWhiteSpace(resetPasswordRequest.NewPassword) || resetPasswordRequest.NewPassword.Length < 6)
        {
            return Result<bool>.Failure(Error.BadRequest("New password should be at least 6 characters."));
        }

        if (resetPasswordRequest.NewPassword != resetPasswordRequest.ConfirmPassword)
        {
            return Result<bool>.Failure(Error.BadRequest("New password and confirmation do not match."));
        }

        var user = await _appUnitOfWork.PortalUserRepository.GetUserByUserIdAsync(resetPasswordRequest.UserId);
        if (user is null)
        {
            return Result<bool>.Failure(Error.NotFound("User not found."));
        }

        if (user.HashedPassword != HashPassword(resetPasswordRequest.OldPassword))
        {
            return Result<bool>.Failure(Error.BadRequest("Old password is incorrect."));
        }

        user.HashedPassword = HashPassword(resetPasswordRequest.NewPassword);
        await _appUnitOfWork.PortalUserRepository.UpdateAsync(user);
        await _appUnitOfWork.SaveChangesAsync();

        RecordAudit(user.Id, user.Id, "ResetPassword", "User reset own password.");

        return Result<bool>.Success(true);
    }

    public async Task<Result<PortalUserResponse>> GetUserProfileAsync(int userId)
    {
        var user = await _appUnitOfWork.PortalUserRepository.GetUserByUserIdAsync(userId);
        if (user is null)
        {
            return Result<PortalUserResponse>.Failure(Error.NotFound("User not found."));
        }

        return Result<PortalUserResponse>.Success(new PortalUserResponse
        {
            UserId = user.Id,
            Name = user.Name,
            EmailId = user.EmailId
        });
    }

    public async Task<Result<bool>> RegeneratePasswordAsync(int actorUserId, AdminRegeneratePasswordRequest request)
    {
        var actor = await _appUnitOfWork.PortalUserRepository.GetUserByUserIdAsync(actorUserId);
        if (actor is null || !actor.IsAdmin)
        {
            return Result<bool>.Failure(new Error(401, "Only admin users can regenerate passwords."));
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            return Result<bool>.Failure(Error.BadRequest("New password should be at least 6 characters."));
        }

        var user = await _appUnitOfWork.PortalUserRepository.GetUserByUserIdAsync(request.TargetUserId);
        if (user is null)
        {
            return Result<bool>.Failure(Error.NotFound("User not found."));
        }

        user.HashedPassword = HashPassword(request.NewPassword);
        await _appUnitOfWork.PortalUserRepository.UpdateAsync(user);
        await _appUnitOfWork.SaveChangesAsync();

        RecordAudit(actorUserId, user.Id, "RegeneratePassword", "Admin regenerated user password.");
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> SetUserAccessAsync(int actorUserId, SetUserAccessRequest request)
    {
        var actor = await _appUnitOfWork.PortalUserRepository.GetUserByUserIdAsync(actorUserId);
        if (actor is null || !actor.IsAdmin)
        {
            return Result<bool>.Failure(new Error(401, "Only admin users can manage access."));
        }

        var user = await _appUnitOfWork.PortalUserRepository.GetUserByUserIdAsync(request.TargetUserId);
        if (user is null)
        {
            return Result<bool>.Failure(Error.NotFound("User not found."));
        }

        user.IsBlocked = request.IsBlocked;
        await _appUnitOfWork.PortalUserRepository.UpdateAsync(user);
        await _appUnitOfWork.SaveChangesAsync();

        RecordAudit(actorUserId, user.Id, "SetUserAccess", $"Access updated. IsBlocked={request.IsBlocked}.");
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> DeleteUserAsync(int actorUserId, int targetUserId)
    {
        var actor = await _appUnitOfWork.PortalUserRepository.GetUserByUserIdAsync(actorUserId);
        if (actor is null || !actor.IsAdmin)
        {
            return Result<bool>.Failure(new Error(401, "Only admin users can delete users."));
        }

        var user = await _appUnitOfWork.PortalUserRepository.GetUserByUserIdAsync(targetUserId);
        if (user is null)
        {
            return Result<bool>.Failure(Error.NotFound("User not found."));
        }

        await _appUnitOfWork.PortalUserRepository.DeleteAsync(user);
        await _appUnitOfWork.SaveChangesAsync();

        RecordAudit(actorUserId, targetUserId, "DeleteUser", "Admin deleted user account.");
        return Result<bool>.Success(true);
    }

    public async Task<Result<List<UserAuditLogResponse>>> GetUserActivityAsync(int actorUserId, int? targetUserId)
    {
        var actor = await _appUnitOfWork.PortalUserRepository.GetUserByUserIdAsync(actorUserId);
        if (actor is null || !actor.IsAdmin)
        {
            return Result<List<UserAuditLogResponse>>.Failure(new Error(401, "Only admin users can view activity logs."));
        }

        var logs = _auditLogs.ToList();
        if (targetUserId.HasValue)
        {
            logs = logs.Where(log => log.TargetUserId == targetUserId.Value || log.ActorUserId == targetUserId.Value).ToList();
        }

        return Result<List<UserAuditLogResponse>>.Success(logs.OrderByDescending(log => log.TimestampUtc).ToList());
    }

    private string GenerateJwtToken(PortalUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.EmailId),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User")
        };

        var token = new JwtSecurityToken(
            issuer: "TheAuctionHouse",
            audience: "TheAuctionHouseClient",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(4),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }

    private static void RecordAudit(int actorUserId, int? targetUserId, string action, string details)
    {
        _auditLogs.Enqueue(new UserAuditLogResponse
        {
            TimestampUtc = DateTime.UtcNow,
            ActorUserId = actorUserId,
            TargetUserId = targetUserId,
            Action = action,
            Details = details
        });

        while (_auditLogs.Count > 1000)
        {
            _auditLogs.TryDequeue(out _);
        }
    }
}
