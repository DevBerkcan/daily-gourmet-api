using DailyGourmet.Api.Models.Entities;

namespace DailyGourmet.Api.Repositories.Interfaces;

public interface IUserRepository : IRepository<User>
{
    /// <summary>Bypasses the tenant query filter — needed for login, where no tenant context
    /// exists yet (the caller hasn't authenticated, so we don't know their tenant until we find
    /// them by email).</summary>
    Task<User?> GetByEmailIgnoringTenantAsync(string email, CancellationToken ct = default);

    Task<User?> GetByIdIgnoringTenantAsync(Guid id, CancellationToken ct = default);
}
