using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smakosz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M_AddUserRecommendationCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_recommendation_cache",
                schema: "system",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    top_dish_ids = table.Column<string>(type: "jsonb", nullable: false),
                    model_version = table.Column<string>(type: "text", nullable: false),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_recommendation_cache", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_user_recommendation_cache_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_rec_cache_model_version",
                schema: "system",
                table: "user_recommendation_cache",
                column: "model_version");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_recommendation_cache",
                schema: "system");
        }
    }
}
