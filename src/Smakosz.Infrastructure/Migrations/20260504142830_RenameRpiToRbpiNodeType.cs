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
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'system_nodes') THEN
        UPDATE system_nodes SET node_type = 'rbpi_gateway' WHERE node_type = 'rpi_gateway';
    END IF;
END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'system_nodes') THEN
        UPDATE system_nodes SET node_type = 'rpi_gateway' WHERE node_type = 'rbpi_gateway';
    END IF;
END $$;");
        }
    }
}
