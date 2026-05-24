using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodDbAPI.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMealDescriptionLengthLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Meals",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Caffeine",
                table: "FoodEntries",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Salt",
                table: "FoodEntries",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "CaffeineUnit",
                table: "FddbFoodNutritions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "CaffeineValue",
                table: "FddbFoodNutritions",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Caffeine",
                table: "FoodEntries");

            migrationBuilder.DropColumn(
                name: "Salt",
                table: "FoodEntries");

            migrationBuilder.DropColumn(
                name: "CaffeineUnit",
                table: "FddbFoodNutritions");

            migrationBuilder.DropColumn(
                name: "CaffeineValue",
                table: "FddbFoodNutritions");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Meals",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
