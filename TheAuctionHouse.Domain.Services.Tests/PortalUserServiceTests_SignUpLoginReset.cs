using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using TheAuctionHouse.Common.ErrorHandling;
using TheAuctionHouse.Data.EFCore.InMemory;
using TheAuctionHouse.Domain.Entities;

namespace TheAuctionHouse.Domain.Services.Tests;

public class PortalUserServiceTests_SignUpLoginReset
{
    private IAppUnitOfWork GetUoW()
    {
        var options = new DbContextOptionsBuilder<InMemoryAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new InMemoryAppUnitOfWork(new InMemoryAppDbContext(options));
    }

    private PortalUserService BuildService(IAppUnitOfWork uow) =>
        new(uow, new Mock<IEmailService>().Object, "AuctionHouseJwtSecretKey!@#$%2025XYZ");

    // ── SignUp ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SignUpAsync_ValidRequest_ReturnsSuccess()
    {
        var uow = GetUoW();
        var svc = BuildService(uow);

        var result = await svc.SignUpAsync(new SignUpRequest
            { Name = "Alice", EmailId = "alice@example.com", Password = "pass123" });

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SignUpAsync_DuplicateEmail_ReturnsError()
    {
        var uow = GetUoW();
        var svc = BuildService(uow);
        await svc.SignUpAsync(new SignUpRequest { Name = "Alice", EmailId = "dup@example.com", Password = "pass" });

        var result = await svc.SignUpAsync(new SignUpRequest { Name = "Bob", EmailId = "dup@example.com", Password = "other" });

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.Error.ErrorCode);
    }

    [Fact]
    public async Task SignUpAsync_MissingName_ReturnsError()
    {
        var uow = GetUoW();
        var svc = BuildService(uow);

        var result = await svc.SignUpAsync(new SignUpRequest { Name = "", EmailId = "a@b.com", Password = "pass" });

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.Error.ErrorCode);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsJwtToken()
    {
        var uow = GetUoW();
        var svc = BuildService(uow);
        await svc.SignUpAsync(new SignUpRequest { Name = "Bob", EmailId = "bob@example.com", Password = "secret" });

        var result = await svc.LoginAsync(new LoginRequest { EmailId = "bob@example.com", Password = "secret" });

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value!);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsError()
    {
        var uow = GetUoW();
        var svc = BuildService(uow);
        await svc.SignUpAsync(new SignUpRequest { Name = "Carol", EmailId = "carol@example.com", Password = "correct" });

        var result = await svc.LoginAsync(new LoginRequest { EmailId = "carol@example.com", Password = "wrong" });

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.Error.ErrorCode);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ReturnsNotFound()
    {
        var uow = GetUoW();
        var svc = BuildService(uow);

        var result = await svc.LoginAsync(new LoginRequest { EmailId = "nobody@example.com", Password = "pass" });

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.Error.ErrorCode);
    }

    // ── ResetPassword ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ResetPasswordAsync_ValidRequest_ReturnsSuccess()
    {
        var uow = GetUoW();
        var svc = BuildService(uow);
        await svc.SignUpAsync(new SignUpRequest { Name = "Dave", EmailId = "dave@example.com", Password = "oldPass1" });
        var user = await uow.PortalUserRepository.GetUserByEmailAsync("dave@example.com");

        var result = await svc.ResetPasswordAsync(new ResetPasswordRequest
        {
            UserId = user!.Id,
            OldPassword = "oldPass1",
            NewPassword = "newPass2",
            ConfirmPassword = "newPass2"
        });

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ResetPasswordAsync_WrongOldPassword_ReturnsError()
    {
        var uow = GetUoW();
        var svc = BuildService(uow);
        await svc.SignUpAsync(new SignUpRequest { Name = "Eve", EmailId = "eve@example.com", Password = "correct" });
        var user = await uow.PortalUserRepository.GetUserByEmailAsync("eve@example.com");

        var result = await svc.ResetPasswordAsync(new ResetPasswordRequest
        {
            UserId = user!.Id,
            OldPassword = "wrong",
            NewPassword = "new",
            ConfirmPassword = "new"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.Error.ErrorCode);
    }

    [Fact]
    public async Task ResetPasswordAsync_PasswordMismatch_ReturnsError()
    {
        var uow = GetUoW();
        var svc = BuildService(uow);
        await svc.SignUpAsync(new SignUpRequest { Name = "Frank", EmailId = "frank@example.com", Password = "pass" });
        var user = await uow.PortalUserRepository.GetUserByEmailAsync("frank@example.com");

        var result = await svc.ResetPasswordAsync(new ResetPasswordRequest
        {
            UserId = user!.Id,
            OldPassword = "pass",
            NewPassword = "newA",
            ConfirmPassword = "newB"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.Error.ErrorCode);
    }

    [Fact]
    public async Task GetUserProfileAsync_ReturnsCorrectProfile()
    {
        var uow = GetUoW();
        var svc = BuildService(uow);
        await svc.SignUpAsync(new SignUpRequest { Name = "Grace", EmailId = "grace@example.com", Password = "p" });
        var user = await uow.PortalUserRepository.GetUserByEmailAsync("grace@example.com");

        var result = await svc.GetUserProfileAsync(user!.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("Grace", result.Value!.Name);
        Assert.Equal("grace@example.com", result.Value.EmailId);
    }
}
