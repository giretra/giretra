using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Giretra.Model.Migrations
{
    /// <inheritdoc />
    public partial class DropMatchPlayerUniquePlayerIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_match_players_match_id_player_id",
                table: "match_players");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_match_players_match_id_player_id",
                table: "match_players",
                columns: new[] { "match_id", "player_id" },
                unique: true);
        }
    }
}
