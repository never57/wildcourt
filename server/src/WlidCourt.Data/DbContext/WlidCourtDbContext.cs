using Microsoft.EntityFrameworkCore;
using WlidCourt.Data.Entities;

namespace WlidCourt.Data.DbContext;

/// <summary>
/// EF Core DbContext（MySQL，Pomelo 驱动）。
/// 对应文档 02 §2.8：共 13 张表，骨架阶段先声明已落实体的 7 张表，
/// 其余 6 张（nonfiction_collections / badges / match_pools / operation_logs /
/// daily_settlements / sign_in_records）待实体补齐后加入。
/// 注：命名空间 WlidCourt.Data.DbContext 与 EF Core 的 DbContext 类同名，
/// 故基类使用全限定名 Microsoft.EntityFrameworkCore.DbContext。
/// </summary>
public class WlidCourtDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public WlidCourtDbContext(DbContextOptions<WlidCourtDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserEntity> Users => Set<UserEntity>();

    public DbSet<PetEntity> Pets => Set<PetEntity>();

    public DbSet<LetterEntity> Letters => Set<LetterEntity>();

    public DbSet<FriendEntity> Friends => Set<FriendEntity>();

    public DbSet<InventoryEntity> Inventories => Set<InventoryEntity>();

    public DbSet<YardEntity> Yards => Set<YardEntity>();

    public DbSet<TransactionEntity> Transactions => Set<TransactionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // TODO: 表名/主键/索引/长度约束等 Fluent 配置（字段见文档 15.3），骨架阶段留空。
        base.OnModelCreating(modelBuilder);
    }
}
