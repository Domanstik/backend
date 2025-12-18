using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StarMarathon.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalJwt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalAuthJwt",
                table: "profiles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalAuthJwt",
                table: "profiles");
        }
    }
}
