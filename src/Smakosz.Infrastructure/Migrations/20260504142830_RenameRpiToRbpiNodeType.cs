using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smakosz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameRpiToRbpiNodeType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE system_nodes SET node_type = 'rbpi_gateway' WHERE node_type = 'rpi_gateway';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE system_nodes SET node_type = 'rpi_gateway' WHERE node_type = 'rbpi_gateway';");
        }
    }
}
