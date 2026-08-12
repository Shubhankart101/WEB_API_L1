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

public class AssetServiceTests
{
    private readonly Mock<IAppUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IAssetRepository> _assetRepoMock;
    private readonly Mock<IAuctionRepository> _auctionRepoMock;
    private readonly Mock<IAuctionService> _auctionServiceMock;
    private readonly AssetService _service;

    public AssetServiceTests()
    {
        _unitOfWorkMock = new Mock<IAppUnitOfWork>();
        _assetRepoMock = new Mock<IAssetRepository>();
        _auctionRepoMock = new Mock<IAuctionRepository>();
        _auctionServiceMock = new Mock<IAuctionService>();
        _unitOfWorkMock.Setup(u => u.AssetRepository).Returns(_assetRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.AuctionRepository).Returns(_auctionRepoMock.Object);
        _assetRepoMock.Setup(r => r.DeleteAsync(It.IsAny<Asset>())).Returns(Task.CompletedTask);
        _auctionRepoMock.Setup(r => r.DeleteAuctionsByAssetIdAsync(It.IsAny<int>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _service = new AssetService(_unitOfWorkMock.Object, _auctionServiceMock.Object);
    }

    [Fact]
    public async Task CreateAssetAsync_WithValidRequest_ReturnsSuccess()
    {
        var request = new AssetInformationUpdateRequest
        {
            Title = "Valid Asset 01",
            Description = "This is a valid asset description.",
            RetailPrice = 1000
        };

        _assetRepoMock.Setup(r => r.AddAsync(It.IsAny<Asset>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _service.CreateAssetAsync(request,1);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CreateAssetAsync_WithInvalidTitle_ReturnsValidationError()
    {
        var request = new AssetInformationUpdateRequest
        {
            Title = "Short",
            Description = "Valid description for asset.",
            RetailPrice = 1000
        };

        var result = await _service.CreateAssetAsync(request,1);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.Error.ErrorCode);
        Assert.Contains("Title", result.Error.Message);
    }

    [Fact]
    public async Task UpdateAssetAsync_AssetNotFound_ReturnsNotFound()
    {
        var request = new AssetInformationUpdateRequest
        {
            Title = "Nonexistent Asset",
            Description = "Some description",
            RetailPrice = 1000
        };

        _assetRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Asset>());

        var result = await _service.UpdateAssetAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("Asset not found.", result.Error.Message);
    }

    [Fact]
    public async Task UpdateAssetAsync_AssetNotDraft_ReturnsValidationError()
    {
        var request = new AssetInformationUpdateRequest
        {
            AssetId = 1, // Set AssetId so the service can find the asset
            Title = "Existing Asset",
            Description = "Some description",
            RetailPrice = 1000
        };

        var asset = new Asset
        {
            Id = 1,
            Title = "Existing Asset",
            Description = "Old description",
            RetailValue = 500,
            Status = AssetStatus.ClosedForAuction
        };

        _assetRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Asset> { asset });

        var result = await _service.UpdateAssetAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.Error.ErrorCode);
        Assert.Contains("Draft status can be updated", result.Error.Message);
    }

    [Fact]
    public void AssetService_CanBeConstructed()
    {
        Assert.NotNull(_service);
    }

    [Fact]
    public async Task DeleteAssetAsync_WhenAssetIsClosedForAuction_ReturnsValidationError()
    {
        _assetRepoMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(new Asset
        {
            Id = 10,
            Status = AssetStatus.ClosedForAuction
        });

        var result = await _service.DeleteAssetAsync(10);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.Error.ErrorCode);
    }

    [Fact]
    public async Task DeleteAssetAsync_WhenAssetIsDraft_DeletesAssetSuccessfully()
    {
        var asset = new Asset
        {
            Id = 11,
            Status = AssetStatus.Draft
        };

        _assetRepoMock.Setup(r => r.GetByIdAsync(11)).ReturnsAsync(asset);

        var result = await _service.DeleteAssetAsync(11);

        Assert.True(result.IsSuccess);
        _auctionRepoMock.Verify(r => r.DeleteAuctionsByAssetIdAsync(11), Times.Once);
        _assetRepoMock.Verify(r => r.DeleteAsync(asset), Times.Once);
    }
}