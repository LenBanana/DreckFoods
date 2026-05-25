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
            migrationBuilder.Sql(@"
                ALTER TABLE ""FddbFoods""
                ADD COLUMN IF NOT EXISTS ""ServingsJson"" character varying(4000) NOT NULL DEFAULT '[]';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""FddbFoods""
                DROP COLUMN IF EXISTS ""ServingsJson"";");
        }
    }
}
