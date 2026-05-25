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
            migrationBuilder.Sql(@"
                ALTER TABLE ""Meals""
                ALTER COLUMN ""Description"" TYPE text;");

            migrationBuilder.Sql(@"
                ALTER TABLE ""FoodEntries""
                ADD COLUMN IF NOT EXISTS ""Caffeine"" double precision NOT NULL DEFAULT 0.0;");

            migrationBuilder.Sql(@"
                ALTER TABLE ""FoodEntries""
                ADD COLUMN IF NOT EXISTS ""Salt"" double precision NOT NULL DEFAULT 0.0;");

            migrationBuilder.Sql(@"
                ALTER TABLE ""FddbFoodNutritions""
                ADD COLUMN IF NOT EXISTS ""CaffeineUnit"" text NOT NULL DEFAULT '';");

            migrationBuilder.Sql(@"
                ALTER TABLE ""FddbFoodNutritions""
                ADD COLUMN IF NOT EXISTS ""CaffeineValue"" double precision NOT NULL DEFAULT 0.0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""FoodEntries""
                DROP COLUMN IF EXISTS ""Caffeine"";");

            migrationBuilder.Sql(@"
                ALTER TABLE ""FoodEntries""
                DROP COLUMN IF EXISTS ""Salt"";");

            migrationBuilder.Sql(@"
                ALTER TABLE ""FddbFoodNutritions""
                DROP COLUMN IF EXISTS ""CaffeineUnit"";");

            migrationBuilder.Sql(@"
                ALTER TABLE ""FddbFoodNutritions""
                DROP COLUMN IF EXISTS ""CaffeineValue"";");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Meals""
                ALTER COLUMN ""Description"" TYPE character varying(1000);");
        }
    }
}
