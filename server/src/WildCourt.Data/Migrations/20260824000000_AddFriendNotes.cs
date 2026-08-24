using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using WildCourt.Data.DbContext;

#nullable disable

namespace WildCourt.Data.Migrations
{
    /// <summary>
    /// 创建 friend_notes 表（单向好友备注）。
    /// 注：[Migration]/[DbContext] 属性直接标注于主类，使运行时 db.Database.Migrate() 可发现并应用本迁移，无需 Designer 文件。
    /// </summary>
    [DbContext(typeof(WildCourtDbContext))]
    [Migration("20260824000000_AddFriendNotes")]
    public partial class AddFriendNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "friend_notes",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    friend_id = table.Column<long>(type: "bigint", nullable: false),
                    note = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_friend_notes", x => new { x.user_id, x.friend_id });
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "friend_notes");
        }
    }
}
