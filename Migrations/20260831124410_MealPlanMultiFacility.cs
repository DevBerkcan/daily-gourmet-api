using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyGourmet.Api.Migrations
{
    /// <inheritdoc />
    public partial class MealPlanMultiFacility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Order matters here: the junction table must exist and be backfilled from
            // MealPlans.FacilityId BEFORE that column is dropped — EF's scaffolded order does the
            // drop first, which would destroy the source data before it could be copied. Reordered
            // by hand, same pattern as the earlier MealPlanFacilityOwnership migration.
            migrationBuilder.CreateTable(
                name: "MealPlanFacilities",
                columns: table => new
                {
                    MealPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    CalendarWeek = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealPlanFacilities", x => new { x.MealPlanId, x.FacilityId });
                    table.ForeignKey(
                        name: "FK_MealPlanFacilities_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MealPlanFacilities_MealPlans_MealPlanId",
                        column: x => x.MealPlanId,
                        principalTable: "MealPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanFacilities_FacilityId",
                table: "MealPlanFacilities",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanFacilities_TenantId_FacilityId_Year_CalendarWeek",
                table: "MealPlanFacilities",
                columns: new[] { "TenantId", "FacilityId", "Year", "CalendarWeek" },
                unique: true);

            // Backfill: every existing plan currently has at most one facility (MealPlans.FacilityId),
            // so this is a straight 1:1 copy into the junction table — no ambiguity, nothing left behind.
            migrationBuilder.Sql("""
                INSERT INTO MealPlanFacilities (MealPlanId, FacilityId, TenantId, Year, CalendarWeek)
                SELECT Id, FacilityId, TenantId, Year, CalendarWeek
                FROM MealPlans
                WHERE FacilityId IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_MealPlans_Facilities_FacilityId",
                table: "MealPlans");

            migrationBuilder.DropIndex(
                name: "IX_MealPlans_FacilityId",
                table: "MealPlans");

            migrationBuilder.DropIndex(
                name: "IX_MealPlans_TenantId_FacilityId_Year_CalendarWeek",
                table: "MealPlans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MealPlans_FacilityRequiredUnlessTemplate",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "FacilityId",
                table: "MealPlans");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MealPlanFacilities");

            migrationBuilder.AddColumn<Guid>(
                name: "FacilityId",
                table: "MealPlans",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealPlans_FacilityId",
                table: "MealPlans",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_MealPlans_TenantId_FacilityId_Year_CalendarWeek",
                table: "MealPlans",
                columns: new[] { "TenantId", "FacilityId", "Year", "CalendarWeek" },
                unique: true,
                filter: "[FacilityId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MealPlans_FacilityRequiredUnlessTemplate",
                table: "MealPlans",
                sql: "[IsTemplate] = 1 OR [FacilityId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_MealPlans_Facilities_FacilityId",
                table: "MealPlans",
                column: "FacilityId",
                principalTable: "Facilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
