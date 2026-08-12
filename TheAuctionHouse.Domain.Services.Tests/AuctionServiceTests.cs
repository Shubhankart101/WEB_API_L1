using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Xunit;
using TheAuctionHouse.Domain.Entities;
using TheAuctionHouse.Domain.DataContracts;
using TheAuctionHouse.Common.ErrorHandling;
using TheAuctionHouse.Common; // or wherever Result<T> is defined

namespace TheAuctionHouse.Domain.Services.Tests;
public class AuctionServiceTests
{
    private readonly Mock<IAppUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IAuctionRepository> _auctionRepoMock;
    private readonly Mock<IAssetRepository> _assetRepoMock;
    private readonly Mock<IWalletRepository> _walletRepoMock;
    private readonly Mock<IPortalUserRepository> _portalUserRepoMock;
    private readonly AuctionService _service;

    public AuctionServiceTests()
    {
        _unitOfWorkMock = new Mock<IAppUnitOfWork>();
        _auctionRepoMock = new Mock<IAuctionRepository>();
        _assetRepoMock = new Mock<IAssetRepository>();
        _walletRepoMock = new Mock<IWalletRepository>();
        _portalUserRepoMock = new Mock<IPortalUserRepository>();

        _unitOfWorkMock.Setup(u => u.AuctionRepository).Returns(_auctionRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.AssetRepository).Returns(_assetRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.WalletRepository).Returns(_walletRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.PortalUserRepository).Returns(_portalUserRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _auctionRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Auction>())).Returns(Task.CompletedTask);
        _assetRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Asset>())).Returns(Task.CompletedTask);
        _walletRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Wallet>())).Returns(Task.CompletedTask);
        _walletRepoMock.Setup(r => r.AddAsync(It.IsAny<Wallet>())).Returns(Task.CompletedTask);

        _service = new AuctionService(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task PostAuctionAsync_WithInvalidReservedPrice_ReturnsValidationError()
    {
        var request = new PostAuctionRequest
        {
            AssetId = 1,
            OwnerId = 1,
            ReservedPrice = 0, // Invalid
            MinimumBidIncrement = 10,
            TotalMinutesToExpiry = 60
        };

        var result = await _service.PostAuctionAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.Error.ErrorCode);
        Assert.Contains("Reserved price", result.Error.Message);
    }

    [Fact]
    public async Task PostAuctionAsync_AssetNotFound_ReturnsNotFound()
    {
        var request = new PostAuctionRequest
        {
            AssetId = 99,
            OwnerId = 1,
            ReservedPrice = 100,
            MinimumBidIncrement = 10,
            TotalMinutesToExpiry = 60
        };

        _assetRepoMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Asset?)null);

        var result = await _service.PostAuctionAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.Error.ErrorCode);
        Assert.Contains("Asset not found", result.Error.Message);
    }

    [Fact]
    public async Task PostAuctionAsync_WithValidRequest_ReturnsSuccess()
    {
        var request = new PostAuctionRequest
        {
            AssetId = 1,
            OwnerId = 1,
            ReservedPrice = 100,
            MinimumBidIncrement = 10,
            TotalMinutesToExpiry = 60
        };

        _assetRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Asset { Id = 1, UserId = 1, Status = AssetStatus.OpenToAuction });
        _auctionRepoMock.Setup(r => r.AddAsync(It.IsAny<Auction>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var result =await _service.PostAuctionAsync(request);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetAuctionByIdAsync_AuctionNotFound_ReturnsNotFound()
    {
        _auctionRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((Auction?)null);

        var result = await _service.GetAuctionByIdAsync(1);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.Error.ErrorCode);
        Assert.Contains("Auction not found", result.Error.Message);
    }

    [Fact]
    public async Task GetAuctionByIdAsync_AuctionFound_ReturnsAuctionResponse()
    {
        var auction = new Auction
        {
            Id = 1,
            UserId = 2,
            AssetId = 3,
            ReservedPrice = 100,
            CurrentHighestBid = 0,
            CurrentHighestBidderId = 0,
            MinimumBidIncrement = 10,
            StartDate = DateTime.UtcNow,
            TotalMinutesToExpiry = 60,
            Status = AuctionStatus.Live
        };

        _auctionRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(auction);
        // Mock asset lookup
        _assetRepoMock.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(new Asset { Id = 3, Title = "Test Asset", Description = "Desc", RetailValue = 100, Status = AssetStatus.OpenToAuction });
        // Mock bid histories
        _auctionRepoMock.Setup(r => r.GetBidHistoriesByAuctionIdAsync(1)).ReturnsAsync(new List<BidHistory>());

        var result = await _service.GetAuctionByIdAsync(1);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(AuctionStatus.Live.ToString(), result.Value.Status);
    }

    [Fact]
    public async Task PlaceBidAsync_WithInsufficientWalletBalance_ReturnsError()
    {
        var auction = new Auction
        {
            Id = 10,
            UserId = 1,
            AssetId = 3,
            ReservedPrice = 100,
            CurrentHighestBid = 100,
            CurrentHighestBidderId = 2,
            MinimumBidIncrement = 10,
            StartDate = DateTime.UtcNow,
            TotalMinutesToExpiry = 120,
            Status = AuctionStatus.Live
        };

        _auctionRepoMock.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(auction);
        _walletRepoMock.Setup(r => r.GetByUserIdAsync(5)).ReturnsAsync(new Wallet
        {
            UserId = 5,
            Amount = 120,
            BlockedAmount = 50
        });

        var result = await _service.PlaceBidAsync(new PlaceBidRequest
        {
            AuctionId = 10,
            UserId = 5,
            BidAmount = 120
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.Error.ErrorCode);
    }

    [Fact]
    public async Task PlaceBidAsync_WithValidBid_UpdatesAuctionAndWallet()
    {
        var auction = new Auction
        {
            Id = 11,
            UserId = 1,
            AssetId = 3,
            ReservedPrice = 100,
            CurrentHighestBid = 100,
            CurrentHighestBidderId = 2,
            MinimumBidIncrement = 10,
            StartDate = DateTime.UtcNow,
            TotalMinutesToExpiry = 120,
            Status = AuctionStatus.Live
        };

        var currentBidderWallet = new Wallet { UserId = 2, Amount = 1000, BlockedAmount = 100 };
        var newBidderWallet = new Wallet { UserId = 5, Amount = 1000, BlockedAmount = 0 };

        _auctionRepoMock.Setup(r => r.GetByIdAsync(11)).ReturnsAsync(auction);
        _walletRepoMock.Setup(r => r.GetByUserIdAsync(5)).ReturnsAsync(newBidderWallet);
        _walletRepoMock.Setup(r => r.GetByUserIdAsync(2)).ReturnsAsync(currentBidderWallet);
        _portalUserRepoMock.Setup(r => r.GetUserByUserIdAsync(5)).ReturnsAsync(new PortalUser { Id = 5, Name = "Bidder 5" });
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _service.PlaceBidAsync(new PlaceBidRequest
        {
            AuctionId = 11,
            UserId = 5,
            BidAmount = 110
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(110, auction.CurrentHighestBid);
        Assert.Equal(5, auction.CurrentHighestBidderId);
        Assert.Equal(110, newBidderWallet.BlockedAmount);
        Assert.Equal(0, currentBidderWallet.BlockedAmount);
        _auctionRepoMock.Verify(r => r.AddAsync(It.IsAny<BidHistory>()), Times.Once);
    }

    [Fact]
    public async Task CheckAuctionExpiriesAsync_WithWinningBid_TransfersOwnershipAndSettlesWallets()
    {
        var expiredAuction = new Auction
        {
            Id = 21,
            UserId = 1,
            AssetId = 100,
            ReservedPrice = 100,
            CurrentHighestBid = 300,
            CurrentHighestBidderId = 2,
            MinimumBidIncrement = 10,
            StartDate = DateTime.UtcNow.AddMinutes(-120),
            TotalMinutesToExpiry = 60,
            Status = AuctionStatus.Live
        };

        var asset = new Asset
        {
            Id = 100,
            UserId = 1,
            Status = AssetStatus.ClosedForAuction
        };

        var buyerWallet = new Wallet { UserId = 2, Amount = 1000, BlockedAmount = 300 };
        var sellerWallet = new Wallet { UserId = 1, Amount = 200, BlockedAmount = 0 };

        _auctionRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Auction> { expiredAuction });
        _assetRepoMock.Setup(r => r.GetByIdAsync(100)).ReturnsAsync(asset);
        _walletRepoMock.Setup(r => r.GetByUserIdAsync(2)).ReturnsAsync(buyerWallet);
        _walletRepoMock.Setup(r => r.GetByUserIdAsync(1)).ReturnsAsync(sellerWallet);

        var result = await _service.CheckAuctionExpiriesAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(AuctionStatus.Expired, expiredAuction.Status);
        Assert.Equal(2, asset.UserId);
        Assert.Equal(AssetStatus.OpenToAuction, asset.Status);
        Assert.Equal(700, buyerWallet.Amount);
        Assert.Equal(0, buyerWallet.BlockedAmount);
        Assert.Equal(500, sellerWallet.Amount);
    }

    [Fact]
    public async Task CheckAuctionExpiriesAsync_WithNoBids_ReopensAsset()
    {
        var expiredAuction = new Auction
        {
            Id = 22,
            UserId = 1,
            AssetId = 101,
            ReservedPrice = 100,
            CurrentHighestBid = 0,
            CurrentHighestBidderId = 0,
            MinimumBidIncrement = 10,
            StartDate = DateTime.UtcNow.AddMinutes(-120),
            TotalMinutesToExpiry = 60,
            Status = AuctionStatus.Live
        };

        var asset = new Asset
        {
            Id = 101,
            UserId = 1,
            Status = AssetStatus.ClosedForAuction
        };

        _auctionRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Auction> { expiredAuction });
        _assetRepoMock.Setup(r => r.GetByIdAsync(101)).ReturnsAsync(asset);

        var result = await _service.CheckAuctionExpiriesAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(AuctionStatus.ExpiredWithoutBids, expiredAuction.Status);
        Assert.Equal(AssetStatus.OpenToAuction, asset.Status);
    }
}