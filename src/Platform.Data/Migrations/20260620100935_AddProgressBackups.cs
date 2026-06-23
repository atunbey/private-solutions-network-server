using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProgressBackups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProgressBackups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApplicationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProgressJson = table.Column<string>(type: "text", nullable: false),
                    CapturedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgressBackups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgressBackups_NodeId_Username_ApplicationName_CapturedUtc",
                table: "ProgressBackups",
                columns: new[] { "NodeId", "Username", "ApplicationName", "CapturedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProgressBackups");
        }
    }
}
