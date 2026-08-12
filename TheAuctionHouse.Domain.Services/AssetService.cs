using System.Text.RegularExpressions;
using TheAuctionHouse.Common.ErrorHandling;
using TheAuctionHouse.Domain.Entities;
using TheAuctionHouse.Domain.ServiceContracts;
using TheAuctionHouse.Domain.DataContracts;
using System.Linq;

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

    public async Task<Result<bool>> CreateAssetAsync(AssetInformationUpdateRequest request, int userId)
    {
        var validation = ValidateAssetInput(request.Title, request.Description, request.RetailPrice);
        if (!validation.IsSuccess)
        {
            return validation;
        }

        var asset = new Asset
        {
            UserId = userId,
            Title = NormalizeText(request.Title),
            Description = request.Description.Trim(),
            RetailValue = request.RetailPrice,
            Status = AssetStatus.Draft
        };

        await _unitOfWork.AssetRepository.AddAsync(asset);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> DeleteAssetAsync(int assetId)
    {
        var asset = await _unitOfWork.AssetRepository.GetByIdAsync(assetId);
        if (asset is null)
        {
            return Result<bool>.Failure(Error.NotFound("Asset not found."));
        }

        if (asset.Status != AssetStatus.Draft && asset.Status != AssetStatus.OpenToAuction)
        {
            return Result<bool>.Failure(Error.BadRequest("Only assets in Draft or OpenToAuction status can be deleted."));
        }

        await _unitOfWork.AuctionRepository.DeleteAuctionsByAssetIdAsync(assetId);
        await _unitOfWork.AssetRepository.DeleteAsync(asset);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true);
    }

    public async Task<Result<List<AssetResponse>>> GetAllAssetsByUserIdAsync(int userId)
    {
        var assets = await _unitOfWork.AssetRepository.GetAssetsByUserIdAsync(userId);
        var owner = await _unitOfWork.PortalUserRepository.GetUserByUserIdAsync(userId);
        var ownerName = owner?.Name ?? string.Empty;

        var response = assets.Select(asset => new AssetResponse
        {
            AssetId = asset.Id,
            Title = asset.Title,
            Description = asset.Description,
            RetailPrice = asset.RetailValue,
            OwnerName = ownerName,
            Status = asset.Status.ToString()
        }).ToList();

        return Result<List<AssetResponse>>.Success(response);
    }

    public async Task<Result<AssetResponse>> GetAssetByIdAsync(int assetId)
    {
        var asset = await _unitOfWork.AssetRepository.GetByIdAsync(assetId);
        if (asset is null)
        {
            return Result<AssetResponse>.Failure(Error.NotFound("Asset not found."));
        }

        var owner = await _unitOfWork.PortalUserRepository.GetUserByUserIdAsync(asset.UserId);
        return Result<AssetResponse>.Success(new AssetResponse
        {
            AssetId = asset.Id,
            Title = asset.Title,
            Description = asset.Description,
            RetailPrice = asset.RetailValue,
            OwnerName = owner?.Name ?? string.Empty,
            Status = asset.Status.ToString()
        });
    }

    public async Task<PortalUserResponse> GetPortalUserByEmailAsync(string email)
    {
        var user = await _unitOfWork.PortalUserRepository.GetUserByEmailAsync(email);
        if (user is null)
        {
            return new PortalUserResponse();
        }

        return new PortalUserResponse
        {
            UserId = user.Id,
            Name = user.Name,
            EmailId = user.EmailId
        };
    }

    public async Task<Result<bool>> UpdateAssetAsync(AssetInformationUpdateRequest updateAssetRequest)
    {
        var allAssets = await _unitOfWork.AssetRepository.GetAllAsync();
        Asset? existingAsset = null;

        if (updateAssetRequest.AssetId > 0)
        {
            existingAsset = allAssets.FirstOrDefault(asset => asset.Id == updateAssetRequest.AssetId);
        }

        if (existingAsset is null)
        {
            return Result<bool>.Failure(Error.NotFound("Asset not found."));
        }

        if (existingAsset.Status != AssetStatus.Draft)
        {
            return Result<bool>.Failure(Error.BadRequest("Only assets in Draft status can be updated."));
        }

        var validation = ValidateAssetInput(updateAssetRequest.Title, updateAssetRequest.Description, updateAssetRequest.RetailPrice);
        if (!validation.IsSuccess)
        {
            return validation;
        }

        existingAsset.Title = NormalizeText(updateAssetRequest.Title);
        existingAsset.Description = updateAssetRequest.Description.Trim();
        existingAsset.RetailValue = updateAssetRequest.RetailPrice;

        await _unitOfWork.AssetRepository.UpdateAsync(existingAsset);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true);
    }

    private static Result<bool> ValidateAssetInput(string title, string description, int retailPrice)
    {
        var normalizedTitle = NormalizeText(title);
        if (string.IsNullOrWhiteSpace(normalizedTitle) || normalizedTitle.Length < 10 || normalizedTitle.Length > 150)
        {
            return Result<bool>.Failure(Error.BadRequest("Title should be between 10 and 150 characters."));
        }

        if (!Regex.IsMatch(normalizedTitle, @"^[a-zA-Z0-9 ]+$"))
        {
            return Result<bool>.Failure(Error.BadRequest("Title should not contain special characters."));
        }

        var trimmedDescription = description?.Trim() ?? string.Empty;
        if (trimmedDescription.Length < 10 || trimmedDescription.Length > 1000)
        {
            return Result<bool>.Failure(Error.BadRequest("Description should be between 10 and 1000 characters."));
        }

        if (retailPrice <= 0)
        {
            return Result<bool>.Failure(Error.BadRequest("Retail value should be a positive integer."));
        }

        return Result<bool>.Success(true);
    }

    private static string NormalizeText(string input)
    {
        var trimmed = (input ?? string.Empty).Trim();
        return Regex.Replace(trimmed, @"\s+", " ");
    }
}