using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using WildCourt.Data.DbContext;

#nullable disable

namespace WildCourt.Data.Migrations
{
    /// <summary>
    /// 创建 fishing_events 表（钓鱼事件上报落库）。
    /// 注：[Migration]/[DbContext] 属性通常由 EF CLI 生成的 .Designer.cs 承载；
    /// 此处直接标注于主类，使运行时 db.Database.Migrate() 可发现并应用本迁移，无需 Designer 文件。
    /// 后续若用 `dotnet ef migrations add` 生成新迁移，工具会自动补全 Designer + ModelSnapshot。
    /// </summary>
    [DbContext(typeof(WildCourtDbContext))]
    [Migration("20260820120000_AddFishingEvents")]
    public partial class AddFishingEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fishing_events",
                columns: table => new
                {
                    event_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    event_type = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    bait_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fish_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fish_name = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reason = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    timestamp_ms = table.Column<long>(type: "bigint", nullable: false),
                    server_time = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fishing_events", x => x.event_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "idx_fishing_events_user_id",
                table: "fishing_events",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_fishing_events_created_at",
                table: "fishing_events",
                column: "server_time");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fishing_events");
        }
    }
}
