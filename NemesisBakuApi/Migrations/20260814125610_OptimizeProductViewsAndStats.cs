using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NemesisBakuApi.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeProductViewsAndStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WhatsAppClickLogs_ClickType",
                table: "WhatsAppClickLogs");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppClickLogs_ClickType_CreatedAt",
                table: "WhatsAppClickLogs",
                columns: new[] { "ClickType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisits_VisitorId_VisitedAt",
                table: "SiteVisits",
                columns: new[] { "VisitorId", "VisitedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsActive_ViewCount",
                table: "Products",
                columns: new[] { "IsActive", "ViewCount" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WhatsAppClickLogs_ClickType_CreatedAt",
                table: "WhatsAppClickLogs");

            migrationBuilder.DropIndex(
                name: "IX_SiteVisits_VisitorId_VisitedAt",
                table: "SiteVisits");

            migrationBuilder.DropIndex(
                name: "IX_Products_IsActive_ViewCount",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppClickLogs_ClickType",
                table: "WhatsAppClickLogs",
                column: "ClickType");
        }
    }
}
