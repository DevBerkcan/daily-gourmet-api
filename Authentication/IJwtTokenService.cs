using DailyGourmet.Api.Models.Entities;

namespace DailyGourmet.Api.Authentication;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}
