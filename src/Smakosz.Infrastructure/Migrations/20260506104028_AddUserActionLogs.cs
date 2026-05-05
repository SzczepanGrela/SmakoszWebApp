using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Smakosz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserActionLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_action_logs",
                schema: "system",
                columns: table => new
                {
                    action_log_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    actor_user_id = table.Column<int>(type: "integer", nullable: true),
                    action_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    old_value = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    new_value = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_action_logs", x => x.action_log_id);
                    table.ForeignKey(
                        name: "fk_user_action_logs_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_user_action_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_action_logs_actor_user_id",
                schema: "system",
                table: "user_action_logs",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_action_logs_user_id_created_at",
                schema: "system",
                table: "user_action_logs",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_action_logs",
                schema: "system");
        }
    }
}
