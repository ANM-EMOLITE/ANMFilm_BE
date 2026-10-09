using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPersistentCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cached_responses",
                columns: table => new
                {
                    key = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cached_responses", x => x.key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cached_responses_expires_at",
                table: "cached_responses",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_cached_responses_kind",
                table: "cached_responses",
                column: "kind");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cached_responses");
        }
    }
}
