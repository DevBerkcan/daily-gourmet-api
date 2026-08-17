using DailyGourmet.Api.Data;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Repositories.Implementations;

public class UserRepository(DailyGourmetDbContext db) : Repository<User>(db), IUserRepository
{
    public Task<User?> GetByEmailIgnoringTenantAsync(string email, CancellationToken ct = default) =>
        Set.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByIdIgnoringTenantAsync(Guid id, CancellationToken ct = default) =>
        Set.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id, ct);
}
