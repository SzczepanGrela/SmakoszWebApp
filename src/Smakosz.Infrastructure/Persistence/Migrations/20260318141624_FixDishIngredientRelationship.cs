using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smakosz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixDishIngredientRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_dish_ingredients_dishes_dish_id1",
                table: "dish_ingredients");

            migrationBuilder.DropIndex(
                name: "ix_dish_ingredients_dish_id1",
                table: "dish_ingredients");

            migrationBuilder.DropColumn(
                name: "dish_id1",
                table: "dish_ingredients");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "dish_id1",
                table: "dish_ingredients",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_dish_ingredients_dish_id1",
                table: "dish_ingredients",
                column: "dish_id1");

            migrationBuilder.AddForeignKey(
                name: "fk_dish_ingredients_dishes_dish_id1",
                table: "dish_ingredients",
                column: "dish_id1",
                principalTable: "dishes",
                principalColumn: "dish_id");
        }
    }
}
