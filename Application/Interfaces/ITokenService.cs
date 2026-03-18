using System.Security.Claims;
using AuthService.Domain.Entities;
using AuthService.Shared.Models;

namespace AuthService.Application.Interfaces;

public interface ITokenService
{
    Task<AccessTokenResult> GenerateAccessTokenAsync(User user, CancellationToken cancellationToken = default);
    Task<RefreshTokenResult> GenerateRefreshTokenAsync(CancellationToken cancellationToken = default);
    Task<ClaimsPrincipal> ValidateJwtAsync(string token, CancellationToken cancellationToken = default);
    string HashOpaqueToken(string token);
}
