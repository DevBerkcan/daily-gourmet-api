using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyGourmet.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementSupplierSplitAndApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalToken",
                table: "ProcurementLists",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovalTokenExpiresAt",
                table: "ProcurementLists",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                table: "ProcurementLists",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementLists_SupplierId",
                table: "ProcurementLists",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementLists_Suppliers_SupplierId",
                table: "ProcurementLists",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementLists_Suppliers_SupplierId",
                table: "ProcurementLists");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementLists_SupplierId",
                table: "ProcurementLists");

            migrationBuilder.DropColumn(
                name: "ApprovalToken",
                table: "ProcurementLists");

            migrationBuilder.DropColumn(
                name: "ApprovalTokenExpiresAt",
                table: "ProcurementLists");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "ProcurementLists");
        }
    }
}
