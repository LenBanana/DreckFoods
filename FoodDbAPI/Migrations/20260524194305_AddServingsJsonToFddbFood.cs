using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodDbAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddServingsJsonToFddbFood : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ServingsJson",
                table: "FddbFoods",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServingsJson",
                table: "FddbFoods");
        }
    }
}
