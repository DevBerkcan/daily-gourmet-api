using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyGourmet.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMealPlanDietLinesTemplatesAndTwoLayerDeadlines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "SameDayAdjustmentDeadlineTime",
                table: "TenantSettings",
                type: "time",
                nullable: false,
                // EF's scaffolded default for a new column ignores the C# property initializer
                // (TenantSettings.SameDayAdjustmentDeadlineTime = new(9,0,0)) and falls back to
                // default(TimeSpan) = midnight — which would make the same-day adjustment window
                // for every pre-existing tenant appear permanently expired. Match the intended 09:00.
                defaultValue: new TimeSpan(9, 0, 0));

            migrationBuilder.AddColumn<bool>(
                name: "IsTemplate",
                table: "MealPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TemplateSlot",
                table: "MealPlans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DietLine",
                table: "MealPlanItems",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "NORMALKOST");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "SameDayAdjustmentDeadlineTime",
                table: "Facilities",
                type: "time",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealPlans_TenantId_TemplateSlot",
                table: "MealPlans",
                columns: new[] { "TenantId", "TemplateSlot" },
                unique: true,
                filter: "[IsTemplate] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MealPlans_TenantId_TemplateSlot",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "SameDayAdjustmentDeadlineTime",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "IsTemplate",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "TemplateSlot",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "DietLine",
                table: "MealPlanItems");

            migrationBuilder.DropColumn(
                name: "SameDayAdjustmentDeadlineTime",
                table: "Facilities");
        }
    }
}
