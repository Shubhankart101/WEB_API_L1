using TheAuctionHouse.Common.ErrorHandling;

namespace TheAuctionHouse.Domain.ServiceContracts;

public interface IPortalUserService
{
    Task<Result<bool>> SignUpAsync(SignUpRequest signUpRequest);
    Task<Result<string>> LoginAsync(LoginRequest loginRequest);
    Task<Result<bool>> LogoutAsync(int UserId);
    Task<Result<bool>> ForgotPasswordAsync(ForgotPasswordRequest forgotPasswordRequest);
    Task<Result<bool>> ResetPasswordAsync(ResetPasswordRequest resetPasswordRequest);
    Task<Result<PortalUserResponse>> GetUserProfileAsync(int userId);
    Task<Result<bool>> RegeneratePasswordAsync(int actorUserId, AdminRegeneratePasswordRequest request);
    Task<Result<bool>> SetUserAccessAsync(int actorUserId, SetUserAccessRequest request);
    Task<Result<bool>> DeleteUserAsync(int actorUserId, int targetUserId);
    Task<Result<List<UserAuditLogResponse>>> GetUserActivityAsync(int actorUserId, int? targetUserId);
}
