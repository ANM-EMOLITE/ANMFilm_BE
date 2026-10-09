using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Anime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogAndAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "anime",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    display_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    titles = table.Column<string>(type: "jsonb", nullable: false),
                    images = table.Column<string>(type: "jsonb", nullable: false),
                    format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    season = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    season_year = table.Column<int>(type: "integer", nullable: true),
                    start_date = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    total_units = table.Column<int>(type: "integer", nullable: true),
                    providers = table.Column<List<string>>(type: "text[]", nullable: false),
                    has_stream = table.Column<bool>(type: "boolean", nullable: false),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false),
                    synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anime", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "anime_lists",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    item_count = table.Column<int>(type: "integer", nullable: false),
                    synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anime_lists", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "sync_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    lists = table.Column<List<string>>(type: "text[]", nullable: false),
                    progress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    update_existing = table.Column<bool>(type: "boolean", nullable: false),
                    processed = table.Column<int>(type: "integer", nullable: false),
                    added = table.Column<int>(type: "integer", nullable: false),
                    updated = table.Column<int>(type: "integer", nullable: false),
                    skipped = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "text", nullable: true),
                    triggered_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "anime_list_items",
                columns: table => new
                {
                    list_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    anime_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anime_list_items", x => new { x.list_key, x.position });
                    table.ForeignKey(
                        name: "FK_anime_list_items_anime_anime_id",
                        column: x => x.anime_id,
                        principalTable: "anime",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_users_username",
                table: "admin_users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_anime_display_title",
                table: "anime",
                column: "display_title");

            migrationBuilder.CreateIndex(
                name: "IX_anime_has_stream_is_hidden",
                table: "anime",
                columns: new[] { "has_stream", "is_hidden" });

            migrationBuilder.CreateIndex(
                name: "IX_anime_list_items_anime_id",
                table: "anime_list_items",
                column: "anime_id");

            migrationBuilder.CreateIndex(
                name: "IX_sync_jobs_created_at",
                table: "sync_jobs",
                column: "created_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_users");

            migrationBuilder.DropTable(
                name: "anime_list_items");

            migrationBuilder.DropTable(
                name: "anime_lists");

            migrationBuilder.DropTable(
                name: "sync_jobs");

            migrationBuilder.DropTable(
                name: "anime");
        }
    }
}
