using Microsoft.Extensions.DependencyInjection;
using SAT.Services.Abstractions;
using SAT.Services.Implementations;

namespace SAT.Services;

/// <summary>
/// Đăng ký các service NỀN TẢNG (dùng chung cho mọi module).
///
/// Service riêng của từng module do chính thành viên đó đăng ký trong file
/// SAT.App/Startup/&lt;Module&gt;Registration.cs của mình. Tách như vậy để 5 người
/// không cùng sửa một file và không đụng nhau khi merge (docs/plan §4).
/// </summary>
public static class ServicesServiceCollectionExtensions
{
    public static IServiceCollection AddSatCoreServices(this IServiceCollection services)
    {
        // Singleton: cả app chỉ có một phiên đăng nhập tại một thời điểm.
        services.AddSingleton<ICurrentUserContext, CurrentUserContext>();

        // Scoped: phụ thuộc SatDbContext (cũng Scoped). Đăng ký Singleton ở đây
        // sẽ giữ một DbContext sống suốt đời ứng dụng - vừa rò rỉ bộ nhớ vừa
        // trả về dữ liệu cũ đã cache.
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
