using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smakosz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AC2_AddUniqueCuisineDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE restaurants r
                SET cuisine_type_id = (
                    SELECT MIN(c2.cuisine_type_id) FROM cuisine_types c2
                    WHERE c2.display_name = (SELECT display_name FROM cuisine_types WHERE cuisine_type_id = r.cuisine_type_id)
                )
                WHERE r.cuisine_type_id NOT IN (
                    SELECT MIN(cuisine_type_id) FROM cuisine_types GROUP BY display_name
                );

                UPDATE restaurant_themes t
                SET cuisine_type_id = (
                    SELECT MIN(c2.cuisine_type_id) FROM cuisine_types c2
                    WHERE c2.display_name = (SELECT display_name FROM cuisine_types WHERE cuisine_type_id = t.cuisine_type_id)
                )
                WHERE t.cuisine_type_id NOT IN (
                    SELECT MIN(cuisine_type_id) FROM cuisine_types GROUP BY display_name
                );

                DELETE FROM cuisine_types WHERE cuisine_type_id NOT IN (
                    SELECT MIN(cuisine_type_id) FROM cuisine_types GROUP BY display_name
                );
            ");

            migrationBuilder.CreateIndex(
                name: "ix_cuisine_types_display_name",
                table: "cuisine_types",
                column: "display_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_cuisine_types_display_name",
                table: "cuisine_types");
        }
    }
}
