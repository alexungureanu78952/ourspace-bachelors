using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OurSpace.Bff.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ServerSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BffSessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Payload = table.Column<byte[]>(type: "bytea", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BffSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BffSessions_ExpiresAt",
                table: "BffSessions",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BffSessions");
        }
    }
}
