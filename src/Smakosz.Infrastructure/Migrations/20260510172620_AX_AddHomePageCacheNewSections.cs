using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smakosz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AX_AddHomePageCacheNewSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "most_reviewed_dishes_json",
                schema: "system",
                table: "home_page_cache",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "newest_restaurants_json",
                schema: "system",
                table: "home_page_cache",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "system",
                table: "home_page_cache",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "most_reviewed_dishes_json", "newest_restaurants_json" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "most_reviewed_dishes_json",
                schema: "system",
                table: "home_page_cache");

            migrationBuilder.DropColumn(
                name: "newest_restaurants_json",
                schema: "system",
                table: "home_page_cache");
        }
    }
}
