using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyGourmet.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeReductionFactorAndFullIngredientNutrition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ReductionFactor",
                table: "Recipes",
                type: "decimal(6,3)",
                precision: 6,
                scale: 3,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "Nutrition_AlcoholG",
                table: "Ingredients",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Nutrition_FiberG",
                table: "Ingredients",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Nutrition_Kj",
                table: "Ingredients",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Nutrition_SaturatedFatG",
                table: "Ingredients",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReductionFactor",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Nutrition_AlcoholG",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "Nutrition_FiberG",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "Nutrition_Kj",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "Nutrition_SaturatedFatG",
                table: "Ingredients");
        }
    }
}
