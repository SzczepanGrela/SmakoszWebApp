using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smakosz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationStatusToMenuSection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "moderation_status",
                table: "menu_sections",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "none");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "moderation_status",
                table: "menu_sections");
        }
    }
}
