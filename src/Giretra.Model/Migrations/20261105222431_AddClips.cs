using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Giretra.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddClips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "appear_in_shared_clips",
                table: "players",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "clips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    short_id = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deal_number = table.Column<short>(type: "smallint", nullable: false),
                    from_trick = table.Column<short>(type: "smallint", nullable: false),
                    to_trick = table.Column<short>(type: "smallint", nullable: false),
                    view_position = table.Column<int>(type: "integer", nullable: false),
                    show_all_hands = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    locale = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "en"),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_by_player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    claimed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rendered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_viewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    view_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    duration_ms = table.Column<int>(type: "integer", nullable: true),
                    has_poster = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    has_video = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    error = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clips", x => x.id);
                    table.ForeignKey(
                        name: "fk_clips_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_clips_players_created_by_player_id",
                        column: x => x.created_by_player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_clips_created_by_player_id",
                table: "clips",
                column: "created_by_player_id");

            migrationBuilder.CreateIndex(
                name: "ix_clips_match_id_deal_number_from_trick_to_trick_view_positio",
                table: "clips",
                columns: new[] { "match_id", "deal_number", "from_trick", "to_trick", "view_position", "show_all_hands", "locale" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_clips_short_id",
                table: "clips",
                column: "short_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_clips_status_created_at",
                table: "clips",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clips");

            migrationBuilder.DropColumn(
                name: "appear_in_shared_clips",
                table: "players");
        }
    }
}
