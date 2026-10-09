using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderHealth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "auto_hidden",
                table: "anime",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "anime_provider_status",
                columns: table => new
                {
                    anime_id = table.Column<int>(type: "integer", nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_disabled = table.Column<bool>(type: "boolean", nullable: false),
                    disabled_by = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    last_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anime_provider_status", x => new { x.anime_id, x.provider });
                    table.ForeignKey(
                        name: "FK_anime_provider_status_anime_anime_id",
                        column: x => x.anime_id,
                        principalTable: "anime",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_anime_provider_status_status",
                table: "anime_provider_status",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anime_provider_status");

            migrationBuilder.DropColumn(
                name: "auto_hidden",
                table: "anime");
        }
    }
}
