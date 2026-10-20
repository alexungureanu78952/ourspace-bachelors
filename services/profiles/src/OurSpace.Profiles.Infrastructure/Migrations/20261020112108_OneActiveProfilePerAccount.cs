using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OurSpace.Profiles.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OneActiveProfilePerAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Profiles_OwnerId",
                table: "Profiles");

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Preserve all historical rows; keep the oldest profile active for each owner.
            // The Id tie-breaker makes equal creation timestamps deterministic.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id", row_number() OVER (
                        PARTITION BY "OwnerId" ORDER BY "CreatedAt", "Id") AS position
                    FROM "Profiles"
                )
                UPDATE "Profiles" AS profile SET "IsArchived" = TRUE
                FROM ranked WHERE profile."Id" = ranked."Id" AND ranked.position > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_OwnerId",
                table: "Profiles",
                column: "OwnerId",
                unique: true,
                filter: "NOT \"IsArchived\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Profiles_OwnerId",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Profiles");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_OwnerId",
                table: "Profiles",
                column: "OwnerId");
        }
    }
}
