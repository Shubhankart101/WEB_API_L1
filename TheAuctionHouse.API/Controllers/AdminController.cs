using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheAuctionHouse.Domain.ServiceContracts;

namespace TheAuctionHouse.API.Controllers;

[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ApiControllerBase
{
    private readonly IPortalUserService _portalUserService;

    public AdminController(IPortalUserService portalUserService)
    {
        _portalUserService = portalUserService;
    }

    [HttpPost("regenerate-password")]
    public async Task<IActionResult> RegeneratePassword([FromBody] AdminRegeneratePasswordRequest request)
    {
        var actorUserId = GetAuthenticatedUserId();
        if (!actorUserId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _portalUserService.RegeneratePasswordAsync(actorUserId.Value, request);
        return HandleResult(result);
    }

    [HttpPost("access")]
    public async Task<IActionResult> SetUserAccess([FromBody] SetUserAccessRequest request)
    {
        var actorUserId = GetAuthenticatedUserId();
        if (!actorUserId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _portalUserService.SetUserAccessAsync(actorUserId.Value, request);
        return HandleResult(result);
    }

    [HttpDelete("users/{targetUserId:int}")]
    public async Task<IActionResult> DeleteUser(int targetUserId)
    {
        var actorUserId = GetAuthenticatedUserId();
        if (!actorUserId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _portalUserService.DeleteUserAsync(actorUserId.Value, targetUserId);
        return HandleResult(result);
    }

    [HttpGet("activity")]
    public async Task<IActionResult> GetActivity([FromQuery] int? targetUserId)
    {
        var actorUserId = GetAuthenticatedUserId();
        if (!actorUserId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _portalUserService.GetUserActivityAsync(actorUserId.Value, targetUserId);
        return HandleResult(result);
    }
}
