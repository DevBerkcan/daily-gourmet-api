using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyGourmet.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSameDayAdjustmentTrackingAndOrderReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KitchenNotificationEmails",
                table: "TenantSettings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrderReminderHoursBefore",
                table: "TenantSettings",
                type: "int",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.AddColumn<int>(
                name: "OriginalPortions",
                table: "OrderItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SameDayAdjustedAtUtc",
                table: "OrderItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SameDayAdjustmentEnabled",
                table: "Facilities",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "OrderReminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MealPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeadlineAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderReminders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderReminders_FacilityId_MealPlanId",
                table: "OrderReminders",
                columns: new[] { "FacilityId", "MealPlanId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderReminders");

            migrationBuilder.DropColumn(
                name: "KitchenNotificationEmails",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "OrderReminderHoursBefore",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "OriginalPortions",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "SameDayAdjustedAtUtc",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "SameDayAdjustmentEnabled",
                table: "Facilities");
        }
    }
}
