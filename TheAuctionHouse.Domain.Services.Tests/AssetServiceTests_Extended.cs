using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Xunit;
using TheAuctionHouse.Domain.Entities;
using TheAuctionHouse.Domain.ServiceContracts;
using TheAuctionHouse.Domain.Services;
using TheAuctionHouse.Domain.DataContracts;
using TheAuctionHouse.Common.ErrorHandling;

namespace TheAuctionHouse.Domain.Services.Tests;

public class AssetServiceTests_Extended
{
    private readonly Mock<IAppUnitOfWork> _uow = new();
    private readonly Mock<IAssetRepository> _assetRepo = new();
    private readonly Mock<IAuctionService> _auctionSvc = new();
    private readonly AssetService _service;

    public AssetServiceTests_Extended()
    {
        _uow.Setup(u => u.AssetRepository).Returns(_assetRepo.Object);
        _service = new AssetService(_uow.Object, _auctionSvc.Object);
    }

    [Fact]
    public async Task DeleteAssetAsync_DraftAsset_ReturnsSuccess()
    {
        var asset = new Asset { Id = 1, Status = AssetStatus.Draft };
        _assetRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(asset);
        _uow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _service.DeleteAssetAsync(1);

        Assert.True(result.IsSuccess);
        _assetRepo.Verify(r => r.DeleteAsync(asset), Times.Once);
    }

    [Fact]
    public async Task DeleteAssetAsync_OpenAsset_ReturnsSuccess()
    {
        var asset = new Asset { Id = 2, Status = AssetStatus.OpenToAuction };
        _assetRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(asset);
        _uow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _service.DeleteAssetAsync(2);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DeleteAssetAsync_ClosedAsset_ReturnsError()
    {
        var asset = new Asset { Id = 3, Status = AssetStatus.ClosedForAuction };
        _assetRepo.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(asset);

        var result = await _service.DeleteAssetAsync(3);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.Error.ErrorCode);
    }

    [Fact]
    public async Task DeleteAssetAsync_NotFound_ReturnsNotFound()
    {
        _assetRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Asset?)null);

        var result = await _service.DeleteAssetAsync(99);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.Error.ErrorCode);
    }

    [Fact]
    public async Task GetAssetByIdAsync_Found_ReturnsAssetResponse()
    {
        var asset = new Asset { Id = 1, Title = "Test Asset 01", Description = "Some description here", RetailValue = 500, Status = AssetStatus.Draft };
        _assetRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(asset);

        var result = await _service.GetAssetByIdAsync(1);

        Assert.True(result.IsSuccess);
        Assert.Equal("Test Asset 01", result.Value!.Title);
    }

    [Fact]
    public async Task GetAssetByIdAsync_NotFound_ReturnsNotFound()
    {
        _assetRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync((Asset?)null);

        var result = await _service.GetAssetByIdAsync(5);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.Error.ErrorCode);
    }

    [Fact]
    public async Task GetAllAssetsByUserIdAsync_ReturnsUserAssets()
    {
        var assets = new List<Asset>
        {
            new() { Id = 1, UserId = 1, Title = "Asset One One", Description = "desc one", RetailValue = 100 },
            new() { Id = 2, UserId = 1, Title = "Asset Two Two", Description = "desc two", RetailValue = 200 }
        };
        _assetRepo.Setup(r => r.GetAssetsByUserIdAsync(1)).ReturnsAsync(assets);

        var result = await _service.GetAllAssetsByUserIdAsync(1);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
    }

    [Fact]
    public async Task CreateAssetAsync_TitleWithSpecialChars_ReturnsError()
    {
        var req = new AssetInformationUpdateRequest { Title = "Invalid!@Title!!", Description = "Valid description here", RetailPrice = 100 };

        var result = await _service.CreateAssetAsync(req, 1);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.Error.ErrorCode);
        Assert.Contains("Title", result.Error.Message);
    }

    [Fact]
    public async Task CreateAssetAsync_DescriptionTooShort_ReturnsError()
    {
        var req = new AssetInformationUpdateRequest { Title = "Valid Title Here", Description = "Short", RetailPrice = 100 };

        var result = await _service.CreateAssetAsync(req, 1);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.Error.ErrorCode);
    }

    [Fact]
    public async Task CreateAssetAsync_ZeroRetailPrice_ReturnsError()
    {
        var req = new AssetInformationUpdateRequest { Title = "Valid Title Here", Description = "A valid description for this asset.", RetailPrice = 0 };

        var result = await _service.CreateAssetAsync(req, 1);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.Error.ErrorCode);
    }
}
