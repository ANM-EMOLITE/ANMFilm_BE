using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Anime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersAndWatchHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_activity_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    target_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    target_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_activity_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    is_banned = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "watch_history",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    anime_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cover = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    episode = table.Column<int>(type: "integer", nullable: true),
                    provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    count = table.Column<int>(type: "integer", nullable: false),
                    watched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_watch_history", x => new { x.user_id, x.anime_id });
                    table.ForeignKey(
                        name: "FK_watch_history_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "watchlist",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    anime_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cover = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_watchlist", x => new { x.user_id, x.anime_id });
                    table.ForeignKey(
                        name: "FK_watchlist_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_activity_logs_action",
                table: "user_activity_logs",
                column: "action");

            migrationBuilder.CreateIndex(
                name: "IX_user_activity_logs_user_id_created_at",
                table: "user_activity_logs",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_watch_history_user_id_watched_at",
                table: "watch_history",
                columns: new[] { "user_id", "watched_at" });

            migrationBuilder.CreateIndex(
                name: "IX_watchlist_user_id_added_at",
                table: "watchlist",
                columns: new[] { "user_id", "added_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_activity_logs");

            migrationBuilder.DropTable(
                name: "watch_history");

            migrationBuilder.DropTable(
                name: "watchlist");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
