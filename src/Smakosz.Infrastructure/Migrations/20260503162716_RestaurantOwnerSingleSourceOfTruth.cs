using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smakosz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestaurantOwnerSingleSourceOfTruth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_restaurants_restaurant_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_restaurant_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_restaurants_owner_id",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "restaurant_id",
                table: "users");

            migrationBuilder.AddColumn<int>(
                name: "requester_id",
                schema: "system",
                table: "tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resolution",
                schema: "system",
                table: "tickets",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "resolved_at",
                schema: "system",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "resolved_by_admin_id",
                schema: "system",
                table: "tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tickets_requester_id",
                schema: "system",
                table: "tickets",
                column: "requester_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_resolved_by_admin_id",
                schema: "system",
                table: "tickets",
                column: "resolved_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_ticket_type_status",
                schema: "system",
                table: "tickets",
                columns: new[] { "ticket_type", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_restaurants_owner_id_unique",
                table: "restaurants",
                column: "owner_id",
                unique: true,
                filter: "owner_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_tickets_users_requester_id",
                schema: "system",
                table: "tickets",
                column: "requester_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_tickets_users_resolved_by_admin_id",
                schema: "system",
                table: "tickets",
                column: "resolved_by_admin_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tickets_users_requester_id",
                schema: "system",
                table: "tickets");

            migrationBuilder.DropForeignKey(
                name: "fk_tickets_users_resolved_by_admin_id",
                schema: "system",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_tickets_requester_id",
                schema: "system",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_tickets_resolved_by_admin_id",
                schema: "system",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_tickets_ticket_type_status",
                schema: "system",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_restaurants_owner_id_unique",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "requester_id",
                schema: "system",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "resolution",
                schema: "system",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "resolved_at",
                schema: "system",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "resolved_by_admin_id",
                schema: "system",
                table: "tickets");

            migrationBuilder.AddColumn<int>(
                name: "restaurant_id",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_restaurant_id",
                table: "users",
                column: "restaurant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_restaurants_owner_id",
                table: "restaurants",
                column: "owner_id");

            migrationBuilder.AddForeignKey(
                name: "fk_users_restaurants_restaurant_id",
                table: "users",
                column: "restaurant_id",
                principalTable: "restaurants",
                principalColumn: "restaurant_id");
        }
    }
}
