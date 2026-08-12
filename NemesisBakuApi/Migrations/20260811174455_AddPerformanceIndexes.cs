using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NemesisBakuApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_BrandId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_CategoryId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Orders_UserId",
                table: "Orders");

            migrationBuilder.AlterColumn<string>(
                name: "ClickType",
                table: "WhatsAppClickLogs",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "UserOtpCodes",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VisitorId",
                table: "SiteVisits",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppClickLogs_ClickType",
                table: "WhatsAppClickLogs",
                column: "ClickType");

            migrationBuilder.CreateIndex(
                name: "IX_UserOtpCodes_Email_Purpose_IsUsed_ExpiresAt",
                table: "UserOtpCodes",
                columns: new[] { "Email", "Purpose", "IsUsed", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisits_VisitedAt",
                table: "SiteVisits",
                column: "VisitedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisits_VisitorId",
                table: "SiteVisits",
                column: "VisitorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId_IsActive_StockCount",
                table: "ProductVariants",
                columns: new[] { "ProductId", "IsActive", "StockCount" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_BrandId_IsActive",
                table: "Products",
                columns: new[] { "BrandId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId_IsActive",
                table: "Products",
                columns: new[] { "CategoryId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsActive_CreatedAt",
                table: "Products",
                columns: new[] { "IsActive", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_CreatedAt",
                table: "Orders",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId_CreatedAt",
                table: "Orders",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WhatsAppClickLogs_ClickType",
                table: "WhatsAppClickLogs");

            migrationBuilder.DropIndex(
                name: "IX_UserOtpCodes_Email_Purpose_IsUsed_ExpiresAt",
                table: "UserOtpCodes");

            migrationBuilder.DropIndex(
                name: "IX_SiteVisits_VisitedAt",
                table: "SiteVisits");

            migrationBuilder.DropIndex(
                name: "IX_SiteVisits_VisitorId",
                table: "SiteVisits");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_ProductId_IsActive_StockCount",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_Products_BrandId_IsActive",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_CategoryId_IsActive",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_IsActive_CreatedAt",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Status_CreatedAt",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_UserId_CreatedAt",
                table: "Orders");

            migrationBuilder.AlterColumn<string>(
                name: "ClickType",
                table: "WhatsAppClickLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "UserOtpCodes",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VisitorId",
                table: "SiteVisits",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_Products_BrandId",
                table: "Products",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId",
                table: "Orders",
                column: "UserId");
        }
    }
}
