using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WlidCourt.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "coupon",
                table: "users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "shell",
                table: "users",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "coupon",
                table: "users");

            migrationBuilder.DropColumn(
                name: "shell",
                table: "users");
        }
    }
}
