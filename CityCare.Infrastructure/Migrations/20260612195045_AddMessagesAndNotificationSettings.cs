using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityCare.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMessagesAndNotificationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "incident_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_incident_messages_incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_incident_messages_users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_notification_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    PushEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    FollowedTypes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_notification_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_notification_settings_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_incident_messages_AuthorUserId",
                table: "incident_messages",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_incident_messages_CreatedAt",
                table: "incident_messages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_incident_messages_IncidentId",
                table: "incident_messages",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_user_notification_settings_UserId",
                table: "user_notification_settings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incident_messages");

            migrationBuilder.DropTable(
                name: "user_notification_settings");
        }
    }
}
