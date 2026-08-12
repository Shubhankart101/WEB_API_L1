using TheAuctionHouse.Common.ErrorHandling;
using TheAuctionHouse.Domain.Entities;
using TheAuctionHouse.Domain.ServiceContracts;
using TheAuctionHouse.Domain.DataContracts;
using System.Linq;

namespace TheAuctionHouse.Domain.Services;

public class WalletService : IWalletService
{
    private readonly IAppUnitOfWork _unitOfWork;

    public WalletService(IAppUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<bool>> BlockAmountAsync(WalletTransactionRequest walletTransactionRequest)
    {
        var amountValidation = ValidateAmount(walletTransactionRequest.Amount);
        if (!amountValidation.IsSuccess)
        {
            return amountValidation;
        }

        var wallet = await _unitOfWork.WalletRepository.GetByUserIdAsync(walletTransactionRequest.UserId);
        if (wallet is null)
        {
            return Result<bool>.Failure(Error.NotFound("Wallet not found."));
        }

        var availableBalance = wallet.Amount - wallet.BlockedAmount;
        if (availableBalance < walletTransactionRequest.Amount)
        {
            return Result<bool>.Failure(Error.BadRequest("Insufficient available balance to block amount."));
        }

        wallet.BlockedAmount += walletTransactionRequest.Amount;
        await _unitOfWork.WalletRepository.UpdateAsync(wallet);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> DepositAsync(WalletTransactionRequest walletTransactionRequest)
    {
        var amountValidation = ValidateAmount(walletTransactionRequest.Amount);
        if (!amountValidation.IsSuccess)
        {
            return amountValidation;
        }

        var wallet = await _unitOfWork.WalletRepository.GetByUserIdAsync(walletTransactionRequest.UserId);
        if (wallet is null)
        {
            wallet = new Wallet
            {
                UserId = walletTransactionRequest.UserId,
                Amount = walletTransactionRequest.Amount,
                BlockedAmount = 0
            };

            await _unitOfWork.WalletRepository.AddAsync(wallet);
        }
        else
        {
            wallet.Amount += walletTransactionRequest.Amount;
            await _unitOfWork.WalletRepository.UpdateAsync(wallet);
        }

        await _unitOfWork.SaveChangesAsync();
        return Result<bool>.Success(true);
    }

    public async Task<Result<WalletBalenceResponse>> GetWalletBalenceAsync(int userId)
    {
        var wallet = await _unitOfWork.WalletRepository.GetByUserIdAsync(userId);
        if (wallet is null)
        {
            return Result<WalletBalenceResponse>.Failure(Error.NotFound("Wallet not found."));
        }

        var bidHistories = await _unitOfWork.AuctionRepository.GetBidHistoriesByUserIdAsync(userId);

        return Result<WalletBalenceResponse>.Success(new WalletBalenceResponse
        {
            UserId = wallet.UserId,
            Amount = wallet.Amount,
            BlockedAmount = wallet.BlockedAmount,
            BidHistory = bidHistories
                .OrderByDescending(b => b.BidDate)
                .Select(history => new BidHistoryResponse
                {
                    BidId = history.Id,
                    AuctionId = history.AuctionId,
                    UserId = history.BidderId,
                    BidAmount = history.BidAmount,
                    BidTime = history.BidDate,
                    UserName = history.BidderName
                })
                .ToList()
        });
    }

    public async Task<Result<bool>> WithDrawalAsync(WalletTransactionRequest walletTransactionRequest)
    {
        var amountValidation = ValidateAmount(walletTransactionRequest.Amount);
        if (!amountValidation.IsSuccess)
        {
            return amountValidation;
        }

        var wallet = await _unitOfWork.WalletRepository.GetByUserIdAsync(walletTransactionRequest.UserId);
        if (wallet is null)
        {
            return Result<bool>.Failure(Error.NotFound("Wallet not found."));
        }

        var availableBalance = wallet.Amount - wallet.BlockedAmount;
        if (availableBalance < walletTransactionRequest.Amount)
        {
            return Result<bool>.Failure(Error.BadRequest("Insufficient wallet balance."));
        }

        wallet.Amount -= walletTransactionRequest.Amount;
        await _unitOfWork.WalletRepository.UpdateAsync(wallet);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true);
    }

    private static Result<bool> ValidateAmount(int amount)
    {
        if (amount <= 0 || amount > 999999)
        {
            return Result<bool>.Failure(Error.BadRequest("Amount should be between 1 and 999999."));
        }

        return Result<bool>.Success(true);
    }
}