using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDisplayNameAndAuthorName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthorName",
                table: "incident_messages",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "users");

            migrationBuilder.DropColumn(
                name: "AuthorName",
                table: "incident_messages");
        }
    }
}
