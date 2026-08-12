using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheAuctionHouse.Domain.ServiceContracts;

namespace TheAuctionHouse.API.Controllers;

[Route("api/[controller]")]
[Authorize]
public class WalletController : ApiControllerBase
{
    private readonly IWalletService _walletService;

    public WalletController(IWalletService walletService)
    {
        _walletService = walletService;
    }

    [HttpPost("deposit")]
    public async Task<IActionResult> Deposit([FromBody] WalletTransactionRequest request)
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        request.UserId = userId.Value;
        var result = await _walletService.DepositAsync(request);
        return HandleResult(result);
    }

    [HttpPost("withdraw")]
    public async Task<IActionResult> Withdraw([FromBody] WalletTransactionRequest request)
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        request.UserId = userId.Value;
        var result = await _walletService.WithDrawalAsync(request);
        return HandleResult(result);
    }

    [HttpGet("balance")]
    public async Task<IActionResult> Balance()
    {
        var userId = GetAuthenticatedUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _walletService.GetWalletBalenceAsync(userId.Value);
        return HandleResult(result);
    }
}
