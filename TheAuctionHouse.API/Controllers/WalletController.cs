using Microsoft.AspNetCore.Mvc;
using TheAuctionHouse.Domain.ServiceContracts;

namespace TheAuctionHouse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WalletController : ControllerBase
{
    private readonly IWalletService _walletService;
    public WalletController(IWalletService walletService) => _walletService = walletService;

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetBalance(int userId)
    {
        var result = await _walletService.GetWalletBalenceAsync(userId);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error.Message);
    }

    [HttpPost("deposit")]
    public async Task<IActionResult> Deposit([FromBody] WalletTransactionRequest request)
    {
        var result = await _walletService.DepositAsync(request);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error.Message);
    }

    [HttpPost("withdraw")]
    public async Task<IActionResult> Withdraw([FromBody] WalletTransactionRequest request)
    {
        var result = await _walletService.WithDrawalAsync(request);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error.Message);
    }
}
