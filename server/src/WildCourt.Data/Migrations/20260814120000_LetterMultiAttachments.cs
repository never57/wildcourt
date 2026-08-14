using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WildCourt.Data.Migrations
{
    /// <summary>
    /// 邮件附件从单附件（item_id/reward_type/reward_count 三列）迁移为多附件（attachments JSON 列）。
    /// 步骤：加 attachments 列 → 旧数据转换（单附件包成 JSON 数组）→ 删旧三列。
    /// </summary>
    public partial class LetterMultiAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. 加 attachments 列（longtext，可空；无附件为 NULL）
            migrationBuilder.AddColumn<string>(
                name: "attachments",
                table: "letters",
                type: "longtext",
                nullable: true);

            // 2. 旧数据转换：把单附件（reward_type 非空）包成 JSON 数组写入 attachments
            //    仅官方信件（from_user=0）曾携带附件；审核链路信件 reward_type 恒为 NULL，不受影响。
            //    序列化字段名与 LetterAttachment 属性名一致（PascalCase）：Type / ItemId / Count。
            migrationBuilder.Sql(@"
                UPDATE `letters` SET `attachments` =
                    JSON_ARRAY(
                        JSON_OBJECT(
                            'Type', `reward_type`,
                            'ItemId', CASE WHEN `reward_type` = 'item' THEN `item_id` ELSE NULL END,
                            'Count', `reward_count`
                        )
                    )
                WHERE `reward_type` IS NOT NULL AND `reward_type` <> '';
            ");

            // 3. 删除旧三列
            migrationBuilder.DropColumn(name: "item_id", table: "letters");
            migrationBuilder.DropColumn(name: "reward_type", table: "letters");
            migrationBuilder.DropColumn(name: "reward_count", table: "letters");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回滚：重新加回三列，把 attachments 第一个附件展开回写，再删 attachments 列
            migrationBuilder.AddColumn<string>(
                name: "item_id",
                table: "letters",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "reward_type",
                table: "letters",
                type: "varchar(16)",
                maxLength: 16,
                nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "reward_count",
                table: "letters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // 从 attachments 第一个元素恢复（多附件无法完整还原，仅恢复首个，回滚为尽力而为）
            migrationBuilder.Sql(@"
                UPDATE `letters` SET
                    `reward_type` = JSON_UNQUOTE(JSON_EXTRACT(`attachments`, '$[0].Type')),
                    `item_id` = JSON_UNQUOTE(JSON_EXTRACT(`attachments`, '$[0].ItemId')),
                    `reward_count` = COALESCE(JSON_EXTRACT(`attachments`, '$[0].Count'), 0)
                WHERE `attachments` IS NOT NULL;
            ");

            migrationBuilder.DropColumn(name: "attachments", table: "letters");
        }
    }
}
