using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Giretra.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddBotAgentTypeFactory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "agent_type_factory",
                table: "bots",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "agent_type_factory",
                table: "bots");
        }
    }
}
