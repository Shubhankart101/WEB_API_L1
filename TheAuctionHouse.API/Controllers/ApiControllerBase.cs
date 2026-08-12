using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TheAuctionHouse.Common.ErrorHandling;

namespace TheAuctionHouse.API.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return result.Error.ErrorCode switch
        {
            400 => BadRequest(result.Error),
            401 => Unauthorized(result.Error),
            404 => NotFound(result.Error),
            422 => UnprocessableEntity(result.Error),
            _ => StatusCode(500, result.Error)
        };
    }

    protected int? GetAuthenticatedUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        return null;
    }
}
