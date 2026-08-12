using Microsoft.EntityFrameworkCore;
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
var emailApiBase = GetRequiredConfiguration(builder.Configuration, "EmailApi:BaseUrl");
var emailApiKey = GetRequiredConfiguration(builder.Configuration, "EmailApi:ApiKey");

builder.Services.AddDbContext<AuctionHouseDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("AuthDb") ?? "Data Source=auth.db"));

builder.Services.AddScoped<IAppUnitOfWork, SqliteAppUnitOfWork>();

builder.Services.AddHttpClient("EmailApi", client =>
    client.BaseAddress = new Uri(emailApiBase));

builder.Services.AddScoped<IEmailService>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new HttpEmailService(factory.CreateClient("EmailApi"), emailApiKey);
});

builder.Services.AddScoped<IPortalUserService>(sp =>
    new PortalUserService(
        sp.GetRequiredService<IAppUnitOfWork>(),
        sp.GetRequiredService<IEmailService>(),
        jwtKey,
        jwtIssuer,
        jwtAudience));

var app = builder.Build();

// Ensure DB is created
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AuctionHouseDbContext>().Database.EnsureCreated();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ── Auth endpoints ──────────────────────────────────────────────────────────

app.MapPost("/api/auth/signup", async (SignUpRequest req, IPortalUserService svc) =>
{
    var result = await svc.SignUpAsync(req);
    return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error.Message);
});

app.MapPost("/api/auth/login", async (LoginRequest req, IPortalUserService svc) =>
{
    var result = await svc.LoginAsync(req);
    return result.IsSuccess
        ? Results.Ok(new { token = result.Value })
        : Results.BadRequest(result.Error.Message);
});

app.MapPost("/api/auth/logout/{userId:int}", async (int userId, IPortalUserService svc) =>
{
    var result = await svc.LogoutAsync(userId);
    return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error.Message);
});

app.MapPost("/api/auth/forgot-password", async (ForgotPasswordRequest req, IPortalUserService svc) =>
{
    var result = await svc.ForgotPasswordAsync(req);
    return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error.Message);
});

app.MapPost("/api/auth/reset-password", async (ResetPasswordRequest req, IPortalUserService svc) =>
{
    var result = await svc.ResetPasswordAsync(req);
    return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error.Message);
});

app.MapGet("/api/auth/profile/{userId:int}", async (int userId, IPortalUserService svc) =>
{
    var result = await svc.GetUserProfileAsync(userId);
    return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error.Message);
});

app.Run();
