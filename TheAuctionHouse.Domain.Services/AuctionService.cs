using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TheAuctionHouse.Common.ErrorHandling;
using TheAuctionHouse.Domain.Entities;
using TheAuctionHouse.Domain.ServiceContracts;
using TheAuctionHouse.Domain.DataContracts;

namespace TheAuctionHouse.Domain.Services;

public class AuctionService : IAuctionService
{
    private readonly IAppUnitOfWork _unitOfWork;

    public AuctionService(IAppUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<bool>> CheckAuctionExpiriesAsync()
    {
        var auctions = await _unitOfWork.AuctionRepository.GetAllAsync();
        var liveAuctions = auctions.Where(a => a.Status == AuctionStatus.Live).ToList();

        foreach (var auction in liveAuctions)
        {
            if (!auction.IsExpired())
            {
                continue;
            }

            var asset = await _unitOfWork.AssetRepository.GetByIdAsync(auction.AssetId);

            if (auction.CurrentHighestBid <= 0)
            {
                auction.Status = AuctionStatus.ExpiredWithoutBids;
                if (asset is not null)
                {
                    asset.Status = AssetStatus.OpenToAuction;
                    await _unitOfWork.AssetRepository.UpdateAsync(asset);
                }

                await _unitOfWork.AuctionRepository.UpdateAsync(auction);
                continue;
            }

            auction.Status = AuctionStatus.Expired;
            if (asset is not null)
            {
                asset.UserId = auction.CurrentHighestBidderId;
                asset.Status = AssetStatus.OpenToAuction;
                await _unitOfWork.AssetRepository.UpdateAsync(asset);
            }

            await SettleWalletsAsync(auction);
            await _unitOfWork.AuctionRepository.UpdateAsync(auction);
        }

        await _unitOfWork.SaveChangesAsync();
        return Result<bool>.Success(true);
    }

    public async Task<Result<List<AuctionResponse>>> GetAllOpenAuctionsByUserIdAsync()
    {
        var auctions = await _unitOfWork.AuctionRepository.GetAllAsync();
        var openAuctions = auctions
            .Where(a => a.Status == AuctionStatus.Live && !a.IsExpired())
            .OrderBy(a => a.GetRemainingTimeInMinutes())
            .ToList();

        var response = new List<AuctionResponse>();
        foreach (var auction in openAuctions)
        {
            response.Add(await MapAuctionResponseAsync(auction));
        }

        return Result<List<AuctionResponse>>.Success(response);
    }

    public async Task<Result<AuctionResponse>> GetAuctionByIdAsync(int auctionId)
    {
        var auction = await _unitOfWork.AuctionRepository.GetByIdAsync(auctionId);
        if (auction is null)
        {
            return Result<AuctionResponse>.Failure(Error.NotFound("Auction not found."));
        }

        return Result<AuctionResponse>.Success(await MapAuctionResponseAsync(auction));
    }

    public async Task<Result<List<AuctionResponse>>> GetAuctionsByUserIdAsync(int userId)
    {
        var auctions = await _unitOfWork.AuctionRepository.GetAuctionsByUserIdAsync(userId);
        var response = new List<AuctionResponse>();

        foreach (var auction in auctions.OrderByDescending(a => a.StartDate))
        {
            response.Add(await MapAuctionResponseAsync(auction));
        }

        return Result<List<AuctionResponse>>.Success(response);
    }

    public async Task<Result<bool>> PlaceBidAsync(PlaceBidRequest placeBidRequest)
    {
        if (placeBidRequest.BidAmount <= 0)
        {
            return Result<bool>.Failure(Error.BadRequest("Bid amount should be a positive integer."));
        }

        var auction = await _unitOfWork.AuctionRepository.GetByIdAsync(placeBidRequest.AuctionId);
        if (auction is null)
        {
            return Result<bool>.Failure(Error.NotFound("Auction not found."));
        }

        if (auction.Status != AuctionStatus.Live || auction.IsExpired())
        {
            return Result<bool>.Failure(Error.BadRequest("Auction is not live."));
        }

        if (auction.UserId == placeBidRequest.UserId)
        {
            return Result<bool>.Failure(Error.BadRequest("Auction owner cannot place a bid."));
        }

        var minimumAllowedBid = auction.CurrentHighestBid > 0
            ? auction.CurrentHighestBid + auction.MinimumBidIncrement
            : auction.ReservedPrice;

        if (placeBidRequest.BidAmount < minimumAllowedBid)
        {
            return Result<bool>.Failure(Error.BadRequest("Bid amount should meet current call for value."));
        }

        var bidderWallet = await _unitOfWork.WalletRepository.GetByUserIdAsync(placeBidRequest.UserId);
        if (bidderWallet is null)
        {
            return Result<bool>.Failure(Error.NotFound("Wallet not found for bidder."));
        }

        var availableBalance = bidderWallet.Amount - bidderWallet.BlockedAmount;
        if (availableBalance < placeBidRequest.BidAmount)
        {
            return Result<bool>.Failure(Error.BadRequest("Insufficient wallet balance for this bid."));
        }

        if (auction.CurrentHighestBid > 0 && auction.CurrentHighestBidderId > 0)
        {
            var previousBidderWallet = await _unitOfWork.WalletRepository.GetByUserIdAsync(auction.CurrentHighestBidderId);
            if (previousBidderWallet is not null)
            {
                previousBidderWallet.BlockedAmount = Math.Max(0, previousBidderWallet.BlockedAmount - auction.CurrentHighestBid);
                await _unitOfWork.WalletRepository.UpdateAsync(previousBidderWallet);
            }
        }

        bidderWallet.BlockedAmount += placeBidRequest.BidAmount;
        await _unitOfWork.WalletRepository.UpdateAsync(bidderWallet);

        auction.CurrentHighestBid = placeBidRequest.BidAmount;
        auction.CurrentHighestBidderId = placeBidRequest.UserId;

        await _unitOfWork.AuctionRepository.UpdateAsync(auction);
        await _unitOfWork.AuctionRepository.AddAsync(new BidHistory
        {
            AuctionId = auction.Id,
            BidderId = placeBidRequest.UserId,
            BidAmount = placeBidRequest.BidAmount,
            BidDate = DateTime.UtcNow,
            BidderName = (await _unitOfWork.PortalUserRepository.GetUserByUserIdAsync(placeBidRequest.UserId))?.Name ?? string.Empty
        });

        await _unitOfWork.SaveChangesAsync();
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> PostAuctionAsync(PostAuctionRequest postAuctionRequest)
    {
        if (postAuctionRequest.ReservedPrice <= 0 || postAuctionRequest.ReservedPrice > 9999)
        {
            return Result<bool>.Failure(Error.BadRequest("Reserved price should be between 1 and 9999."));
        }

        if (postAuctionRequest.MinimumBidIncrement <= 0 || postAuctionRequest.MinimumBidIncrement > 999)
        {
            return Result<bool>.Failure(Error.BadRequest("Minimum bid increment should be between 1 and 999."));
        }

        if (postAuctionRequest.TotalMinutesToExpiry <= 0 || postAuctionRequest.TotalMinutesToExpiry > 10080)
        {
            return Result<bool>.Failure(Error.BadRequest("Total minutes to expiry should be between 1 and 10080."));
        }

        var asset = await _unitOfWork.AssetRepository.GetByIdAsync(postAuctionRequest.AssetId);
        if (asset is null)
        {
            return Result<bool>.Failure(Error.NotFound("Asset not found."));
        }

        if (asset.UserId != postAuctionRequest.OwnerId)
        {
            return Result<bool>.Failure(Error.BadRequest("Only owner can post auction for an asset."));
        }

        if (asset.Status != AssetStatus.OpenToAuction)
        {
            return Result<bool>.Failure(Error.BadRequest("Only OpenToAuction assets can be posted for auction."));
        }

        var auction = new Auction
        {
            AssetId = postAuctionRequest.AssetId,
            UserId = postAuctionRequest.OwnerId,
            ReservedPrice = postAuctionRequest.ReservedPrice,
            CurrentHighestBid = postAuctionRequest.CurrentHighestBid,
            CurrentHighestBidderId = postAuctionRequest.CurrentHighestBidderId,
            MinimumBidIncrement = postAuctionRequest.MinimumBidIncrement,
            StartDate = DateTime.UtcNow,
            TotalMinutesToExpiry = postAuctionRequest.TotalMinutesToExpiry,
            Status = AuctionStatus.Live
        };

        asset.Status = AssetStatus.ClosedForAuction;

        await _unitOfWork.AuctionRepository.AddAsync(auction);
        await _unitOfWork.AssetRepository.UpdateAsync(asset);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true);
    }

    private async Task SettleWalletsAsync(Auction auction)
    {
        var buyerWallet = await _unitOfWork.WalletRepository.GetByUserIdAsync(auction.CurrentHighestBidderId);
        if (buyerWallet is not null)
        {
            buyerWallet.BlockedAmount = Math.Max(0, buyerWallet.BlockedAmount - auction.CurrentHighestBid);
            buyerWallet.Amount -= auction.CurrentHighestBid;
            await _unitOfWork.WalletRepository.UpdateAsync(buyerWallet);
        }

        var sellerWallet = await _unitOfWork.WalletRepository.GetByUserIdAsync(auction.UserId);
        if (sellerWallet is null)
        {
            sellerWallet = new Wallet
            {
                UserId = auction.UserId,
                Amount = auction.CurrentHighestBid,
                BlockedAmount = 0
            };
            await _unitOfWork.WalletRepository.AddAsync(sellerWallet);
        }
        else
        {
            sellerWallet.Amount += auction.CurrentHighestBid;
            await _unitOfWork.WalletRepository.UpdateAsync(sellerWallet);
        }
    }

    private async Task<AuctionResponse> MapAuctionResponseAsync(Auction auction)
    {
        var asset = await _unitOfWork.AssetRepository.GetByIdAsync(auction.AssetId);
        var highestBidder = auction.CurrentHighestBidderId > 0
            ? await _unitOfWork.PortalUserRepository.GetUserByUserIdAsync(auction.CurrentHighestBidderId)
            : null;

        var bidHistory = await _unitOfWork.AuctionRepository.GetBidHistoriesByAuctionIdAsync(auction.Id);
        return new AuctionResponse
        {
            AuctionId = auction.Id,
            UserId = auction.UserId,
            AssetId = auction.AssetId,
            AssetTitle = asset?.Title ?? string.Empty,
            AssetDescription = asset?.Description ?? string.Empty,
            CurrentHighestBid = auction.CurrentHighestBid,
            CurrentHighestBidderId = auction.CurrentHighestBidderId,
            HighestBidderName = highestBidder?.Name ?? string.Empty,
            MinimumBidIncrement = auction.MinimumBidIncrement,
            ReservedPrice = auction.ReservedPrice,
            StartDate = auction.StartDate,
            TotalMinutesToExpiry = auction.TotalMinutesToExpiry,
            Status = auction.Status.ToString(),
            CustomStatusMessage = auction.IsExpired() ? "Auction expired" : null,
            BidHistory = bidHistory.Select(history => new BidHistoryResponse
            {
                BidId = history.Id,
                AuctionId = history.AuctionId,
                UserId = history.BidderId,
                BidAmount = history.BidAmount,
                BidTime = history.BidDate,
                UserName = history.BidderName
            }).OrderByDescending(h => h.BidTime).ToList()
        };
    }
}