using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WlidCourt.Data.DbContext;

/// <summary>
/// 设计时工厂：仅供 `dotnet ef migrations` 生成迁移使用（不连接 MySQL、不启动应用 host）。
/// 运行时（Program.cs）使用真正的 appsettings 连接串，本工厂不被调用。
/// </summary>
public class WlidCourtDbContextFactory : IDesignTimeDbContextFactory<WlidCourtDbContext>
{
    public WlidCourtDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<WlidCourtDbContext>()
            .UseMySql(
                "Server=localhost;Port=3306;Database=wlidcourt;User=root;Password=;CharSet=utf8mb4;",
                new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;
        return new WlidCourtDbContext(options);
    }
}
