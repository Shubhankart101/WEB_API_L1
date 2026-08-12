using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheAuctionHouse.Domain.ServiceContracts;

namespace TheAuctionHouse.API.Controllers;

[Route("api/[controller]")]
[Authorize]
public class AuctionController : ApiControllerBase
{
    private readonly IAuctionService _auctionService;

    public AuctionController(IAuctionService auctionService)
    {
        _auctionService = auctionService;
    }

    [HttpPost]
    public async Task<IActionResult> PostAuction([FromBody] PostAuctionRequest request)
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        request.OwnerId = userId.Value;
        var result = await _auctionService.PostAuctionAsync(request);
        return HandleResult(result);
    }

    [HttpPost("bid")]
    public async Task<IActionResult> PlaceBid([FromBody] PlaceBidRequest request)
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        request.UserId = userId.Value;
        var result = await _auctionService.PlaceBidAsync(request);
        return HandleResult(result);
    }

    [HttpPost("check-expiries")]
    public async Task<IActionResult> CheckAuctionExpiries()
    {
        var result = await _auctionService.CheckAuctionExpiriesAsync();
        return HandleResult(result);
    }

    [HttpGet("{auctionId:int}")]
    public async Task<IActionResult> GetById(int auctionId)
    {
        var result = await _auctionService.GetAuctionByIdAsync(auctionId);
        return HandleResult(result);
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _auctionService.GetAuctionsByUserIdAsync(userId.Value);
        return HandleResult(result);
    }

    [HttpGet("open")]
    [AllowAnonymous]
    public async Task<IActionResult> GetOpen()
    {
        var result = await _auctionService.GetAllOpenAuctionsByUserIdAsync();
        return HandleResult(result);
    }
}
