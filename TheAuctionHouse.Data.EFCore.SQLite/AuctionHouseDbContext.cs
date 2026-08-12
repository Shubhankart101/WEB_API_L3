using Microsoft.EntityFrameworkCore;
using TheAuctionHouse.Domain.Entities;

namespace TheAuctionHouse.Data.EFCore.SQLite;

public class AuctionHouseDbContext : DbContext
{
    public AuctionHouseDbContext(DbContextOptions<AuctionHouseDbContext> options) : base(options) { }

    public DbSet<PortalUser> PortalUsers { get; set; }
    public DbSet<Asset> Assets { get; set; }
    public DbSet<Auction> Auctions { get; set; }
    public DbSet<BidHistory> BidHistories { get; set; }
    public DbSet<Wallet> Wallets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Wallet>().HasKey(w => w.UserId);
    }
}
