using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TheAuctionHouse.Common.ErrorHandling;
using TheAuctionHouse.Domain.Entities;
using TheAuctionHouse.Domain.ServiceContracts;
using TheAuctionHouse.Domain.DataContracts;

public class AuctionService : IAuctionService
{
    private readonly IAppUnitOfWork _unitOfWork;

    public AuctionService(IAppUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<bool>> PostAuctionAsync(PostAuctionRequest postAuctionRequest)
    {
        if (postAuctionRequest.ReservedPrice <= 0 || postAuctionRequest.ReservedPrice > 9999)
            return Result<bool>.Failure(Error.BadRequest("Reserved price must be a non-zero positive integer not exceeding $9999."));

        if (postAuctionRequest.MinimumBidIncrement <= 0 || postAuctionRequest.MinimumBidIncrement > 999)
            return Result<bool>.Failure(Error.BadRequest("Incremental value must be a non-zero positive integer not exceeding $999."));

        if (postAuctionRequest.TotalMinutesToExpiry <= 0 || postAuctionRequest.TotalMinutesToExpiry > 10080)
            return Result<bool>.Failure(Error.BadRequest("Expiration time must be between 1 and 10080 minutes."));

        var asset = await _unitOfWork.AssetRepository.GetByIdAsync(postAuctionRequest.AssetId);
        if (asset == null)
            return Result<bool>.Failure(Error.NotFound("Asset not found."));

        var auction = new Auction
        {
            UserId = postAuctionRequest.OwnerId,
            AssetId = postAuctionRequest.AssetId,
            ReservedPrice = postAuctionRequest.ReservedPrice,
            CurrentHighestBid = 0,
            CurrentHighestBidderId = 0,
            MinimumBidIncrement = postAuctionRequest.MinimumBidIncrement,
            StartDate = DateTime.UtcNow,
            TotalMinutesToExpiry = postAuctionRequest.TotalMinutesToExpiry,
            Status = AuctionStatus.Live
        };

        asset.Status = AssetStatus.ClosedForAuction;
        await _unitOfWork.AssetRepository.UpdateAsync(asset);
        await _unitOfWork.AuctionRepository.AddAsync(auction);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> CheckAuctionExpiriesAsync()
    {
        var auctions = await _unitOfWork.AuctionRepository.GetAllAsync();
        var liveAuctions = auctions.Where(a => a.Status == AuctionStatus.Live).ToList();

        foreach (var auction in liveAuctions)
        {
            if (!auction.IsExpired())
                continue;

            var asset = await _unitOfWork.AssetRepository.GetByIdAsync(auction.AssetId);
            if (asset == null)
                continue;

            if (auction.IsExpiredWithoutBids())
            {
                auction.Status = AuctionStatus.ExpiredWithoutBids;
                asset.Status = AssetStatus.OpenToAuction;
            }
            else
            {
                auction.Status = AuctionStatus.Expired;
                asset.UserId = auction.CurrentHighestBidderId;
                asset.Status = AssetStatus.OpenToAuction;

                // Deduct blocked amount from winner's wallet and transfer to seller
                var buyerWallet = await _unitOfWork.WalletRepository.GetByUserIdAsync(auction.CurrentHighestBidderId);
                if (buyerWallet != null)
                {
                    buyerWallet.BlockedAmount -= auction.CurrentHighestBid;
                    buyerWallet.Amount -= auction.CurrentHighestBid;
                    await _unitOfWork.WalletRepository.UpdateAsync(buyerWallet);
                }

                var sellerWallet = await _unitOfWork.WalletRepository.GetByUserIdAsync(auction.UserId);
                if (sellerWallet != null)
                {
                    sellerWallet.Amount += auction.CurrentHighestBid;
                    await _unitOfWork.WalletRepository.UpdateAsync(sellerWallet);
                }
            }

            await _unitOfWork.AuctionRepository.UpdateAsync(auction);
            await _unitOfWork.AssetRepository.UpdateAsync(asset);
        }

        await _unitOfWork.SaveChangesAsync();
        return Result<bool>.Success(true);
    }

    public async Task<Result<AuctionResponse>> GetAuctionByIdAsync(int auctionId)
    {
        var auction = await _unitOfWork.AuctionRepository.GetByIdAsync(auctionId);
        if (auction == null)
            return Result<AuctionResponse>.Failure(Error.NotFound("Auction not found."));

        var asset = await _unitOfWork.AssetRepository.GetByIdAsync(auction.AssetId);
        var bidHistories = await _unitOfWork.AuctionRepository.GetBidHistoriesByAuctionIdAsync(auctionId);

        var response = MapToAuctionResponse(auction, asset, bidHistories);
        return Result<AuctionResponse>.Success(response);
    }

    public async Task<Result<List<AuctionResponse>>> GetAuctionsByUserIdAsync(int userId)
    {
        var auctions = await _unitOfWork.AuctionRepository.GetAuctionsByUserIdAsync(userId);
        var responses = new List<AuctionResponse>();

        foreach (var auction in auctions)
        {
            var asset = await _unitOfWork.AssetRepository.GetByIdAsync(auction.AssetId);
            var bidHistories = await _unitOfWork.AuctionRepository.GetBidHistoriesByAuctionIdAsync(auction.Id);
            responses.Add(MapToAuctionResponse(auction, asset, bidHistories));
        }

        return Result<List<AuctionResponse>>.Success(responses);
    }

    public async Task<Result<List<AuctionResponse>>> GetAllOpenAuctionsByUserIdAsync()
    {
        var allAuctions = await _unitOfWork.AuctionRepository.GetAllAsync();
        var liveAuctions = allAuctions.Where(a => a.Status == AuctionStatus.Live && !a.IsExpired()).ToList();

        var responses = new List<AuctionResponse>();
        foreach (var auction in liveAuctions)
        {
            var asset = await _unitOfWork.AssetRepository.GetByIdAsync(auction.AssetId);
            var bidHistories = await _unitOfWork.AuctionRepository.GetBidHistoriesByAuctionIdAsync(auction.Id);
            responses.Add(MapToAuctionResponse(auction, asset, bidHistories));
        }

        // Sort: auctions with user's highest bid first, then by nearest expiry
        responses = responses
            .OrderByDescending(r => r.CurrentHighestBid > 0)
            .ThenBy(r => r.StartDate.AddMinutes(r.TotalMinutesToExpiry))
            .ToList();

        return Result<List<AuctionResponse>>.Success(responses);
    }

    public async Task<Result<bool>> PlaceBidAsync(PlaceBidRequest placeBidRequest)
    {
        var auction = await _unitOfWork.AuctionRepository.GetByIdAsync(placeBidRequest.AuctionId);
        if (auction == null)
            return Result<bool>.Failure(Error.NotFound("Auction not found."));

        if (auction.IsExpired())
            return Result<bool>.Failure(Error.BadRequest("Auction has expired."));

        var nextCallAmount = auction.CurrentHighestBid == 0
            ? auction.ReservedPrice
            : auction.CurrentHighestBid + auction.MinimumBidIncrement;

        if (placeBidRequest.BidAmount < nextCallAmount)
            return Result<bool>.Failure(Error.BadRequest($"Bid amount must be at least {nextCallAmount}."));

        var buyerWallet = await _unitOfWork.WalletRepository.GetByUserIdAsync(placeBidRequest.UserId);
        if (buyerWallet == null)
            return Result<bool>.Failure(Error.BadRequest("Bidder wallet not found."));

        var availableBalance = buyerWallet.Amount - buyerWallet.BlockedAmount;
        if (availableBalance < placeBidRequest.BidAmount)
            return Result<bool>.Failure(Error.BadRequest("Insufficient wallet balance to place this bid."));

        // Unblock previous highest bidder's funds
        if (auction.CurrentHighestBidderId != 0)
        {
            var prevBidderWallet = await _unitOfWork.WalletRepository.GetByUserIdAsync(auction.CurrentHighestBidderId);
            if (prevBidderWallet != null)
            {
                prevBidderWallet.BlockedAmount -= auction.CurrentHighestBid;
                await _unitOfWork.WalletRepository.UpdateAsync(prevBidderWallet);
            }
        }

        // Block new bidder's funds
        buyerWallet.BlockedAmount += placeBidRequest.BidAmount;
        await _unitOfWork.WalletRepository.UpdateAsync(buyerWallet);

        // Update auction
        auction.CurrentHighestBid = placeBidRequest.BidAmount;
        auction.CurrentHighestBidderId = placeBidRequest.UserId;
        await _unitOfWork.AuctionRepository.UpdateAsync(auction);

        // Record bid history
        var bidHistory = new BidHistory
        {
            AuctionId = placeBidRequest.AuctionId,
            BidderId = placeBidRequest.UserId,
            BidAmount = placeBidRequest.BidAmount,
            BidDate = DateTime.UtcNow
        };
        await _unitOfWork.AuctionRepository.AddAsync(bidHistory);

        await _unitOfWork.SaveChangesAsync();
        return Result<bool>.Success(true);
    }

    private AuctionResponse MapToAuctionResponse(Auction auction, Asset? asset, List<BidHistory> bidHistories)
    {
        return new AuctionResponse
        {
            AuctionId = auction.Id,
            UserId = auction.UserId,
            AssetId = auction.AssetId,
            AssetTitle = asset?.Title ?? string.Empty,
            AssetDescription = asset?.Description ?? string.Empty,
            ReservedPrice = auction.ReservedPrice,
            CurrentHighestBid = auction.CurrentHighestBid,
            CurrentHighestBidderId = auction.CurrentHighestBidderId,
            MinimumBidIncrement = auction.MinimumBidIncrement,
            StartDate = auction.StartDate,
            TotalMinutesToExpiry = auction.TotalMinutesToExpiry,
            Status = auction.Status.ToString(),
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
    }
}