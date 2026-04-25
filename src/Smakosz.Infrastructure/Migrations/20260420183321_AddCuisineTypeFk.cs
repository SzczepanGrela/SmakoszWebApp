using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smakosz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCuisineTypeFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            SeedCuisineTypes(migrationBuilder);

            migrationBuilder.AddColumn<int>(
                name: "cuisine_type_id",
                table: "restaurants",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE restaurants r
                SET cuisine_type_id = ct.cuisine_type_id
                FROM cuisine_types ct
                WHERE LOWER(TRIM(r.cuisine_type)) = LOWER(ct.name)
                   OR LOWER(TRIM(r.cuisine_type)) = LOWER(ct.display_name)
            ");

            migrationBuilder.DropIndex(
                name: "ix_restaurants_cuisine_type",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "cuisine_type",
                table: "restaurants");

            migrationBuilder.CreateIndex(
                name: "ix_restaurants_cuisine_type_id",
                table: "restaurants",
                column: "cuisine_type_id");

            migrationBuilder.AddForeignKey(
                name: "fk_restaurants_cuisine_types_cuisine_type_id",
                table: "restaurants",
                column: "cuisine_type_id",
                principalTable: "cuisine_types",
                principalColumn: "cuisine_type_id",
                onDelete: ReferentialAction.SetNull);
        }

        private static void SeedCuisineTypes(MigrationBuilder migrationBuilder)
        {
            var rows = new (string Name, string DisplayName)[]
            {
                ("polska", "Polska"),
                ("wloska", "Włoska"),
                ("francuska", "Francuska"),
                ("hiszpanska", "Hiszpanska"),
                ("grecka", "Grecka"),
                ("portugalska", "Portugalska"),
                ("japonska", "Japonska"),
                ("chinska", "Chinska"),
                ("koreanska", "Koreanska"),
                ("wietnamska", "Wietnamska"),
                ("tajska", "Tajska"),
                ("indyjska", "Indyjska"),
                ("amerykanska", "Amerykanska"),
                ("meksykanska", "Meksykanska"),
                ("brazylijska", "Brazylijska"),
                ("turecka", "Turecka"),
                ("libanska", "Libanska"),
                ("marokanska", "Marokanska"),
                ("izraelska", "Izraelska"),
                ("niemiecka", "Niemiecka"),
                ("austriacka", "Austriacka"),
                ("wegierska", "Wegierska"),
                ("czeska", "Czeska"),
                ("ukrainska", "Ukrainska"),
                ("rosyjska", "Rosyjska"),
                ("wegetarianska", "Wegetarianska"),
                ("weganska", "Weganska"),
                ("fusion", "Fusion"),
                ("street_food", "Street food"),
                ("fast_food", "Fast food"),
                ("kawiarnia", "Kawiarnia")
            };

            var data = new object[rows.Length, 2];
            for (var i = 0; i < rows.Length; i++)
            {
                data[i, 0] = rows[i].Name;
                data[i, 1] = rows[i].DisplayName;
            }

            migrationBuilder.InsertData(
                table: "cuisine_types",
                columns: new[] { "name", "display_name" },
                values: data);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cuisine_type",
                table: "restaurants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE restaurants r
                SET cuisine_type = ct.display_name
                FROM cuisine_types ct
                WHERE r.cuisine_type_id = ct.cuisine_type_id
            ");

            migrationBuilder.DropForeignKey(
                name: "fk_restaurants_cuisine_types_cuisine_type_id",
                table: "restaurants");

            migrationBuilder.DropIndex(
                name: "ix_restaurants_cuisine_type_id",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "cuisine_type_id",
                table: "restaurants");

            migrationBuilder.DeleteData(
                table: "cuisine_types",
                keyColumn: "name",
                keyValues: new object[]
                {
                    "polska", "wloska", "francuska", "hiszpanska", "grecka", "portugalska",
                    "japonska", "chinska", "koreanska", "wietnamska", "tajska", "indyjska",
                    "amerykanska", "meksykanska", "brazylijska",
                    "turecka", "libanska", "marokanska", "izraelska",
                    "niemiecka", "austriacka", "wegierska", "czeska", "ukrainska", "rosyjska",
                    "wegetarianska", "weganska", "fusion", "street_food", "fast_food", "kawiarnia"
                });

            migrationBuilder.CreateIndex(
                name: "ix_restaurants_cuisine_type",
                table: "restaurants",
                column: "cuisine_type");
        }
    }
}
