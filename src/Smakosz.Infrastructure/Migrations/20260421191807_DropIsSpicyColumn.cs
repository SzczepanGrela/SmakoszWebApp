using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smakosz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropIsSpicyColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO dish_tags (dish_id, tag_id)
                SELECT d.dish_id, t.tag_id
                FROM dishes d
                CROSS JOIN tags t
                WHERE d.is_spicy = true
                  AND t.tag_name = 'Ostre'
                  AND t.category = 'spice'
                  AND NOT EXISTS (
                      SELECT 1 FROM dish_tags dt2
                      JOIN tags t2 ON t2.tag_id = dt2.tag_id
                      WHERE dt2.dish_id = d.dish_id
                        AND t2.category = 'spice'
                  )
            ");

            migrationBuilder.DropColumn(
                name: "is_spicy",
                table: "dishes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_spicy",
                table: "dishes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(@"
                UPDATE dishes d SET is_spicy = true
                FROM dish_tags dt
                JOIN tags t ON t.tag_id = dt.tag_id
                WHERE dt.dish_id = d.dish_id
                  AND t.category = 'spice'
                  AND t.tag_name IN ('Ostre', 'Bardzo ostre')
            ");
        }
    }
}
