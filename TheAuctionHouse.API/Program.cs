using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TheAuctionHouse.Data.EFCore.SQLite;
using TheAuctionHouse.Domain.DataContracts;
using TheAuctionHouse.Domain.ServiceContracts;
using TheAuctionHouse.Domain.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

static string GetRequiredConfiguration(IConfiguration configuration, string key)
{
    return configuration[key]
        ?? throw new InvalidOperationException($"Missing required configuration value: {key}");
}

var jwtKey = GetRequiredConfiguration(builder.Configuration, "Jwt:Key");
var jwtIssuer = GetRequiredConfiguration(builder.Configuration, "Jwt:Issuer");
var jwtAudience = GetRequiredConfiguration(builder.Configuration, "Jwt:Audience");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddDbContext<AuctionHouseDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("BusinessDb") ?? "Data Source=business.db"));

builder.Services.AddScoped<IAppUnitOfWork, SqliteAppUnitOfWork>();
builder.Services.AddScoped<IAuctionService, AuctionService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<IAssetService>(sp =>
    new AssetService(sp.GetRequiredService<IAppUnitOfWork>(), sp.GetRequiredService<IAuctionService>()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AuctionHouseDbContext>().Database.EnsureCreated();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// ── Asset endpoints ─────────────────────────────────────────────────────────

var assets = app.MapGroup("/api/assets").RequireAuthorization();

assets.MapGet("/{userId:int}", async (int userId, IAssetService svc) =>
{
    var r = await svc.GetAllAssetsByUserIdAsync(userId);
    return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error.Message);
});

assets.MapGet("/detail/{assetId:int}", async (int assetId, IAssetService svc) =>
{
    var r = await svc.GetAssetByIdAsync(assetId);
    return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error.Message);
});

assets.MapPost("/{userId:int}", async (int userId, AssetInformationUpdateRequest req, IAssetService svc) =>
{
    var r = await svc.CreateAssetAsync(req, userId);
    return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error.Message);
});

assets.MapPut("/", async (AssetInformationUpdateRequest req, IAssetService svc) =>
{
    var r = await svc.UpdateAssetAsync(req);
    return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error.Message);
});

assets.MapDelete("/{assetId:int}", async (int assetId, IAssetService svc) =>
{
    var r = await svc.DeleteAssetAsync(assetId);
    return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error.Message);
});

// ── Auction endpoints ────────────────────────────────────────────────────────

var auctions = app.MapGroup("/api/auctions").RequireAuthorization();

auctions.MapGet("/", async (IAuctionService svc) =>
{
    var r = await svc.GetAllOpenAuctionsByUserIdAsync();
    return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error.Message);
});

auctions.MapGet("/{auctionId:int}", async (int auctionId, IAuctionService svc) =>
{
    var r = await svc.GetAuctionByIdAsync(auctionId);
    return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error.Message);
});

auctions.MapGet("/user/{userId:int}", async (int userId, IAuctionService svc) =>
{
    var r = await svc.GetAuctionsByUserIdAsync(userId);
    return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error.Message);
});

auctions.MapPost("/", async (PostAuctionRequest req, IAuctionService svc) =>
{
    var r = await svc.PostAuctionAsync(req);
    return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error.Message);
});

auctions.MapPost("/bid", async (PlaceBidRequest req, IAuctionService svc) =>
{
    var r = await svc.PlaceBidAsync(req);
    return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error.Message);
});

auctions.MapPost("/check-expiries", async (IAuctionService svc) =>
{
    var r = await svc.CheckAuctionExpiriesAsync();
    return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error.Message);
});

// ── Wallet endpoints ─────────────────────────────────────────────────────────

var wallet = app.MapGroup("/api/wallet").RequireAuthorization();

wallet.MapGet("/{userId:int}", async (int userId, IWalletService svc) =>
{
    var r = await svc.GetWalletBalenceAsync(userId);
    return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error.Message);
});

wallet.MapPost("/deposit", async (WalletTransactionRequest req, IWalletService svc) =>
{
    var r = await svc.DepositAsync(req);
    return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error.Message);
});

wallet.MapPost("/withdraw", async (WalletTransactionRequest req, IWalletService svc) =>
{
    var r = await svc.WithDrawalAsync(req);
    return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error.Message);
});

app.Run();
