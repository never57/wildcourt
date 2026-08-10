using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WildCourt.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFarmPlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "farm_plots",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    cell_id = table.Column<int>(type: "int", nullable: false),
                    crop_id = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    planted_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ready_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    state = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_farm_plots", x => new { x.user_id, x.cell_id });
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "farm_plots");
        }
    }
}
