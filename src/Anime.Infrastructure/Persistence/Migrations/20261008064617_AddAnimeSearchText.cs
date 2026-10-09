using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnimeSearchText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "search_text",
                table: "anime",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Điền search_text cho các anime đã đồng bộ trước đó.
            migrationBuilder.Sql("""
                UPDATE anime SET search_text = COALESCE(
                    (SELECT string_agg(DISTINCT value, ' | ') FROM jsonb_each_text(titles) WHERE value <> ''), '')
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "search_text",
                table: "anime");
        }
    }
}
