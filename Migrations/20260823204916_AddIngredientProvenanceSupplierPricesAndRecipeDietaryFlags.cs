using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyGourmet.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIngredientProvenanceSupplierPricesAndRecipeDietaryFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DgeCertified",
                table: "Recipes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "GlutenFree",
                table: "Recipes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LactoseFree",
                table: "Recipes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ExternalRefId",
                table: "Ingredients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsManuallyEdited",
                table: "Ingredients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedAt",
                table: "Ingredients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "Ingredients",
                type: "int",
                nullable: false,
                // 1 = IngredientSource.Manuell: every pre-existing row predates provenance tracking
                // and was created by staff, not by a sync that doesn't exist yet — defaulting to
                // Rezeptrechner (0) would mislabel them, even though it's harmless for the actual
                // sync-overwrite protection (that matches on ExternalRefId, which is null here).
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "IngredientSupplierPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierArticleNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Unit = table.Column<int>(type: "int", nullable: false),
                    AvailabilityNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientSupplierPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientSupplierPrices_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientSupplierPrices_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSupplierPrices_IngredientId",
                table: "IngredientSupplierPrices",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSupplierPrices_SupplierId",
                table: "IngredientSupplierPrices",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IngredientSupplierPrices");

            migrationBuilder.DropColumn(
                name: "DgeCertified",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "GlutenFree",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "LactoseFree",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ExternalRefId",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "IsManuallyEdited",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Ingredients");
        }
    }
}
