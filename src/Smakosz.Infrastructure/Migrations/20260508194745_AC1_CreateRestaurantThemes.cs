using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Smakosz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AC1_CreateRestaurantThemes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "restaurant_themes",
                columns: table => new
                {
                    theme_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    icon = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    cuisine_type_id = table.Column<int>(type: "integer", nullable: false),
                    weight = table.Column<double>(type: "double precision", nullable: false),
                    prompt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_restaurant_themes", x => x.theme_id);
                    table.ForeignKey(
                        name: "fk_restaurant_themes_cuisine_types_cuisine_type_id",
                        column: x => x.cuisine_type_id,
                        principalTable: "cuisine_types",
                        principalColumn: "cuisine_type_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_themes_cuisine_type_id",
                table: "restaurant_themes",
                column: "cuisine_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_themes_name",
                table: "restaurant_themes",
                column: "name",
                unique: true);

            migrationBuilder.Sql(@"
                INSERT INTO cuisine_types (cuisine_type_id, name, display_name, icon)
                VALUES (1, 'inna', 'Inna kuchnia', NULL)
                ON CONFLICT (cuisine_type_id) DO NOTHING;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO restaurant_themes (theme_id, public_id, name, display_name, icon, cuisine_type_id, weight, prompt)
                VALUES (1, gen_random_uuid(), 'inne', 'Inne', NULL, 1, 0.0, NULL);
                SELECT setval(pg_get_serial_sequence('restaurant_themes', 'theme_id'),
                              GREATEST((SELECT MAX(theme_id) FROM restaurant_themes), 1));
            ");

            migrationBuilder.AddColumn<int>(
                name: "theme_id",
                table: "restaurants",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(@"
                UPDATE restaurants r
                SET theme_id = COALESCE((
                    SELECT t.theme_id FROM restaurant_themes t
                    WHERE t.cuisine_type_id = r.cuisine_type_id
                    ORDER BY t.theme_id
                    LIMIT 1
                ), 1);
            ");

            migrationBuilder.CreateIndex(
                name: "ix_restaurants_theme_id",
                table: "restaurants",
                column: "theme_id");

            migrationBuilder.AddForeignKey(
                name: "fk_restaurants_restaurant_themes_theme_id",
                table: "restaurants",
                column: "theme_id",
                principalTable: "restaurant_themes",
                principalColumn: "theme_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_restaurants_restaurant_themes_theme_id",
                table: "restaurants");

            migrationBuilder.DropIndex(
                name: "ix_restaurants_theme_id",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "theme_id",
                table: "restaurants");

            migrationBuilder.DropTable(
                name: "restaurant_themes");
        }
    }
}
