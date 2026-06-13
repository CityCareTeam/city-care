using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationMessageCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MessageCount",
                table: "notifications",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MessageCount",
                table: "notifications");
        }
    }
}
