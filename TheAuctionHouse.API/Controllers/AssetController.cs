using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TheAuctionHouse.API.Controllers;

[Route("api/[controller]")]
[Authorize]
public class AssetController : ApiControllerBase
{
    private readonly IAssetService _assetService;

    public AssetController(IAssetService assetService)
    {
        _assetService = assetService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AssetInformationUpdateRequest request)
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _assetService.CreateAssetAsync(request, userId.Value);
        return HandleResult(result);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] AssetInformationUpdateRequest request)
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        request.UserId = userId.Value;
        var result = await _assetService.UpdateAssetAsync(request);
        return HandleResult(result);
    }

    [HttpDelete("{assetId:int}")]
    public async Task<IActionResult> Delete(int assetId)
    {
        var result = await _assetService.DeleteAssetAsync(assetId);
        return HandleResult(result);
    }

    [HttpGet("{assetId:int}")]
    public async Task<IActionResult> GetById(int assetId)
    {
        var result = await _assetService.GetAssetByIdAsync(assetId);
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

        var result = await _assetService.GetAllAssetsByUserIdAsync(userId.Value);
        return HandleResult(result);
    }
}
