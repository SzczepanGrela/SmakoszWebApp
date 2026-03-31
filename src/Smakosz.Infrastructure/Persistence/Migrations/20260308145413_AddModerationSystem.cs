using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Smakosz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reviews_content_status",
                table: "reviews");

            migrationBuilder.DropIndex(
                name: "ix_media_assets_moderation",
                table: "media_assets");

            migrationBuilder.DropColumn(
                name: "ai_model_version",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "ai_processed_at",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "ai_spam_score",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "ai_toxicity_score",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "ai_verdict",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "ai_confidence",
                table: "restaurant_edit_requests");

            migrationBuilder.DropColumn(
                name: "ai_model_version",
                table: "restaurant_edit_requests");

            migrationBuilder.DropColumn(
                name: "ai_processed_at",
                table: "restaurant_edit_requests");

            migrationBuilder.DropColumn(
                name: "ai_verdict",
                table: "restaurant_edit_requests");

            migrationBuilder.DropColumn(
                name: "auto_approve_reason",
                table: "restaurant_edit_requests");

            migrationBuilder.DropColumn(
                name: "auto_approved",
                table: "restaurant_edit_requests");

            migrationBuilder.DropColumn(
                name: "ai_model_version",
                table: "media_assets");

            migrationBuilder.DropColumn(
                name: "ai_nsfw_score",
                table: "media_assets");

            migrationBuilder.DropColumn(
                name: "ai_on_topic_score",
                table: "media_assets");

            migrationBuilder.DropColumn(
                name: "ai_processed_at",
                table: "media_assets");

            migrationBuilder.DropColumn(
                name: "ai_verdict",
                table: "media_assets");

            migrationBuilder.AlterColumn<bool>(
                name: "is_approved",
                table: "reviews",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<string>(
                name: "moderation_status",
                table: "restaurants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "moderation_status",
                table: "restaurant_edit_requests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "moderation_status",
                table: "menu_sections",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "moderation_status",
                table: "dishes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "model_name",
                schema: "system",
                table: "ai_logs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "moderation_results",
                schema: "system",
                columns: table => new
                {
                    result_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ai_verdict = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ai_model_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ai_model_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    scores = table.Column<string>(type: "jsonb", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    auto_approved = table.Column<bool>(type: "boolean", nullable: false),
                    auto_approve_reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moderation_results", x => x.result_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reviews_content_status",
                table: "reviews",
                columns: new[] { "content_status", "created_at" },
                filter: "content_status IN ('pending', 'needs_review')");

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_moderation",
                table: "media_assets",
                columns: new[] { "status", "created_at" },
                filter: "status IN ('pending', 'needs_review')");

            migrationBuilder.CreateIndex(
                name: "ix_moderation_results_entity_type_entity_id",
                schema: "system",
                table: "moderation_results",
                columns: new[] { "entity_type", "entity_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_moderation_results_status_processed_at",
                schema: "system",
                table: "moderation_results",
                columns: new[] { "status", "processed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "moderation_results",
                schema: "system");

            migrationBuilder.DropIndex(
                name: "ix_reviews_content_status",
                table: "reviews");

            migrationBuilder.DropIndex(
                name: "ix_media_assets_moderation",
                table: "media_assets");

            migrationBuilder.DropColumn(
                name: "moderation_status",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "moderation_status",
                table: "restaurant_edit_requests");

            migrationBuilder.DropColumn(
                name: "moderation_status",
                table: "menu_sections");

            migrationBuilder.DropColumn(
                name: "moderation_status",
                table: "dishes");

            migrationBuilder.DropColumn(
                name: "model_name",
                schema: "system",
                table: "ai_logs");

            migrationBuilder.AlterColumn<bool>(
                name: "is_approved",
                table: "reviews",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_model_version",
                table: "reviews",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ai_processed_at",
                table: "reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ai_spam_score",
                table: "reviews",
                type: "numeric(5,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ai_toxicity_score",
                table: "reviews",
                type: "numeric(5,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_verdict",
                table: "reviews",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ai_confidence",
                table: "restaurant_edit_requests",
                type: "numeric(5,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_model_version",
                table: "restaurant_edit_requests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ai_processed_at",
                table: "restaurant_edit_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_verdict",
                table: "restaurant_edit_requests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "auto_approve_reason",
                table: "restaurant_edit_requests",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "auto_approved",
                table: "restaurant_edit_requests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ai_model_version",
                table: "media_assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ai_nsfw_score",
                table: "media_assets",
                type: "numeric(5,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ai_on_topic_score",
                table: "media_assets",
                type: "numeric(5,4)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ai_processed_at",
                table: "media_assets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_verdict",
                table: "media_assets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_reviews_content_status",
                table: "reviews",
                columns: new[] { "content_status", "created_at" },
                filter: "content_status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_moderation",
                table: "media_assets",
                columns: new[] { "status", "created_at" },
                filter: "status = 'pending'");
        }
    }
}
