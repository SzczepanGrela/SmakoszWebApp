using Microsoft.EntityFrameworkCore.Migrations;
using Smakosz.Domain.Constants;

#nullable disable

namespace Smakosz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedDishCategoryTags : Migration
    {
        private static readonly string[] Columns =
        {
            "tag_name",
            "category",
            "target_entity",
            "display_color",
            "created_at"
        };

        private static readonly string[] CategoryNames =
        {
            "Pizza", "Burger", "Kebab", "Makaron", "Sushi",
            "Zupa", "Sałatka", "Deser", "Napój",
            "Śniadanie", "Przystawka", "Kanapka", "Pierogi",
            "Stek", "Ryba", "Kuchnia domowa", "Fast food", "Inne"
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var createdAt = new System.DateTime(2026, 4, 18, 0, 0, 0, System.DateTimeKind.Utc);

            var values = new object[CategoryNames.Length, Columns.Length];
            for (var i = 0; i < CategoryNames.Length; i++)
            {
                values[i, 0] = CategoryNames[i];
                values[i, 1] = TagCategories.DishCategory;
                values[i, 2] = "dish";
                values[i, 3] = "#e67e22";
                values[i, 4] = createdAt;
            }

            migrationBuilder.InsertData(
                table: "tags",
                columns: Columns,
                values: values);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var name in CategoryNames)
            {
                migrationBuilder.DeleteData(
                    table: "tags",
                    keyColumn: "tag_name",
                    keyValue: name);
            }
        }
    }
}
