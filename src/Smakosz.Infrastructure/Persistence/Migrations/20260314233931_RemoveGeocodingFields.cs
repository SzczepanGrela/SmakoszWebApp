using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smakosz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGeocodingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "geocode_source",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "geocoded_at",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "restaurants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "geocode_source",
                table: "restaurants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "geocoded_at",
                table: "restaurants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "latitude",
                table: "restaurants",
                type: "numeric(10,7)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "longitude",
                table: "restaurants",
                type: "numeric(10,7)",
                nullable: true);
        }
    }
}
