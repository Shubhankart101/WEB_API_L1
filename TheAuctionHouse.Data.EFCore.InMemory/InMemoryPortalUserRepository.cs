using Microsoft.EntityFrameworkCore;
using SKUApp.Data.EFCore.InMemory;
using TheAuctionHouse.Domain.DataContracts;
using TheAuctionHouse.Domain.Entities;

public class InMemoryPortalUserRespository : GenericRepository<PortalUser>, IPortalUserRepository
{
    public InMemoryPortalUserRespository(IAppDbContext context) : base(context)
    {
    }

    public void DepositWalletBalance(int userId, int amount)
    {
        var user = this._context.PortalUsers.FirstOrDefault(x => x.Id == userId);
        if (user is null)
        {
            return;
        }

        user.WalletBalence += amount;
        this._context.SaveChanges();
    }

    public async Task<PortalUser?> GetUserByEmailAsync(string email)
    {
        return await this._context.PortalUsers.FirstOrDefaultAsync(x => x.EmailId == email);
    }

    public Task<PortalUser?> GetUserByUserIdAsync(int userId)
    {
        // Use the context to find the user by Id
        return this._context.PortalUsers.FirstOrDefaultAsync(x => x.Id == userId);
    }
    public void WithdrawWalletBalance(int userId, int amount)
    {
        var user = this._context.PortalUsers.FirstOrDefault(x => x.Id == userId);
        if (user is null)
        {
            return;
        }

        user.WalletBalence -= amount;
        this._context.SaveChanges();
    }
}