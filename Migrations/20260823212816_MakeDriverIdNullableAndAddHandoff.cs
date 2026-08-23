using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyGourmet.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakeDriverIdNullableAndAddHandoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "DriverId",
                table: "Routes",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<DateTime>(
                name: "HandoffConfirmedAt",
                table: "Routes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HandoffDessertConfirmed",
                table: "Routes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HandoffKaltConfirmed",
                table: "Routes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HandoffWarmConfirmed",
                table: "Routes",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HandoffConfirmedAt",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "HandoffDessertConfirmed",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "HandoffKaltConfirmed",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "HandoffWarmConfirmed",
                table: "Routes");

            migrationBuilder.AlterColumn<Guid>(
                name: "DriverId",
                table: "Routes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
