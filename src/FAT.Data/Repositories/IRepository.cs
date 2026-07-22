namespace FAT.Data.Repositories;

/// <summary>
/// A shared repository for the CRUD operations that repeat across entities.
///
/// The point is to keep view models and services from depending on EF Core
/// directly. Complex queries (transcript, graduation progress) belong in a
/// dedicated repository or go through <see cref="Query"/> - do not pile them
/// in here.
///
/// There is no separate UnitOfWork: DbContext already plays that role. Call
/// <see cref="SaveChangesAsync"/> once after a batch of changes.
/// </summary>
public interface IRepository<T> where T : class
{
    /// <summary>Fetch by primary key. Returns null when not found.</summary>
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Fetch every row, without change tracking.</summary>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Escape hatch for complex queries (Where/Include/GroupBy).
    ///
    /// Change tracking is OFF by default because most screens only read; pass
    /// <paramref name="asTracking"/> when the entity will be modified and saved.
    /// </summary>
    IQueryable<T> Query(bool asTracking = false);

    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    void Update(T entity);
    void Remove(T entity);
    void RemoveRange(IEnumerable<T> entities);

    /// <summary>Flush pending changes to the database; returns rows affected.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
