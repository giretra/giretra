using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Giretra.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddBotRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_elo_histories_player_id_recorded_at",
                table: "elo_histories");

            migrationBuilder.AddColumn<bool>(
                name: "involved_bots",
                table: "elo_histories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "rating",
                table: "bots",
                type: "integer",
                nullable: false,
                defaultValue: 1000);

            migrationBuilder.CreateIndex(
                name: "ix_elo_history_bot_gains",
                table: "elo_histories",
                columns: new[] { "player_id", "recorded_at" },
                descending: new[] { false, true },
                filter: "involved_bots = TRUE AND elo_change > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_elo_history_bot_gains",
                table: "elo_histories");

            migrationBuilder.DropColumn(
                name: "involved_bots",
                table: "elo_histories");

            migrationBuilder.DropColumn(
                name: "rating",
                table: "bots");

            migrationBuilder.CreateIndex(
                name: "ix_elo_histories_player_id_recorded_at",
                table: "elo_histories",
                columns: new[] { "player_id", "recorded_at" },
                descending: new[] { false, true });
        }
    }
}
