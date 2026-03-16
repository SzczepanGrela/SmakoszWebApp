using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Smakosz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHomePageCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "home_page_cache",
                schema: "system",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    trending_restaurants_json = table.Column<string>(type: "text", nullable: true),
                    trending_dishes_json = table.Column<string>(type: "text", nullable: true),
                    top_rated_dishes_json = table.Column<string>(type: "text", nullable: true),
                    recent_reviews_json = table.Column<string>(type: "text", nullable: true),
                    popular_categories_json = table.Column<string>(type: "text", nullable: true),
                    hero_image_json = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_home_page_cache", x => x.id);
                });

            migrationBuilder.InsertData(
                schema: "system",
                table: "home_page_cache",
                columns: new[] { "id", "hero_image_json", "popular_categories_json", "recent_reviews_json", "top_rated_dishes_json", "trending_dishes_json", "trending_restaurants_json" },
                values: new object[] { 1, null, null, null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "home_page_cache",
                schema: "system");
        }
    }
}
