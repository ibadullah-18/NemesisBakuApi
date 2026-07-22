using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NemesisBakuApi.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramNotificationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TelegramChatId",
                table: "AspNetUsers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TelegramLinkedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TelegramNotificationsEnabled",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TelegramUsername",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TelegramOrderNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TelegramChatId = table.Column<long>(type: "bigint", nullable: false),
                    AdminFullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PanelRole = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramOrderNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelegramOrderNotifications_AspNetUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TelegramOrderNotifications_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_TelegramChatId",
                table: "AspNetUsers",
                column: "TelegramChatId",
                unique: true,
                filter: "[TelegramChatId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TelegramOrderNotifications_AdminUserId",
                table: "TelegramOrderNotifications",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TelegramOrderNotifications_OrderId_AdminUserId",
                table: "TelegramOrderNotifications",
                columns: new[] { "OrderId", "AdminUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelegramOrderNotifications_SentAt_NextAttemptAt_AttemptCount",
                table: "TelegramOrderNotifications",
                columns: new[] { "SentAt", "NextAttemptAt", "AttemptCount" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelegramOrderNotifications");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_TelegramChatId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TelegramChatId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TelegramLinkedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TelegramNotificationsEnabled",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TelegramUsername",
                table: "AspNetUsers");
        }
    }
}
