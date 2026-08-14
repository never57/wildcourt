using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WildCourt.Data.Migrations
{
    /// <summary>
    /// 官方信件状态机重构：送达 → 未读。
    /// 仅迁移官方链路（from_user=0）的初始状态值；玩家写信审核链路（待审核/通过/退回）不受影响。
    /// </summary>
    public partial class RenameLetterStatusDeliveredToUnread : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 将官方信件的初始状态「送达」重命名为「未读」（仅 from_user=0 的系统信件）
            migrationBuilder.Sql(
                "UPDATE `letters` SET `status` = '未读' WHERE `status` = '送达' AND `from_user` = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回滚：官方信件的「未读」还原为「送达」
            migrationBuilder.Sql(
                "UPDATE `letters` SET `status` = '送达' WHERE `status` = '未读' AND `from_user` = 0;");
        }
    }
}
