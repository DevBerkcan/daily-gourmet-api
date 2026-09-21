using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyGourmet.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFacilityDeliveryDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeliveryDurationMinutes",
                table: "Facilities",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryRequirements",
                table: "Facilities",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "DeliveryWindowEnd",
                table: "Facilities",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "DeliveryWindowStart",
                table: "Facilities",
                type: "time",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryDurationMinutes",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "DeliveryRequirements",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "DeliveryWindowEnd",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "DeliveryWindowStart",
                table: "Facilities");
        }
    }
}
