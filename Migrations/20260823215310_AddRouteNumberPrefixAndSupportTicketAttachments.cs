using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyGourmet.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRouteNumberPrefixAndSupportTicketAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RouteNumberPrefix",
                table: "TenantSettings",
                type: "nvarchar(max)",
                nullable: false,
                // EF's scaffolded default ignores the C# property initializer (= "RT") — same class
                // of bug as the earlier DietLine/SameDayAdjustmentDeadlineTime fixes.
                defaultValue: "RT");

            migrationBuilder.CreateTable(
                name: "SupportTicketAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTicketAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportTicketAttachments_SupportTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupportTicketAttachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTicketAttachments_TicketId",
                table: "SupportTicketAttachments",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTicketAttachments_UploadedByUserId",
                table: "SupportTicketAttachments",
                column: "UploadedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupportTicketAttachments");

            migrationBuilder.DropColumn(
                name: "RouteNumberPrefix",
                table: "TenantSettings");
        }
    }
}
