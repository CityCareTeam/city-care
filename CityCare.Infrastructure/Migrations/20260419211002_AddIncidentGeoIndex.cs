using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentGeoIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_incidents_Latitude_Longitude",
                table: "incidents",
                columns: new[] { "Latitude", "Longitude" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_incidents_Latitude_Longitude",
                table: "incidents");
        }
    }
}
