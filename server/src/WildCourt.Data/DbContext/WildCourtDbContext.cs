using Microsoft.EntityFrameworkCore;
using WildCourt.Data.Entities;

namespace WildCourt.Data.DbContext;

/// <summary>
/// EF Core DbContext（MySQL，Pomelo 驱动）。
/// 对应文档 02 §2.8：共 13 张表，当前已落实体 7 张；其余 6 张
/// （nonfiction_collections / badges / match_pools / operation_logs /
/// daily_settlements / sign_in_records）待实体补齐后加入。
/// 列名统一 snake_case（与文档 15.3 / init_db.sql 对齐）。
/// 注：命名空间 WildCourt.Data.DbContext 与 EF Core 的 DbContext 类同名，
/// 故基类使用全限定名 Microsoft.EntityFrameworkCore.DbContext。
/// </summary>
public class WildCourtDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public WildCourtDbContext(DbContextOptions<WildCourtDbContext> options)
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
        ConfigureUser(modelBuilder);
        ConfigurePet(modelBuilder);
        ConfigureLetter(modelBuilder);
        ConfigureFriend(modelBuilder);
        ConfigureInventory(modelBuilder);
        ConfigureYard(modelBuilder);
        ConfigureTransaction(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).HasColumnName("user_id").ValueGeneratedOnAdd();
            e.Property(x => x.Username).HasColumnName("username").HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Username).HasDatabaseName("uk_username").IsUnique();
            e.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(128).IsRequired();
            e.Property(x => x.Nickname).HasColumnName("nickname").HasMaxLength(20).IsRequired();
            e.Property(x => x.ProvinceId).HasColumnName("province_id").IsRequired();
            e.Property(x => x.CityId).HasColumnName("city_id").IsRequired();
            e.Property(x => x.Level).HasColumnName("level").IsRequired();
            e.Property(x => x.Shell).HasColumnName("shell").IsRequired();
            e.Property(x => x.Coupon).HasColumnName("coupon").IsRequired();
            e.Property(x => x.Exp).HasColumnName("exp").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            e.Property(x => x.LastLogin).HasColumnName("last_login");
        });
    }

    private static void ConfigurePet(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PetEntity>(e =>
        {
            e.ToTable("pets");
            e.HasKey(x => x.PetId);
            e.Property(x => x.PetId).HasColumnName("pet_id").ValueGeneratedOnAdd();
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.Property(x => x.Type).HasColumnName("type").HasMaxLength(32).IsRequired();
            e.Property(x => x.Level).HasColumnName("level").IsRequired();
            e.Property(x => x.Exp).HasColumnName("exp").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
        });
    }

    private static void ConfigureLetter(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LetterEntity>(e =>
        {
            e.ToTable("letters");
            e.HasKey(x => x.LetterId);
            e.Property(x => x.LetterId).HasColumnName("letter_id").ValueGeneratedOnAdd();
            e.Property(x => x.FromUser).HasColumnName("from_user").IsRequired();
            e.Property(x => x.ToUser).HasColumnName("to_user").IsRequired();
            e.Property(x => x.Content).HasColumnName("content").HasMaxLength(200).IsRequired();
            e.Property(x => x.ItemId).HasColumnName("item_id").HasMaxLength(64);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        });
    }

    private static void ConfigureFriend(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FriendEntity>(e =>
        {
            e.ToTable("friends");
            e.HasKey(x => new { x.UserId, x.FriendId });
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.Property(x => x.FriendId).HasColumnName("friend_id").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        });
    }

    private static void ConfigureInventory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InventoryEntity>(e =>
        {
            e.ToTable("inventories");
            e.HasKey(x => new { x.UserId, x.ItemId });
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.Property(x => x.ItemId).HasColumnName("item_id").HasMaxLength(64).IsRequired();
            e.Property(x => x.Count).HasColumnName("count").IsRequired();
        });
    }

    private static void ConfigureYard(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<YardEntity>(e =>
        {
            e.ToTable("yards");
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).HasColumnName("user_id").ValueGeneratedOnAdd();
            e.Property(x => x.LayoutJson).HasColumnName("layout_json").HasColumnType("longtext").IsRequired();
            e.Property(x => x.Level).HasColumnName("level").IsRequired();
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        });
    }

    private static void ConfigureTransaction(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TransactionEntity>(e =>
        {
            e.ToTable("transactions");
            e.HasKey(x => x.TransId);
            e.Property(x => x.TransId).HasColumnName("trans_id").ValueGeneratedOnAdd();
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.Property(x => x.Type).HasColumnName("type").HasMaxLength(32).IsRequired();
            e.Property(x => x.ItemId).HasColumnName("item_id").HasMaxLength(64).IsRequired();
            e.Property(x => x.Count).HasColumnName("count").IsRequired();
            e.Property(x => x.Price).HasColumnName("price").IsRequired();
        });
    }
}
