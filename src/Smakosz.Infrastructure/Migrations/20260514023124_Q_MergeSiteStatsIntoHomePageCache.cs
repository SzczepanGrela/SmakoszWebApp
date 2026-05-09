using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Smakosz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Q_MergeSiteStatsIntoHomePageCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "site_stats",
                schema: "system");

            migrationBuilder.AddColumn<int>(
                name: "total_dishes",
                schema: "system",
                table: "home_page_cache",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "total_restaurants",
                schema: "system",
                table: "home_page_cache",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "total_reviews",
                schema: "system",
                table: "home_page_cache",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "total_users",
                schema: "system",
                table: "home_page_cache",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                schema: "system",
                table: "home_page_cache",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "total_dishes", "total_restaurants", "total_reviews", "total_users" },
                values: new object[] { 0, 0, 0, 0 });

            migrationBuilder.Sql("DELETE FROM system.config WHERE key = 'trending.window_days';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "total_dishes",
                schema: "system",
                table: "home_page_cache");

            migrationBuilder.DropColumn(
                name: "total_restaurants",
                schema: "system",
                table: "home_page_cache");

            migrationBuilder.DropColumn(
                name: "total_reviews",
                schema: "system",
                table: "home_page_cache");

            migrationBuilder.DropColumn(
                name: "total_users",
                schema: "system",
                table: "home_page_cache");

            migrationBuilder.CreateTable(
                name: "site_stats",
                schema: "system",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    avg_dish_rating = table.Column<double>(type: "double precision", nullable: false),
                    avg_restaurant_food_score = table.Column<double>(type: "double precision", nullable: false),
                    most_active_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    most_popular_cuisine = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    new_users_this_month = table.Column<int>(type: "integer", nullable: false),
                    reviews_this_week = table.Column<int>(type: "integer", nullable: false),
                    total_dishes = table.Column<int>(type: "integer", nullable: false),
                    total_photos = table.Column<int>(type: "integer", nullable: false),
                    total_restaurants = table.Column<int>(type: "integer", nullable: false),
                    total_reviews = table.Column<int>(type: "integer", nullable: false),
                    total_users = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_site_stats", x => x.id);
                });

            migrationBuilder.InsertData(
                schema: "system",
                table: "site_stats",
                columns: new[] { "id", "avg_dish_rating", "avg_restaurant_food_score", "most_active_city", "most_popular_cuisine", "new_users_this_month", "reviews_this_week", "total_dishes", "total_photos", "total_restaurants", "total_reviews", "total_users" },
                values: new object[] { 1, 0.0, 0.0, null, null, 0, 0, 0, 0, 0, 0, 0 });
        }
    }
}
