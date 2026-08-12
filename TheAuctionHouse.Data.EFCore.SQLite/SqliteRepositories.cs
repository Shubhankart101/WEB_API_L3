using Microsoft.EntityFrameworkCore;
using TheAuctionHouse.Domain.DataContracts;
using TheAuctionHouse.Domain.Entities;

namespace TheAuctionHouse.Data.EFCore.SQLite;

public class SqliteGenericRepository<T> : IRepository<T> where T : class
{
    protected readonly AuctionHouseDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public SqliteGenericRepository(AuctionHouseDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<IEnumerable<T>> GetAllAsync() => await _dbSet.ToListAsync();

    public async Task<T?> GetByIdAsync(int id) => await _dbSet.FindAsync(id);

    public async Task<IEnumerable<T>> FindAsync(Func<T, bool> predicate) =>
        await Task.FromResult(_dbSet.Where(predicate).ToList());

    public async Task AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync();
    }
}

public class SqliteAssetRepository : SqliteGenericRepository<Asset>, IAssetRepository
{
    public SqliteAssetRepository(AuctionHouseDbContext context) : base(context) { }

    public async Task<List<Asset>> GetAssetsByUserIdAsync(int userId) =>
        await _context.Assets.Where(a => a.UserId == userId).ToListAsync();
}

public class SqliteAuctionRepository : SqliteGenericRepository<Auction>, IAuctionRepository
{
    public SqliteAuctionRepository(AuctionHouseDbContext context) : base(context) { }

    public async Task<List<Auction>> GetAuctionsByUserIdAsync(int userId) =>
        await _context.Auctions.Where(a => a.UserId == userId).ToListAsync();

    public async Task<List<BidHistory>> GetBidHistoriesByAuctionIdAsync(int auctionId) =>
        await _context.BidHistories.Where(b => b.AuctionId == auctionId).ToListAsync();

    public async Task<List<BidHistory>> GetBidHistoriesByUserIdAsync(int userId) =>
        await _context.BidHistories.Where(b => b.BidderId == userId).ToListAsync();

    public async Task AddAsync(BidHistory bidHistory)
    {
        await _context.BidHistories.AddAsync(bidHistory);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAuctionsByAssetIdAsync(int assetId)
    {
        var auctions = await _context.Auctions.Where(a => a.AssetId == assetId).ToListAsync();
        if (auctions.Any())
        {
            _context.Auctions.RemoveRange(auctions);
            await _context.SaveChangesAsync();
        }
    }
}

public class SqlitePortalUserRepository : SqliteGenericRepository<PortalUser>, IPortalUserRepository
{
    public SqlitePortalUserRepository(AuctionHouseDbContext context) : base(context) { }

    public async Task<PortalUser?> GetUserByUserIdAsync(int userId) =>
        await _context.PortalUsers.FirstOrDefaultAsync(u => u.Id == userId);

    public async Task<PortalUser?> GetUserByEmailAsync(string email) =>
        await _context.PortalUsers.FirstOrDefaultAsync(u => u.EmailId == email);

    public void DepositWalletBalance(int userId, int amount) { }
    public void WithdrawWalletBalance(int userId, int amount) { }
}

public class SqliteWalletRepository : IWalletRepository
{
    private readonly AuctionHouseDbContext _context;
    public SqliteWalletRepository(AuctionHouseDbContext context) => _context = context;

    public async Task<Wallet?> GetByUserIdAsync(int userId) =>
        await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);

    public async Task AddAsync(Wallet wallet)
    {
        await _context.Wallets.AddAsync(wallet);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Wallet wallet)
    {
        _context.Wallets.Update(wallet);
        await _context.SaveChangesAsync();
    }
}
