using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WildCourt.Data.Entities;

namespace WildCourt.Data.DbContext;

/// <summary>
/// EF Core DbContext（MySQL，Pomelo 驱动）。
/// 对应文档 02 §2.8：共 13 张核心表 + 扩展表（admins/announcements/operation_logs）。
/// 剩余待补核心表：nonfiction_collections / badges / match_pools / daily_settlements。
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

    public DbSet<FarmPlotEntity> FarmPlots => Set<FarmPlotEntity>();

    public DbSet<StealRecordEntity> StealRecords => Set<StealRecordEntity>();

    public DbSet<SignInRecordEntity> SignInRecords => Set<SignInRecordEntity>();

    public DbSet<AdminEntity> Admins => Set<AdminEntity>();

    public DbSet<AnnouncementEntity> Announcements => Set<AnnouncementEntity>();

    public DbSet<AdminOperationLogEntity> AdminOperationLogs => Set<AdminOperationLogEntity>();

    public DbSet<FriendRequestEntity> FriendRequests => Set<FriendRequestEntity>();

    public DbSet<ChatMessageEntity> ChatMessages => Set<ChatMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureUser(modelBuilder);
        ConfigurePet(modelBuilder);
        ConfigureLetter(modelBuilder);
        ConfigureFriend(modelBuilder);
        ConfigureInventory(modelBuilder);
        ConfigureYard(modelBuilder);
        ConfigureTransaction(modelBuilder);
        ConfigureFarmPlot(modelBuilder);
        ConfigureStealRecord(modelBuilder);
        ConfigureSignInRecord(modelBuilder);
        ConfigureAdmin(modelBuilder);
        ConfigureAnnouncement(modelBuilder);
        ConfigureAdminOperationLog(modelBuilder);
        ConfigureFriendRequest(modelBuilder);
        ConfigureChatMessage(modelBuilder);
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
            e.Property(x => x.AvatarUrl).HasColumnName("avatar_url").HasColumnType("longtext");
            e.Property(x => x.ProvinceId).HasColumnName("province_id").IsRequired();
            e.Property(x => x.CityId).HasColumnName("city_id").IsRequired();
            e.Property(x => x.Level).HasColumnName("level").IsRequired();
            e.Property(x => x.Shell).HasColumnName("shell").IsRequired();
            e.Property(x => x.Coupon).HasColumnName("coupon").IsRequired();
            e.Property(x => x.Exp).HasColumnName("exp").IsRequired();
            e.Property(x => x.IsBanned).HasColumnName("is_banned").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            e.Property(x => x.LastLogin).HasColumnName("last_login");
        });
    }

    private static void ConfigureAdmin(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminEntity>(e =>
        {
            e.ToTable("admins");
            e.HasKey(x => x.AdminId);
            e.Property(x => x.AdminId).HasColumnName("admin_id").ValueGeneratedOnAdd();
            e.Property(x => x.Username).HasColumnName("username").HasMaxLength(32).IsRequired();
            e.HasIndex(x => x.Username).HasDatabaseName("uk_admin_username").IsUnique();
            e.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(128).IsRequired();
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(32).IsRequired();
            e.Property(x => x.Role).HasColumnName("role").HasMaxLength(16).HasDefaultValue("operator").IsRequired();
            e.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            e.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
            e.Property(x => x.TwoFactorEnabled).HasColumnName("two_factor_enabled").IsRequired();
            e.Property(x => x.TwoFactorSecret).HasColumnName("two_factor_secret").HasMaxLength(64);
        });
    }

    private static void ConfigureAnnouncement(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnnouncementEntity>(e =>
        {
            e.ToTable("announcements");
            e.HasKey(x => x.AnnouncementId);
            e.Property(x => x.AnnouncementId).HasColumnName("announcement_id").ValueGeneratedOnAdd();
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(100).IsRequired();
            e.Property(x => x.Content).HasColumnName("content").HasColumnType("longtext").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            e.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            e.Property(x => x.PublishedAt).HasColumnName("published_at");
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
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(100);
            e.Property(x => x.Content).HasColumnName("content").HasMaxLength(200).IsRequired();
            // 多附件：List<LetterAttachment> 序列化为 JSON 文本存储（旧 item_id/reward_type/reward_count 三列已迁移删除）
            e.Property(x => x.Attachments)
                .HasColumnName("attachments")
                .HasColumnType("longtext")
                .HasConversion(
                    v => JsonSerializer.Serialize(v ?? new List<LetterAttachment>(), (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<LetterAttachment>>(v, (JsonSerializerOptions?)null) ?? new List<LetterAttachment>());
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

    private static void ConfigureFriendRequest(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FriendRequestEntity>(e =>
        {
            e.ToTable("friend_requests");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.FromUserId).HasColumnName("from_user_id").IsRequired();
            e.Property(x => x.ToUserId).HasColumnName("to_user_id").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            e.HasIndex(x => new { x.ToUserId, x.Status }).HasDatabaseName("idx_friend_requests_to_status");
        });
    }

    private static void ConfigureChatMessage(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatMessageEntity>(e =>
        {
            e.ToTable("chat_messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.FromUserId).HasColumnName("from_user_id").IsRequired();
            e.Property(x => x.ToUserId).HasColumnName("to_user_id").IsRequired();
            e.Property(x => x.Content).HasColumnName("content").HasMaxLength(1000).IsRequired();
            e.Property(x => x.IsRead).HasColumnName("is_read").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            e.HasIndex(x => new { x.ToUserId, x.IsRead }).HasDatabaseName("idx_chat_messages_to_read");
            e.HasIndex(x => new { x.FromUserId, x.ToUserId }).HasDatabaseName("idx_chat_messages_from_to");
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

    private static void ConfigureFarmPlot(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FarmPlotEntity>(e =>
        {
            e.ToTable("farm_plots");
            e.HasKey(x => new { x.UserId, x.CellId });
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.Property(x => x.CellId).HasColumnName("cell_id").IsRequired();
            e.Property(x => x.CropId).HasColumnName("crop_id").HasMaxLength(32).IsRequired();
            e.Property(x => x.PlantedAt).HasColumnName("planted_at").IsRequired();
            e.Property(x => x.ReadyAt).HasColumnName("ready_at").IsRequired();
            e.Property(x => x.State).HasColumnName("state").HasMaxLength(16).IsRequired();
            e.Property(x => x.StolenCount).HasColumnName("stolen_count").IsRequired();
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        });
    }

    private static void ConfigureStealRecord(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StealRecordEntity>(e =>
        {
            e.ToTable("steal_records");
            e.HasKey(x => x.RecordId);
            e.Property(x => x.RecordId).HasColumnName("record_id").ValueGeneratedOnAdd();
            e.Property(x => x.StealerUserId).HasColumnName("stealer_user_id").IsRequired();
            e.Property(x => x.TargetUserId).HasColumnName("target_user_id").IsRequired();
            e.Property(x => x.CropId).HasColumnName("crop_id").HasMaxLength(32).IsRequired();
            e.Property(x => x.CellId).HasColumnName("cell_id").IsRequired();
            e.Property(x => x.Count).HasColumnName("count").IsRequired();
            e.Property(x => x.StolenAt).HasColumnName("stolen_at").IsRequired();
        });
    }

    private static void ConfigureSignInRecord(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SignInRecordEntity>(e =>
        {
            e.ToTable("sign_in_records");
            e.HasKey(x => x.RecordId);
            e.Property(x => x.RecordId).HasColumnName("record_id").ValueGeneratedOnAdd();
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.Property(x => x.SignDate).HasColumnName("sign_date").HasColumnType("date").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            e.HasIndex(x => new { x.UserId, x.SignDate }).HasDatabaseName("uk_sign_in_user_date").IsUnique();
        });
    }

    private static void ConfigureAdminOperationLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminOperationLogEntity>(e =>
        {
            e.ToTable("operation_logs");
            e.HasKey(x => x.LogId);
            e.Property(x => x.LogId).HasColumnName("log_id").ValueGeneratedOnAdd();
            e.Property(x => x.AdminId).HasColumnName("admin_id");
            e.Property(x => x.AdminName).HasColumnName("admin_name").HasMaxLength(32).IsRequired();
            e.Property(x => x.Action).HasColumnName("action").HasMaxLength(64).IsRequired();
            e.Property(x => x.Method).HasColumnName("method").HasMaxLength(8).IsRequired();
            e.Property(x => x.Path).HasColumnName("path").HasMaxLength(255).IsRequired();
            e.Property(x => x.Details).HasColumnName("details").HasColumnType("longtext");
            e.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            e.Property(x => x.StatusCode).HasColumnName("status_code").IsRequired();
            e.Property(x => x.DurationMs).HasColumnName("duration_ms").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            e.HasIndex(x => x.AdminId).HasDatabaseName("idx_operation_logs_admin_id");
            e.HasIndex(x => x.Action).HasDatabaseName("idx_operation_logs_action");
            e.HasIndex(x => x.CreatedAt).HasDatabaseName("idx_operation_logs_created_at");
        });
    }
}
