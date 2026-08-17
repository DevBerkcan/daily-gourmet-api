using DailyGourmet.Api.Data;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Repositories.Implementations;

public class Repository<TEntity>(DailyGourmetDbContext db) : IRepository<TEntity>
    where TEntity : BaseEntity
{
    protected readonly DailyGourmetDbContext Db = db;
    protected readonly DbSet<TEntity> Set = db.Set<TEntity>();

    public IQueryable<TEntity> Query() => Set.AsQueryable();

    public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        await Set.AddAsync(entity, ct);
        return entity;
    }

    public void Update(TEntity entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        Set.Update(entity);
    }

    public void Remove(TEntity entity) => Set.Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Db.SaveChangesAsync(ct);
}
