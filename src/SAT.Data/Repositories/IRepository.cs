namespace SAT.Data.Repositories;

/// <summary>
/// Repository dùng chung cho các thao tác CRUD lặp đi lặp lại.
///
/// Mục đích là để ViewModel và Service không phải phụ thuộc trực tiếp vào EF.
/// Truy vấn phức tạp (bảng điểm, tiến độ tốt nghiệp) thì viết repository
/// chuyên biệt hoặc dùng <see cref="Query"/>, đừng nhồi vào đây.
///
/// KHÔNG có UnitOfWork riêng: DbContext đã đóng vai trò đó rồi. Gọi
/// <see cref="SaveChangesAsync"/> một lần sau khi thay đổi xong (docs/plan §4).
/// </summary>
public interface IRepository<T> where T : class
{
    /// <summary>Lấy theo khóa chính. Trả về null nếu không có.</summary>
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Lấy toàn bộ bản ghi (không theo dõi thay đổi).</summary>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Điểm mở rộng cho truy vấn phức tạp (Where/Include/GroupBy).
    ///
    /// Mặc định KHÔNG theo dõi thay đổi vì phần lớn màn hình chỉ đọc; bật
    /// tracking bằng <paramref name="asTracking"/> khi cần sửa rồi lưu lại.
    /// </summary>
    IQueryable<T> Query(bool asTracking = false);

    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    void Update(T entity);
    void Remove(T entity);
    void RemoveRange(IEnumerable<T> entities);

    /// <summary>Ghi mọi thay đổi đang chờ xuống DB. Trả về số dòng bị ảnh hưởng.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
