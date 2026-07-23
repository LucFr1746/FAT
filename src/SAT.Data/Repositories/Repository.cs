using Microsoft.EntityFrameworkCore;

namespace SAT.Data.Repositories;

/// <summary>Cài đặt <see cref="IRepository{T}"/> trên EF Core.</summary>
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly SatDbContext Context;
    protected readonly DbSet<T> DbSet;

    public Repository(SatDbContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        DbSet = context.Set<T>();
    }

    /// <summary>
    /// Dùng FindAsync chứ không phải so sánh tay với "Id": mỗi bảng có tên khóa
    /// chính khác nhau (RoleId, CourseId...) và FindAsync tự tra khóa từ model.
    /// </summary>
    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await DbSet.FindAsync([id], cancellationToken);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking().ToListAsync(cancellationToken);

    public virtual IQueryable<T> Query(bool asTracking = false)
        => asTracking ? DbSet.AsQueryable() : DbSet.AsNoTracking();

    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default)
        => await DbSet.AddAsync(entity, cancellationToken);

    public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        => await DbSet.AddRangeAsync(entities, cancellationToken);

    public virtual void Update(T entity) => DbSet.Update(entity);

    public virtual void Remove(T entity) => DbSet.Remove(entity);

    public virtual void RemoveRange(IEnumerable<T> entities) => DbSet.RemoveRange(entities);

    public virtual Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => Context.SaveChangesAsync(cancellationToken);
}
