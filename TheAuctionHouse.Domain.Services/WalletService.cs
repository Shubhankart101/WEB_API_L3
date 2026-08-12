using TheAuctionHouse.Common.ErrorHandling;
using TheAuctionHouse.Domain.Entities;
using TheAuctionHouse.Domain.ServiceContracts;
using TheAuctionHouse.Domain.DataContracts;

namespace TheAuctionHouse.Domain.Services;

public class WalletService : IWalletService
{
    private readonly IAppUnitOfWork _unitOfWork;

    public WalletService(IAppUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<bool>> DepositAsync(WalletTransactionRequest walletTransactionRequest)
    {
        if (walletTransactionRequest.Amount <= 0 || walletTransactionRequest.Amount > 999999)
            return Result<bool>.Failure(Error.BadRequest("Amount must be a positive integer not exceeding $999,999."));

        var wallet = await _unitOfWork.WalletRepository.GetByUserIdAsync(walletTransactionRequest.UserId);
        if (wallet == null)
        {
            var newWallet = new Wallet { UserId = walletTransactionRequest.UserId, Amount = walletTransactionRequest.Amount };
            await _unitOfWork.WalletRepository.AddAsync(newWallet);
        }
        else
        {
            wallet.Amount += walletTransactionRequest.Amount;
            await _unitOfWork.WalletRepository.UpdateAsync(wallet);
        }

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> WithDrawalAsync(WalletTransactionRequest walletTransactionRequest)
    {
        if (walletTransactionRequest.Amount <= 0 || walletTransactionRequest.Amount > 999999)
            return Result<bool>.Failure(Error.BadRequest("Amount must be a positive integer not exceeding $999,999."));

        var wallet = await _unitOfWork.WalletRepository.GetByUserIdAsync(walletTransactionRequest.UserId);
        if (wallet == null)
            return Result<bool>.Failure(Error.NotFound("Wallet not found."));

        var availableBalance = wallet.Amount - wallet.BlockedAmount;
        if (availableBalance < walletTransactionRequest.Amount)
            return Result<bool>.Failure(Error.BadRequest("Insufficient available balance for withdrawal."));

        wallet.Amount -= walletTransactionRequest.Amount;
        await _unitOfWork.WalletRepository.UpdateAsync(wallet);

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> BlockAmountAsync(WalletTransactionRequest walletTransactionRequest)
    {
        if (walletTransactionRequest.Amount <= 0)
            return Result<bool>.Failure(Error.BadRequest("Amount must be a positive integer."));

        var wallet = await _unitOfWork.WalletRepository.GetByUserIdAsync(walletTransactionRequest.UserId);
        if (wallet == null)
            return Result<bool>.Failure(Error.NotFound("Wallet not found."));

        var availableBalance = wallet.Amount - wallet.BlockedAmount;
        if (availableBalance < walletTransactionRequest.Amount)
            return Result<bool>.Failure(Error.BadRequest("Insufficient available balance to block."));

        wallet.BlockedAmount += walletTransactionRequest.Amount;
        await _unitOfWork.WalletRepository.UpdateAsync(wallet);

        return Result<bool>.Success(true);
    }

    public async Task<Result<WalletBalenceResponse>> GetWalletBalenceAsync(int userId)
    {
        var wallet = await _unitOfWork.WalletRepository.GetByUserIdAsync(userId);
        if (wallet == null)
            return Result<WalletBalenceResponse>.Failure(Error.NotFound("Wallet not found."));

        var bidHistories = await _unitOfWork.AuctionRepository.GetBidHistoriesByUserIdAsync(userId);

        var response = new WalletBalenceResponse
        {
            UserId = wallet.UserId,
            Amount = wallet.Amount,
            BlockedAmount = wallet.BlockedAmount,
            BidHistory = bidHistories.Select(b => new BidHistoryResponse
            {
                BidId = b.Id,
                AuctionId = b.AuctionId,
                UserId = b.BidderId,
                BidAmount = b.BidAmount,
                BidTime = b.BidDate,
                UserName = b.BidderName
            }).ToList()
        };

        return Result<WalletBalenceResponse>.Success(response);
    }
}