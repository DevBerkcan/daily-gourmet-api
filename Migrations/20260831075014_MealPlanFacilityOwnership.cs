using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyGourmet.Api.Migrations
{
    /// <inheritdoc />
    public partial class MealPlanFacilityOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Order matters here: the column must exist and be backfilled from MealPlanFacilities
            // BEFORE that table is dropped — EF's default scaffolded order does the drop first,
            // which would destroy the source data before it could be copied. Reordered by hand.
            migrationBuilder.AddColumn<Guid>(
                name: "FacilityId",
                table: "MealPlans",
                type: "uniqueidentifier",
                nullable: true);

            // Only backfills plans with exactly one associated facility (deterministic, no guessing
            // which one to keep). A plan with zero or more than one — none currently exist locally,
            // but this can't be verified for any other environment — is left with FacilityId NULL,
            // which then makes the CK_MealPlans_FacilityRequiredUnlessTemplate constraint below fail
            // loudly for that row instead of silently picking a wrong facility. If this migration
            // fails on CK_MealPlans_FacilityRequiredUnlessTemplate, find the offending plan(s) via:
            //   SELECT mp.Id, mp.Year, mp.CalendarWeek, mp.IsTemplate, COUNT(mf.FacilityId) AS FacilityCount
            //   FROM MealPlans mp LEFT JOIN MealPlanFacilities mf ON mf.MealPlanId = mp.Id
            //   WHERE mp.IsTemplate = 0
            //   GROUP BY mp.Id, mp.Year, mp.CalendarWeek, mp.IsTemplate HAVING COUNT(mf.FacilityId) <> 1;
            // and resolve them manually (assign a facility, or mark as template/archive) before retrying.
            migrationBuilder.Sql("""
                UPDATE mp
                SET mp.FacilityId = (SELECT MIN(mf.FacilityId) FROM MealPlanFacilities mf WHERE mf.MealPlanId = mp.Id)
                FROM MealPlans mp
                WHERE (SELECT COUNT(*) FROM MealPlanFacilities mf WHERE mf.MealPlanId = mp.Id) = 1;
                """);

            migrationBuilder.DropTable(
                name: "MealPlanFacilities");

            migrationBuilder.DropIndex(
                name: "IX_MealPlans_TenantId_Year_CalendarWeek",
                table: "MealPlans");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateTable(
                name: "MealPlanFacilities",
                columns: table => new
                {
                    MealPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
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
                name: "IX_MealPlans_TenantId_Year_CalendarWeek",
                table: "MealPlans",
                columns: new[] { "TenantId", "Year", "CalendarWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanFacilities_FacilityId",
                table: "MealPlanFacilities",
                column: "FacilityId");
        }
    }
}
