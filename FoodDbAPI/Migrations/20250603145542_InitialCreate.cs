using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FoodDbAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FddbFoods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Brand = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TagsJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FddbFoods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CurrentWeight = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    EmailTokenHash = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FddbFoodNutritions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FddbFoodId = table.Column<int>(type: "integer", nullable: false),
                    KilojoulesValue = table.Column<double>(type: "double precision", nullable: false),
                    KilojoulesUnit = table.Column<string>(type: "text", nullable: false),
                    CaloriesValue = table.Column<double>(type: "double precision", nullable: false),
                    CaloriesUnit = table.Column<string>(type: "text", nullable: false),
                    ProteinValue = table.Column<double>(type: "double precision", nullable: false),
                    ProteinUnit = table.Column<string>(type: "text", nullable: false),
                    FatValue = table.Column<double>(type: "double precision", nullable: false),
                    FatUnit = table.Column<string>(type: "text", nullable: false),
                    CarbohydratesTotalValue = table.Column<double>(type: "double precision", nullable: false),
                    CarbohydratesTotalUnit = table.Column<string>(type: "text", nullable: false),
                    CarbohydratesSugarValue = table.Column<double>(type: "double precision", nullable: false),
                    CarbohydratesSugarUnit = table.Column<string>(type: "text", nullable: false),
                    CarbohydratesPolyolsValue = table.Column<double>(type: "double precision", nullable: false),
                    CarbohydratesPolyolsUnit = table.Column<string>(type: "text", nullable: false),
                    FiberValue = table.Column<double>(type: "double precision", nullable: false),
                    FiberUnit = table.Column<string>(type: "text", nullable: false),
                    SaltValue = table.Column<double>(type: "double precision", nullable: false),
                    SaltUnit = table.Column<string>(type: "text", nullable: false),
                    IronValue = table.Column<double>(type: "double precision", nullable: false),
                    IronUnit = table.Column<string>(type: "text", nullable: false),
                    ZincValue = table.Column<double>(type: "double precision", nullable: false),
                    ZincUnit = table.Column<string>(type: "text", nullable: false),
                    MagnesiumValue = table.Column<double>(type: "double precision", nullable: false),
                    MagnesiumUnit = table.Column<string>(type: "text", nullable: false),
                    ChlorideValue = table.Column<double>(type: "double precision", nullable: false),
                    ChlorideUnit = table.Column<string>(type: "text", nullable: false),
                    ManganeseValue = table.Column<double>(type: "double precision", nullable: false),
                    ManganeseUnit = table.Column<string>(type: "text", nullable: false),
                    SulfurValue = table.Column<double>(type: "double precision", nullable: false),
                    SulfurUnit = table.Column<string>(type: "text", nullable: false),
                    PotassiumValue = table.Column<double>(type: "double precision", nullable: false),
                    PotassiumUnit = table.Column<string>(type: "text", nullable: false),
                    CalciumValue = table.Column<double>(type: "double precision", nullable: false),
                    CalciumUnit = table.Column<string>(type: "text", nullable: false),
                    PhosphorusValue = table.Column<double>(type: "double precision", nullable: false),
                    PhosphorusUnit = table.Column<string>(type: "text", nullable: false),
                    CopperValue = table.Column<double>(type: "double precision", nullable: false),
                    CopperUnit = table.Column<string>(type: "text", nullable: false),
                    FluorideValue = table.Column<double>(type: "double precision", nullable: false),
                    FluorideUnit = table.Column<string>(type: "text", nullable: false),
                    IodineValue = table.Column<double>(type: "double precision", nullable: false),
                    IodineUnit = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FddbFoodNutritions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FddbFoodNutritions_FddbFoods_FddbFoodId",
                        column: x => x.FddbFoodId,
                        principalTable: "FddbFoods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FoodEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    FoodName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FoodUrl = table.Column<string>(type: "text", nullable: true),
                    Brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    GramsConsumed = table.Column<double>(type: "double precision", nullable: false),
                    Calories = table.Column<double>(type: "double precision", nullable: false),
                    Protein = table.Column<double>(type: "double precision", nullable: false),
                    Fat = table.Column<double>(type: "double precision", nullable: false),
                    Carbohydrates = table.Column<double>(type: "double precision", nullable: false),
                    Fiber = table.Column<double>(type: "double precision", nullable: false),
                    Sugar = table.Column<double>(type: "double precision", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FoodEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WeightEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeightEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeightEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FddbFoodNutritions_FddbFoodId",
                table: "FddbFoodNutritions",
                column: "FddbFoodId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FddbFoods_Brand",
                table: "FddbFoods",
                column: "Brand");

            migrationBuilder.CreateIndex(
                name: "IX_FddbFoods_Name",
                table: "FddbFoods",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_FoodEntries_UserId_ConsumedAt",
                table: "FoodEntries",
                columns: new[] { "UserId", "ConsumedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeightEntries_UserId_RecordedAt",
                table: "WeightEntries",
                columns: new[] { "UserId", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FddbFoodNutritions");

            migrationBuilder.DropTable(
                name: "FoodEntries");

            migrationBuilder.DropTable(
                name: "WeightEntries");

            migrationBuilder.DropTable(
                name: "FddbFoods");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
