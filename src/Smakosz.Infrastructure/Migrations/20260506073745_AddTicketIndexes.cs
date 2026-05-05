using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smakosz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tickets_ticket_type_status",
                schema: "system",
                table: "tickets");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_ticket_type_created_at",
                schema: "system",
                table: "tickets",
                columns: new[] { "ticket_type", "created_at" },
                descending: new[] { false, true },
                filter: "status = 'open'");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_ticket_type_status_created_at",
                schema: "system",
                table: "tickets",
                columns: new[] { "ticket_type", "status", "created_at" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tickets_ticket_type_created_at",
                schema: "system",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_tickets_ticket_type_status_created_at",
                schema: "system",
                table: "tickets");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_ticket_type_status",
                schema: "system",
                table: "tickets",
                columns: new[] { "ticket_type", "status" });
        }
    }
}
