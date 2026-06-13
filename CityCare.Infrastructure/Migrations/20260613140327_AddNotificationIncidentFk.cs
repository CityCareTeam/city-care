using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationIncidentFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_notifications_IncidentId",
                table: "notifications",
                column: "IncidentId");

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_incidents_IncidentId",
                table: "notifications",
                column: "IncidentId",
                principalTable: "incidents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_notifications_incidents_IncidentId",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_notifications_IncidentId",
                table: "notifications");
        }
    }
}
