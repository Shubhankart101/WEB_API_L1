using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheAuctionHouse.Domain.ServiceContracts;

namespace TheAuctionHouse.API.Controllers;

[Route("api/[controller]")]
public class PortalUserController : ApiControllerBase
{
    private readonly IPortalUserService _portalUserService;

    public PortalUserController(IPortalUserService portalUserService)
    {
        _portalUserService = portalUserService;
    }

    [HttpPost("signup")]
    [AllowAnonymous]
    public async Task<IActionResult> SignUp([FromBody] SignUpRequest request)
    {
        var result = await _portalUserService.SignUpAsync(request);
        return HandleResult(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _portalUserService.LoginAsync(request);
        if (!result.IsSuccess)
        {
            return HandleResult(result);
        }

        return Ok(new { Token = result.Value });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var result = await _portalUserService.ForgotPasswordAsync(request);
        return HandleResult(result);
    }

    [HttpPost("reset-password")]
    [Authorize]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        request.UserId = userId.Value;
        var result = await _portalUserService.ResetPasswordAsync(request);
        return HandleResult(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _portalUserService.LogoutAsync(userId.Value);
        return HandleResult(result);
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _portalUserService.GetUserProfileAsync(userId.Value);
        return HandleResult(result);
    }
}
