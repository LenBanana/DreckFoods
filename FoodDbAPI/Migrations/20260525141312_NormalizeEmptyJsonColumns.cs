using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodDbAPI.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeEmptyJsonColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""FddbFoods""
                SET ""TagsJson"" = '[]'
                WHERE COALESCE(BTRIM(""TagsJson""), '') = '';");

            migrationBuilder.Sql(@"
                UPDATE ""FddbFoods""
                SET ""ServingsJson"" = '[]'
                WHERE COALESCE(BTRIM(""ServingsJson""), '') = '';");

            migrationBuilder.Sql(@"
                ALTER TABLE ""FddbFoods""
                ALTER COLUMN ""ServingsJson"" SET DEFAULT '[]';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""FddbFoods""
                ALTER COLUMN ""ServingsJson"" SET DEFAULT '';");
        }
    }
}
