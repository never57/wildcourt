using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WildCourt.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLetterReward : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reward_type",
                table: "letters",
                type: "varchar(16)",
                maxLength: 16,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "reward_count",
                table: "letters",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reward_count",
                table: "letters");

            migrationBuilder.DropColumn(
                name: "reward_type",
                table: "letters");
        }
    }
}
