using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyGourmet.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUsernameToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Backfill: derive each existing user's Username from the local part of their Email
            // (lowercased; spaces and '+' — the only realistic email characters outside the
            // [a-z0-9._-] convention — stripped), then de-duplicate collisions with a numeric
            // suffix by insertion order — same convention as
            // UserInvitationHelper.GenerateUniqueUsernameAsync, applied once here for existing rows.
            migrationBuilder.Sql("""
                ;WITH normalized AS (
                    SELECT
                        Id,
                        REPLACE(REPLACE(
                            LOWER(
                                CASE WHEN CHARINDEX('@', Email) > 1
                                    THEN LEFT(Email, CHARINDEX('@', Email) - 1)
                                    ELSE Email
                                END
                            ),
                        ' ', ''), '+', '') AS BaseName
                    FROM Users
                ),
                numbered AS (
                    SELECT Id, CASE WHEN BaseName = '' THEN 'user' ELSE BaseName END AS BaseName,
                        ROW_NUMBER() OVER (PARTITION BY CASE WHEN BaseName = '' THEN 'user' ELSE BaseName END ORDER BY Id) AS rn
                    FROM normalized
                )
                UPDATE u
                SET u.Username = CASE WHEN n.rn = 1 THEN n.BaseName ELSE n.BaseName + CAST(n.rn AS nvarchar(10)) END
                FROM Users u
                JOIN numbered n ON n.Id = u.Id;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }
    }
}
