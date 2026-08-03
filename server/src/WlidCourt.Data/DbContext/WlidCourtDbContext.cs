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
        // 已落实体 7 张表的主键/约束配置（字段见文档 15.3）；
        // 其余 6 张表（nonfiction_collections / badges / match_pools / operation_logs /
        // daily_settlements / sign_in_records）待实体补齐后加入。
        modelBuilder.Entity<UserEntity>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).ValueGeneratedOnAdd();
            e.Property(x => x.Username).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(128).IsRequired();
            e.Property(x => x.Nickname).HasMaxLength(20).IsRequired();
            e.Property(x => x.ProvinceId).IsRequired();
            e.Property(x => x.CityId).IsRequired();
            e.Property(x => x.Level).IsRequired();
        });

        modelBuilder.Entity<PetEntity>(e =>
        {
            e.ToTable("pets");
            e.HasKey(x => x.PetId);
            e.Property(x => x.PetId).ValueGeneratedOnAdd();
            e.Property(x => x.Type).HasMaxLength(32).IsRequired();
            e.Property(x => x.Status).HasMaxLength(16).IsRequired();
        });

        modelBuilder.Entity<LetterEntity>(e =>
        {
            e.ToTable("letters");
            e.HasKey(x => x.LetterId);
            e.Property(x => x.LetterId).ValueGeneratedOnAdd();
            e.Property(x => x.Content).HasMaxLength(200).IsRequired();
            e.Property(x => x.ItemId).HasMaxLength(64);
            e.Property(x => x.Status).HasMaxLength(16).IsRequired();
        });

        modelBuilder.Entity<FriendEntity>(e =>
        {
            e.ToTable("friends");
            e.HasKey(x => new { x.UserId, x.FriendId });
        });

        modelBuilder.Entity<InventoryEntity>(e =>
        {
            e.ToTable("inventories");
            e.HasKey(x => new { x.UserId, x.ItemId });
            e.Property(x => x.ItemId).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<YardEntity>(e =>
        {
            e.ToTable("yards");
            e.HasKey(x => x.UserId);
            e.Property(x => x.LayoutJson).HasColumnType("longtext").IsRequired();
        });

        modelBuilder.Entity<TransactionEntity>(e =>
        {
            e.ToTable("transactions");
            e.HasKey(x => x.TransId);
            e.Property(x => x.TransId).ValueGeneratedOnAdd();
            e.Property(x => x.Type).HasMaxLength(32).IsRequired();
            e.Property(x => x.ItemId).HasMaxLength(64).IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }
}
