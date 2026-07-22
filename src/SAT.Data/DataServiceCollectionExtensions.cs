using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SAT.Data.Repositories;

namespace SAT.Data;

/// <summary>Đăng ký DbContext và repository vào DI container.</summary>
public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddSatData(this IServiceCollection services, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "Chuỗi kết nối rỗng. Kiểm tra ConnectionStrings:SatDatabase trong appsettings.json.",
                nameof(connectionString));
        }

        // Scoped chứ KHÔNG phải Singleton: DbContext không an toàn khi dùng đồng
        // thời từ nhiều luồng. Mỗi lần điều hướng sang màn hình mới, app mở một
        // scope riêng nên mỗi ViewModel có DbContext của chính nó.
        services.AddDbContext<SatDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                // Tự thử lại khi mất kết nối chớp nhoáng - hay gặp lúc SQL Server
                // vừa khởi động cùng máy.
                sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(3), errorNumbersToAdd: null);
            });
        }, ServiceLifetime.Scoped);

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        return services;
    }
}
