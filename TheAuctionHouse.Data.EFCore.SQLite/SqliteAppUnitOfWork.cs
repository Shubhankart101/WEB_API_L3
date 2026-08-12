using TheAuctionHouse.Domain.DataContracts;

namespace TheAuctionHouse.Data.EFCore.SQLite;

public class SqliteAppUnitOfWork : IAppUnitOfWork
{
    private readonly AuctionHouseDbContext _context;
    private bool _disposed;

    public SqliteAppUnitOfWork(AuctionHouseDbContext context)
    {
        _context = context;
        AssetRepository = new SqliteAssetRepository(context);
        AuctionRepository = new SqliteAuctionRepository(context);
        PortalUserRepository = new SqlitePortalUserRepository(context);
        WalletRepository = new SqliteWalletRepository(context);
    }

    public IAssetRepository AssetRepository { get; }
    public IAuctionRepository AuctionRepository { get; }
    public IPortalUserRepository PortalUserRepository { get; }
    public IWalletRepository WalletRepository { get; }

    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

    public void Dispose()
    {
        if (!_disposed)
        {
            _context.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
