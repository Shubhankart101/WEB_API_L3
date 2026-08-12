using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using TheAuctionHouse.Common.ErrorHandling;
using TheAuctionHouse.Common.Validation;
using TheAuctionHouse.Domain.DataContracts;
using TheAuctionHouse.Domain.Entities;
using TheAuctionHouse.Domain.ServiceContracts;

namespace TheAuctionHouse.Domain.Services;

public class PortalUserService : IPortalUserService
{
    private IAppUnitOfWork _appUnitOfWork;
    private IEmailService _emailService;
    private readonly string _jwtKey;

    public PortalUserService(IAppUnitOfWork appUnitOfWork, IEmailService emailService, string jwtKey)
    {
        _appUnitOfWork = appUnitOfWork;
        _emailService = emailService;
        _jwtKey = jwtKey;
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    public async Task<Result<bool>> SignUpAsync(SignUpRequest signUpRequest)
    {
        if (string.IsNullOrWhiteSpace(signUpRequest.Name))
            return Result<bool>.Failure(Error.BadRequest("Name is required."));

        if (string.IsNullOrWhiteSpace(signUpRequest.EmailId))
            return Result<bool>.Failure(Error.BadRequest("Email is required."));

        if (string.IsNullOrWhiteSpace(signUpRequest.Password))
            return Result<bool>.Failure(Error.BadRequest("Password is required."));

        var existing = await _appUnitOfWork.PortalUserRepository.GetUserByEmailAsync(signUpRequest.EmailId);
        if (existing != null)
            return Result<bool>.Failure(Error.BadRequest("Email is already registered."));

        var user = new PortalUser
        {
            Name = signUpRequest.Name,
            EmailId = signUpRequest.EmailId,
            HashedPassword = HashPassword(signUpRequest.Password)
        };

        await _appUnitOfWork.PortalUserRepository.AddAsync(user);
        await _appUnitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true);
    }

    public async Task<Result<string>> LoginAsync(LoginRequest loginRequest)
    {
        if (string.IsNullOrWhiteSpace(loginRequest.EmailId) || string.IsNullOrWhiteSpace(loginRequest.Password))
            return Result<string>.Failure(Error.BadRequest("Email and password are required."));

        var user = await _appUnitOfWork.PortalUserRepository.GetUserByEmailAsync(loginRequest.EmailId);
        if (user == null)
            return Result<string>.Failure(Error.NotFound("User not found."));

        if (user.HashedPassword != HashPassword(loginRequest.Password))
            return Result<string>.Failure(Error.BadRequest("Invalid credentials."));

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtKey);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.EmailId)
            }),
            Expires = DateTime.UtcNow.AddHours(8),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return Result<string>.Success(tokenHandler.WriteToken(token));
    }

    public Task<Result<bool>> LogoutAsync(int UserId)
    {
        return Task.FromResult(Result<bool>.Success(true));
    }

    public async Task<Result<bool>> ForgotPasswordAsync(ForgotPasswordRequest forgotPasswordRequest)
    {
        if (!ValidationHelper.Validate(forgotPasswordRequest, out var validationResults))
        {
            var error = Error.ValidationFailures();
            error.ValidationResults.AddRange(validationResults);
            return Result<bool>.Failure(error);
        }

        var user = await _appUnitOfWork.PortalUserRepository.GetUserByEmailAsync(forgotPasswordRequest.EmailId);
        if (user == null)
            return Result<bool>.Failure(Error.NotFound("Email address is not registered."));

        await _emailService.SendEmailAsync(user.EmailId, "Password Reset | The Auction House", "", true);

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ResetPasswordAsync(ResetPasswordRequest resetPasswordRequest)
    {
        var user = await _appUnitOfWork.PortalUserRepository.GetUserByUserIdAsync(resetPasswordRequest.UserId);
        if (user == null)
            return Result<bool>.Failure(Error.NotFound("User not found."));

        if (user.HashedPassword != HashPassword(resetPasswordRequest.OldPassword))
            return Result<bool>.Failure(Error.BadRequest("Current password is incorrect."));

        if (resetPasswordRequest.NewPassword != resetPasswordRequest.ConfirmPassword)
            return Result<bool>.Failure(Error.BadRequest("New password and confirm password do not match."));

        user.HashedPassword = HashPassword(resetPasswordRequest.NewPassword);
        await _appUnitOfWork.PortalUserRepository.UpdateAsync(user);
        await _appUnitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true);
    }

    public async Task<Result<PortalUserResponse>> GetUserProfileAsync(int userId)
    {
        var user = await _appUnitOfWork.PortalUserRepository.GetUserByUserIdAsync(userId);
        if (user == null)
            return Result<PortalUserResponse>.Failure(Error.NotFound("User not found."));

        return Result<PortalUserResponse>.Success(new PortalUserResponse
        {
            UserId = user.Id,
            Name = user.Name,
            EmailId = user.EmailId
        });
    }
}
