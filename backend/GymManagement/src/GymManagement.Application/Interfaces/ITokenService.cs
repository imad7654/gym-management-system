using GymManagement.Domain.Entities;

namespace GymManagement.Application.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user, IEnumerable<string> roles);
    RefreshToken GenerateRefreshToken();
    int? ValidateAccessToken(string token);
}
