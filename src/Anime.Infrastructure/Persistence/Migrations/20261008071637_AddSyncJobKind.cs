using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncJobKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "anime_id",
                table: "sync_jobs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "sync_jobs",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "CATALOG");

            migrationBuilder.AddColumn<string>(
                name: "target",
                table: "sync_jobs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "anime_id",
                table: "sync_jobs");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "sync_jobs");

            migrationBuilder.DropColumn(
                name: "target",
                table: "sync_jobs");
        }
    }
}
