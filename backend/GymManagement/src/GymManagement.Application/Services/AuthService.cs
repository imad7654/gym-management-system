using GymManagement.Application.DTOs.Auth;
using GymManagement.Application.Interfaces;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Application.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<LoginResponse?> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<UserInfo?> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
}

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;

    public AuthService(IUnitOfWork unitOfWork, ITokenService tokenService, IPasswordHasher passwordHasher)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return null;
        }

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var refreshToken = _tokenService.GenerateRefreshToken();

        refreshToken.UserId = user.Id;
        await _unitOfWork.RefreshTokens.AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            AccessTokenExpiration = DateTime.UtcNow.AddMinutes(15),
            User = new UserInfo
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                Roles = roles
            }
        };
    }

    public async Task<LoginResponse?> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var token = await _unitOfWork.RefreshTokens.Query()
            .Include(rt => rt.User)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken, cancellationToken);

        if (token == null || !token.IsActive)
        {
            return null;
        }

        // Revoke old token
        token.RevokedAt = DateTime.UtcNow;

        // Generate new tokens
        var user = token.User;
        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var newAccessToken = _tokenService.GenerateAccessToken(user, roles);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        token.ReplacedByToken = newRefreshToken.Token;
        newRefreshToken.UserId = user.Id;

        await _unitOfWork.RefreshTokens.AddAsync(newRefreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken.Token,
            AccessTokenExpiration = DateTime.UtcNow.AddMinutes(15),
            User = new UserInfo
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                Roles = roles
            }
        };
    }

    public async Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var token = await _unitOfWork.RefreshTokens.Query()
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken, cancellationToken);

        if (token == null)
        {
            return false;
        }

        token.RevokedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<UserInfo?> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            return null;
        }

        return new UserInfo
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName,
            Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList()
        };
    }

    public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return false;
        }

        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            return false;
        }

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);

        // Every session is signed out, this one included, so the person signs in again with
        // the password they just chose.
        //
        // Without this a changed password locked nobody out: an existing refresh token kept
        // minting access tokens until it expired, so whoever had the old password - the
        // usual reason for changing it - would have kept their access anyway.
        //
        // Keeping the current session alive would need the browser to send its refresh
        // token up so it could be spared. One extra sign-in after a deliberate action is a
        // smaller cost than that plumbing, and it fails safe.
        var now = DateTime.UtcNow;

        var sessions = await _unitOfWork.RefreshTokens.Query()
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var token in sessions)
        {
            token.RevokedAt = now;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
