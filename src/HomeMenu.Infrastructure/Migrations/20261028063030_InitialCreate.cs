using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeMenu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    CaloriesPer100gScaled = table.Column<long>(type: "INTEGER", nullable: false),
                    ProteinPer100gScaled = table.Column<long>(type: "INTEGER", nullable: false),
                    FatPer100gScaled = table.Column<long>(type: "INTEGER", nullable: false),
                    CarbsPer100gScaled = table.Column<long>(type: "INTEGER", nullable: false),
                    PricePer100gScaled = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.CheckConstraint("CK_Product_Values", "CaloriesPer100gScaled BETWEEN 0 AND 100000 AND ProteinPer100gScaled BETWEEN 0 AND 10000 AND FatPer100gScaled BETWEEN 0 AND 10000 AND CarbsPer100gScaled BETWEEN 0 AND 10000 AND ProteinPer100gScaled + FatPer100gScaled + CarbsPer100gScaled <= 10000 AND PricePer100gScaled BETWEEN 0 AND 100000000");
                });

            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    MealType = table.Column<int>(type: "INTEGER", nullable: false),
                    DishCategory = table.Column<string>(type: "TEXT", nullable: true),
                    Instructions = table.Column<string>(type: "TEXT", nullable: false),
                    CookingMinutes = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                    table.CheckConstraint("CK_Recipe_Values", "MealType IN (0,1,2) AND CookingMinutes BETWEEN 1 AND 1440");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    Login = table.Column<string>(type: "TEXT", nullable: false),
                    NormalizedLogin = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    Sex = table.Column<int>(type: "INTEGER", nullable: true),
                    Age = table.Column<int>(type: "INTEGER", nullable: true),
                    HeightCmScaled = table.Column<long>(type: "INTEGER", nullable: true),
                    WeightKgScaled = table.Column<long>(type: "INTEGER", nullable: true),
                    ActivityLevel = table.Column<int>(type: "INTEGER", nullable: true),
                    WeeklyBudgetScaled = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_User_Profile", "(Sex IS NULL OR Sex IN (0,1)) AND (Age IS NULL OR Age BETWEEN 18 AND 100) AND (HeightCmScaled IS NULL OR HeightCmScaled BETWEEN 10000 AND 25000) AND (WeightKgScaled IS NULL OR WeightKgScaled BETWEEN 3000 AND 30000) AND (ActivityLevel IS NULL OR ActivityLevel BETWEEN 0 AND 4) AND WeeklyBudgetScaled BETWEEN 0 AND 10000000000");
                });

            migrationBuilder.CreateTable(
                name: "Ingredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RecipeId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    QuantityGramsScaled = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredients", x => x.Id);
                    table.CheckConstraint("CK_Ingredient_Grams", "QuantityGramsScaled BETWEEN 1 AND 10000000");
                    table.ForeignKey(
                        name: "FK_Ingredients_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ingredients_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MenuPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DailyCalorieTargetScaled = table.Column<long>(type: "INTEGER", nullable: false),
                    ProteinTargetGramsScaled = table.Column<long>(type: "INTEGER", nullable: false),
                    FatTargetGramsScaled = table.Column<long>(type: "INTEGER", nullable: false),
                    CarbsTargetGramsScaled = table.Column<long>(type: "INTEGER", nullable: false),
                    BudgetScaled = table.Column<long>(type: "INTEGER", nullable: false),
                    ExcludedProductIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuPlans", x => x.Id);
                    table.CheckConstraint("CK_Plan_Values", "DailyCalorieTargetScaled > 0 AND ProteinTargetGramsScaled >= 0 AND FatTargetGramsScaled >= 0 AND CarbsTargetGramsScaled >= 0 AND BudgetScaled BETWEEN 0 AND 10000000000 AND Version > 0");
                    table.ForeignKey(
                        name: "FK_MenuPlans_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PantryStocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    QuantityGramsScaled = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PantryStocks", x => x.Id);
                    table.CheckConstraint("CK_Stock_Grams", "QuantityGramsScaled BETWEEN 0 AND 1000000000");
                    table.ForeignKey(
                        name: "FK_PantryStocks_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PantryStocks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserExcludedProducts",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserExcludedProducts", x => new { x.UserId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_UserExcludedProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserExcludedProducts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyMenus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MenuPlanId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    MealType = table.Column<int>(type: "INTEGER", nullable: false),
                    RecipeId = table.Column<int>(type: "INTEGER", nullable: false),
                    RecipeNameSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    InstructionsSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    CookedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyMenus", x => x.Id);
                    table.CheckConstraint("CK_Menu_Meal", "MealType IN (0,1,2)");
                    table.ForeignKey(
                        name: "FK_WeeklyMenus_MenuPlans_MenuPlanId",
                        column: x => x.MenuPlanId,
                        principalTable: "MenuPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WeeklyMenus_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MenuIngredientSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WeeklyMenuId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductNameSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    CategorySnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    QuantityGramsScaled = table.Column<long>(type: "INTEGER", nullable: false),
                    CaloriesPer100gScaled = table.Column<long>(type: "INTEGER", nullable: false),
                    ProteinPer100gScaled = table.Column<long>(type: "INTEGER", nullable: false),
                    FatPer100gScaled = table.Column<long>(type: "INTEGER", nullable: false),
                    CarbsPer100gScaled = table.Column<long>(type: "INTEGER", nullable: false),
                    PricePer100gScaled = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuIngredientSnapshots", x => x.Id);
                    table.CheckConstraint("CK_Snapshot_Values", "CaloriesPer100gScaled BETWEEN 0 AND 100000 AND ProteinPer100gScaled BETWEEN 0 AND 10000 AND FatPer100gScaled BETWEEN 0 AND 10000 AND CarbsPer100gScaled BETWEEN 0 AND 10000 AND ProteinPer100gScaled + FatPer100gScaled + CarbsPer100gScaled <= 10000 AND QuantityGramsScaled BETWEEN 1 AND 10000000 AND PricePer100gScaled BETWEEN 0 AND 100000000");
                    table.ForeignKey(
                        name: "FK_MenuIngredientSnapshots_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuIngredientSnapshots_WeeklyMenus_WeeklyMenuId",
                        column: x => x.WeeklyMenuId,
                        principalTable: "WeeklyMenus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_ProductId",
                table: "Ingredients",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_RecipeId_ProductId",
                table: "Ingredients",
                columns: new[] { "RecipeId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuIngredientSnapshots_ProductId",
                table: "MenuIngredientSnapshots",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuIngredientSnapshots_WeeklyMenuId_ProductId",
                table: "MenuIngredientSnapshots",
                columns: new[] { "WeeklyMenuId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuPlans_UserId_WeekStart",
                table: "MenuPlans",
                columns: new[] { "UserId", "WeekStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PantryStocks_ProductId",
                table: "PantryStocks",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PantryStocks_UserId_ProductId_ExpiryDate",
                table: "PantryStocks",
                columns: new[] { "UserId", "ProductId", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_NormalizedName",
                table: "Products",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserExcludedProducts_ProductId",
                table: "UserExcludedProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedLogin",
                table: "Users",
                column: "NormalizedLogin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyMenus_MenuPlanId_Date_MealType",
                table: "WeeklyMenus",
                columns: new[] { "MenuPlanId", "Date", "MealType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyMenus_RecipeId",
                table: "WeeklyMenus",
                column: "RecipeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ingredients");

            migrationBuilder.DropTable(
                name: "MenuIngredientSnapshots");

            migrationBuilder.DropTable(
                name: "PantryStocks");

            migrationBuilder.DropTable(
                name: "UserExcludedProducts");

            migrationBuilder.DropTable(
                name: "WeeklyMenus");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "MenuPlans");

            migrationBuilder.DropTable(
                name: "Recipes");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
