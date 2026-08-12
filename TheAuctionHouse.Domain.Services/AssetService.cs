using System.Text.RegularExpressions;
using TheAuctionHouse.Common.ErrorHandling;
using TheAuctionHouse.Domain.Entities;
using TheAuctionHouse.Domain.ServiceContracts;
using TheAuctionHouse.Domain.DataContracts;

namespace TheAuctionHouse.Domain.Services;

public class AssetService : IAssetService
{
    private readonly IAppUnitOfWork _unitOfWork;
    private readonly IAuctionService _auctionService;

    public AssetService(IAppUnitOfWork unitOfWork, IAuctionService auctionService)
    {
        _unitOfWork = unitOfWork;
        _auctionService = auctionService;
    }

    private (bool IsValid, string NormalizedTitle, string ErrorMessage) ValidateAndNormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return (false, string.Empty, "Title is required.");

        var normalized = Regex.Replace(title.Trim(), @"\s+", " ");

        if (normalized.Length < 10 || normalized.Length > 150)
            return (false, string.Empty, "Title must be between 10 and 150 characters.");

        if (!Regex.IsMatch(normalized, @"^[a-zA-Z0-9 ]+$"))
            return (false, string.Empty, "Title should not contain special characters.");

        return (true, normalized, string.Empty);
    }

    public async Task<Result<bool>> CreateAssetAsync(AssetInformationUpdateRequest request, int userId)
    {
        var (isValid, normalizedTitle, titleError) = ValidateAndNormalizeTitle(request.Title);
        if (!isValid)
            return Result<bool>.Failure(Error.BadRequest($"Title validation failed: {titleError}"));

        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length < 10 || request.Description.Length > 1000)
            return Result<bool>.Failure(Error.BadRequest("Description must be between 10 and 1000 characters."));

        if (request.RetailPrice <= 0)
            return Result<bool>.Failure(Error.BadRequest("Retail Value must be a positive integer."));

        var asset = new Asset
        {
            UserId = userId,
            Title = normalizedTitle,
            Description = request.Description,
            RetailValue = request.RetailPrice,
            Status = AssetStatus.Draft
        };

        await _unitOfWork.AssetRepository.AddAsync(asset);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> UpdateAssetAsync(AssetInformationUpdateRequest updateAssetRequest)
    {
        var assets = await _unitOfWork.AssetRepository.GetAllAsync();
        var asset = assets.FirstOrDefault(a => a.Id == updateAssetRequest.AssetId);

        if (asset == null)
            return Result<bool>.Failure(Error.NotFound("Asset not found."));

        if (asset.Status != AssetStatus.Draft)
            return Result<bool>.Failure(Error.BadRequest("Only assets in Draft status can be updated."));

        var (isValid, normalizedTitle, titleError) = ValidateAndNormalizeTitle(updateAssetRequest.Title);
        if (!isValid)
            return Result<bool>.Failure(Error.BadRequest($"Title validation failed: {titleError}"));

        if (string.IsNullOrWhiteSpace(updateAssetRequest.Description) || updateAssetRequest.Description.Length < 10 || updateAssetRequest.Description.Length > 1000)
            return Result<bool>.Failure(Error.BadRequest("Description must be between 10 and 1000 characters."));

        if (updateAssetRequest.RetailPrice <= 0)
            return Result<bool>.Failure(Error.BadRequest("Retail Value must be a positive integer."));

        asset.Title = normalizedTitle;
        asset.Description = updateAssetRequest.Description;
        asset.RetailValue = updateAssetRequest.RetailPrice;

        await _unitOfWork.AssetRepository.UpdateAsync(asset);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> DeleteAssetAsync(int assetId)
    {
        var asset = await _unitOfWork.AssetRepository.GetByIdAsync(assetId);

        if (asset == null)
            return Result<bool>.Failure(Error.NotFound("Asset not found."));

        if (asset.Status != AssetStatus.Draft && asset.Status != AssetStatus.OpenToAuction)
            return Result<bool>.Failure(Error.BadRequest("Only assets in Open or Draft status can be deleted."));

        await _unitOfWork.AssetRepository.DeleteAsync(asset);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true);
    }

    public async Task<Result<List<AssetResponse>>> GetAllAssetsByUserIdAsync(int userId)
    {
        var assets = await _unitOfWork.AssetRepository.GetAssetsByUserIdAsync(userId);
        var responses = assets.Select(a => new AssetResponse
        {
            AssetId = a.Id,
            Title = a.Title,
            Description = a.Description,
            RetailPrice = a.RetailValue,
            Status = a.Status.ToString()
        }).ToList();

        return Result<List<AssetResponse>>.Success(responses);
    }

    public async Task<Result<AssetResponse>> GetAssetByIdAsync(int assetId)
    {
        var asset = await _unitOfWork.AssetRepository.GetByIdAsync(assetId);

        if (asset == null)
            return Result<AssetResponse>.Failure(Error.NotFound("Asset not found."));

        return Result<AssetResponse>.Success(new AssetResponse
        {
            AssetId = asset.Id,
            Title = asset.Title,
            Description = asset.Description,
            RetailPrice = asset.RetailValue,
            Status = asset.Status.ToString()
        });
    }

    public async Task<PortalUserResponse> GetPortalUserByEmailAsync(string email)
    {
        var user = await _unitOfWork.PortalUserRepository.GetUserByEmailAsync(email);
        if (user == null)
            return null!;

        return new PortalUserResponse
        {
            UserId = user.Id,
            Name = user.Name,
            EmailId = user.EmailId
        };
    }
}