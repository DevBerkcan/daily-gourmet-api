namespace DailyGourmet.Api.Repositories.Interfaces;

/// <summary>Generic data-access contract for Guid-keyed aggregate roots. `Query()` exposes an
/// `IQueryable` (already tenant-filtered by the DbContext) so handlers can compose
/// includes/filters/sorts without controllers or handlers touching the DbContext directly.</summary>
public interface IRepository<TEntity> where TEntity : class
{
    IQueryable<TEntity> Query();
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
