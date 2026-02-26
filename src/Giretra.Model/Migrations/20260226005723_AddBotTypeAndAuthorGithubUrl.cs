using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Giretra.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddBotTypeAndAuthorGithubUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "author_github_url",
                table: "bots",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bot_type",
                table: "bots",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "deterministic");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "author_github_url",
                table: "bots");

            migrationBuilder.DropColumn(
                name: "bot_type",
                table: "bots");
        }
    }
}
