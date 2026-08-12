using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Xunit;
using TheAuctionHouse.Domain.Entities;
using TheAuctionHouse.Domain.DataContracts;
using TheAuctionHouse.Common.ErrorHandling;

namespace TheAuctionHouse.Domain.Services.Tests;

public class AuctionServiceTests_Extended
{
    private readonly Mock<IAppUnitOfWork> _uow = new();
    private readonly Mock<IAuctionRepository> _auctionRepo = new();
    private readonly Mock<IAssetRepository> _assetRepo = new();
    private readonly Mock<IWalletRepository> _walletRepo = new();
    private readonly AuctionService _service;

    public AuctionServiceTests_Extended()
    {
        _uow.Setup(u => u.AuctionRepository).Returns(_auctionRepo.Object);
        _uow.Setup(u => u.AssetRepository).Returns(_assetRepo.Object);
        _uow.Setup(u => u.WalletRepository).Returns(_walletRepo.Object);
        _service = new AuctionService(_uow.Object);
    }

    [Fact]
    public async Task PostAuctionAsync_InvalidMinimumBidIncrement_ReturnsError()
    {
        var req = new PostAuctionRequest { AssetId = 1, OwnerId = 1, ReservedPrice = 100, MinimumBidIncrement = 0, TotalMinutesToExpiry = 60 };

        var result = await _service.PostAuctionAsync(req);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.Error.ErrorCode);
    }

    [Fact]
    public async Task PostAuctionAsync_InvalidExpiryTime_ReturnsError()
    {
        var req = new PostAuctionRequest { AssetId = 1, OwnerId = 1, ReservedPrice = 100, MinimumBidIncrement = 10, TotalMinutesToExpiry = 0 };

        var result = await _service.PostAuctionAsync(req);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.Error.ErrorCode);
    }

    [Fact]
    public async Task PlaceBidAsync_AuctionNotFound_ReturnsNotFound()
    {
        _auctionRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Auction?)null);

        var result = await _service.PlaceBidAsync(new PlaceBidRequest { AuctionId = 99, UserId = 1, BidAmount = 100 });

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.Error.ErrorCode);
    }

    [Fact]
    public async Task PlaceBidAsync_ExpiredAuction_ReturnsError()
    {
        var auction = new Auction
        {
            Id = 1, ReservedPrice = 100, MinimumBidIncrement = 10,
            StartDate = DateTime.UtcNow.AddMinutes(-120),
            TotalMinutesToExpiry = 60,
            Status = AuctionStatus.Live
        };
        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);

        var result = await _service.PlaceBidAsync(new PlaceBidRequest { AuctionId = 1, UserId = 1, BidAmount = 100 });

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.Error.ErrorCode);
        Assert.Contains("expired", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PlaceBidAsync_BidTooLow_ReturnsError()
    {
        var auction = new Auction
        {
            Id = 1, ReservedPrice = 100, MinimumBidIncrement = 10,
            CurrentHighestBid = 200,
            StartDate = DateTime.UtcNow, TotalMinutesToExpiry = 120,
            Status = AuctionStatus.Live
        };
        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
        _walletRepo.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(new Wallet { UserId = 1, Amount = 1000 });

        // Next required bid is 200 + 10 = 210, but bidding only 150
        var result = await _service.PlaceBidAsync(new PlaceBidRequest { AuctionId = 1, UserId = 1, BidAmount = 150 });

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.Error.ErrorCode);
    }

    [Fact]
    public async Task PlaceBidAsync_InsufficientWalletBalance_ReturnsError()
    {
        var auction = new Auction
        {
            Id = 1, ReservedPrice = 100, MinimumBidIncrement = 10,
            CurrentHighestBid = 0,
            StartDate = DateTime.UtcNow, TotalMinutesToExpiry = 120,
            Status = AuctionStatus.Live
        };
        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
        // Available = 50 - 30 blocked = 20, bid 100 → insufficient
        _walletRepo.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(new Wallet { UserId = 1, Amount = 50, BlockedAmount = 30 });

        var result = await _service.PlaceBidAsync(new PlaceBidRequest { AuctionId = 1, UserId = 1, BidAmount = 100 });

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.Error.ErrorCode);
        Assert.Contains("Insufficient", result.Error.Message);
    }

    [Fact]
    public async Task PlaceBidAsync_ValidBid_BlocksFundsAndUpdatesAuction()
    {
        var auction = new Auction
        {
            Id = 1, ReservedPrice = 100, MinimumBidIncrement = 10,
            CurrentHighestBid = 0, CurrentHighestBidderId = 0,
            StartDate = DateTime.UtcNow, TotalMinutesToExpiry = 120,
            Status = AuctionStatus.Live
        };
        var wallet = new Wallet { UserId = 2, Amount = 500, BlockedAmount = 0 };

        _auctionRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
        _walletRepo.Setup(r => r.GetByUserIdAsync(2)).ReturnsAsync(wallet);
        _auctionRepo.Setup(r => r.UpdateAsync(It.IsAny<Auction>())).Returns(Task.CompletedTask);
        _auctionRepo.Setup(r => r.AddAsync(It.IsAny<BidHistory>())).Returns(Task.CompletedTask);
        _walletRepo.Setup(r => r.UpdateAsync(It.IsAny<Wallet>())).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _service.PlaceBidAsync(new PlaceBidRequest { AuctionId = 1, UserId = 2, BidAmount = 100 });

        Assert.True(result.IsSuccess);
        Assert.Equal(100, wallet.BlockedAmount);
        Assert.Equal(100, auction.CurrentHighestBid);
        Assert.Equal(2, auction.CurrentHighestBidderId);
    }

    [Fact]
    public async Task CheckAuctionExpiriesAsync_NoBids_AssetMovedToOpen()
    {
        var asset = new Asset { Id = 1, Status = AssetStatus.ClosedForAuction, UserId = 1 };
        var auction = new Auction
        {
            Id = 1, AssetId = 1,
            StartDate = DateTime.UtcNow.AddMinutes(-120),
            TotalMinutesToExpiry = 60,
            CurrentHighestBid = 0, CurrentHighestBidderId = 0,
            Status = AuctionStatus.Live
        };

        _auctionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Auction> { auction });
        _assetRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(asset);
        _auctionRepo.Setup(r => r.UpdateAsync(It.IsAny<Auction>())).Returns(Task.CompletedTask);
        _assetRepo.Setup(r => r.UpdateAsync(It.IsAny<Asset>())).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _service.CheckAuctionExpiriesAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(AssetStatus.OpenToAuction, asset.Status);
        Assert.Equal(AuctionStatus.ExpiredWithoutBids, auction.Status);
    }

    [Fact]
    public async Task CheckAuctionExpiriesAsync_WithBids_OwnershipTransferred()
    {
        var asset = new Asset { Id = 1, Status = AssetStatus.ClosedForAuction, UserId = 1 };
        var buyerWallet = new Wallet { UserId = 2, Amount = 500, BlockedAmount = 200 };
        var sellerWallet = new Wallet { UserId = 1, Amount = 100, BlockedAmount = 0 };
        var auction = new Auction
        {
            Id = 1, AssetId = 1, UserId = 1,
            StartDate = DateTime.UtcNow.AddMinutes(-120),
            TotalMinutesToExpiry = 60,
            CurrentHighestBid = 200, CurrentHighestBidderId = 2,
            Status = AuctionStatus.Live
        };

        _auctionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Auction> { auction });
        _assetRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(asset);
        _walletRepo.Setup(r => r.GetByUserIdAsync(2)).ReturnsAsync(buyerWallet);
        _walletRepo.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(sellerWallet);
        _auctionRepo.Setup(r => r.UpdateAsync(It.IsAny<Auction>())).Returns(Task.CompletedTask);
        _assetRepo.Setup(r => r.UpdateAsync(It.IsAny<Asset>())).Returns(Task.CompletedTask);
        _walletRepo.Setup(r => r.UpdateAsync(It.IsAny<Wallet>())).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _service.CheckAuctionExpiriesAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, asset.UserId);
        Assert.Equal(AssetStatus.OpenToAuction, asset.Status);
        Assert.Equal(300, buyerWallet.Amount);   // 500 - 200 deducted
        Assert.Equal(300, sellerWallet.Amount);  // 100 + 200 received
    }
}
