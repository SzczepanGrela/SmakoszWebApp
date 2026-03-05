using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smakosz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNeedsReviewStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Make is_approved nullable
            migrationBuilder.AlterColumn<bool>(
                name: "is_approved",
                table: "reviews",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            // Data fix: set is_approved = NULL for pending reviews that were incorrectly set to false
            migrationBuilder.Sql(
                "UPDATE reviews SET is_approved = NULL WHERE content_status = 'pending' AND is_approved = false;");

            // Recreate partial index on reviews to include needs_review
            migrationBuilder.DropIndex(
                name: "ix_reviews_content_status",
                table: "reviews");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_content_status",
                table: "reviews",
                columns: new[] { "content_status", "created_at" },
                filter: "content_status IN ('pending', 'needs_review')");

            // Recreate partial index on media_assets to include needs_review
            migrationBuilder.DropIndex(
                name: "ix_media_assets_moderation",
                table: "media_assets");

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_moderation",
                table: "media_assets",
                columns: new[] { "status", "created_at" },
                filter: "status IN ('pending', 'needs_review')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert is_approved to non-nullable
            migrationBuilder.Sql(
                "UPDATE reviews SET is_approved = false WHERE is_approved IS NULL;");

            migrationBuilder.AlterColumn<bool>(
                name: "is_approved",
                table: "reviews",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            // Revert partial index on reviews
            migrationBuilder.DropIndex(
                name: "ix_reviews_content_status",
                table: "reviews");

            migrationBuilder.CreateIndex(
                name: "ix_reviews_content_status",
                table: "reviews",
                columns: new[] { "content_status", "created_at" },
                filter: "content_status = 'pending'");

            // Revert partial index on media_assets
            migrationBuilder.DropIndex(
                name: "ix_media_assets_moderation",
                table: "media_assets");

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_moderation",
                table: "media_assets",
                columns: new[] { "status", "created_at" },
                filter: "status = 'pending'");
        }
    }
}
