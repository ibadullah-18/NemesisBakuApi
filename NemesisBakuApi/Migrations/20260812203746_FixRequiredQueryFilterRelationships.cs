using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NemesisBakuApi.Migrations
{
    /// <inheritdoc />
    public partial class FixRequiredQueryFilterRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderStatusHistories_Orders_OrderId",
                table: "OrderStatusHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_PromoCodeUsages_AspNetUsers_UserId",
                table: "PromoCodeUsages");

            migrationBuilder.DropForeignKey(
                name: "FK_PromoCodeUsages_PromoCodes_PromoCodeId",
                table: "PromoCodeUsages");

            migrationBuilder.DropForeignKey(
                name: "FK_WhatsAppProductInquiries_Products_ProductId",
                table: "WhatsAppProductInquiries");

            migrationBuilder.DropIndex(
                name: "IX_WhatsAppProductInquiries_ProductId",
                table: "WhatsAppProductInquiries");

            migrationBuilder.DropIndex(
                name: "IX_PromoCodeUsages_PromoCodeId",
                table: "PromoCodeUsages");

            migrationBuilder.DropIndex(
                name: "IX_PromoCodeUsages_UserId",
                table: "PromoCodeUsages");

            migrationBuilder.DropIndex(
                name: "IX_OrderStatusHistories_OrderId",
                table: "OrderStatusHistories");

            migrationBuilder.AlterColumn<string>(
                name: "UserAgent",
                table: "WhatsAppProductInquiries",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SellerPhoneNumber",
                table: "WhatsAppProductInquiries",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ProductLink",
                table: "WhatsAppProductInquiries",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "WhatsAppProductInquiries",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppProductInquiries_ProductId_CreatedAt",
                table: "WhatsAppProductInquiries",
                columns: new[] { "ProductId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodeUsages_PromoCodeId_CreatedAt",
                table: "PromoCodeUsages",
                columns: new[] { "PromoCodeId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodeUsages_UserId_CreatedAt",
                table: "PromoCodeUsages",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistories_OrderId_CreatedAt",
                table: "OrderStatusHistories",
                columns: new[] { "OrderId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_OrderStatusHistories_Orders_OrderId",
                table: "OrderStatusHistories",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PromoCodeUsages_AspNetUsers_UserId",
                table: "PromoCodeUsages",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PromoCodeUsages_PromoCodes_PromoCodeId",
                table: "PromoCodeUsages",
                column: "PromoCodeId",
                principalTable: "PromoCodes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WhatsAppProductInquiries_Products_ProductId",
                table: "WhatsAppProductInquiries",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderStatusHistories_Orders_OrderId",
                table: "OrderStatusHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_PromoCodeUsages_AspNetUsers_UserId",
                table: "PromoCodeUsages");

            migrationBuilder.DropForeignKey(
                name: "FK_PromoCodeUsages_PromoCodes_PromoCodeId",
                table: "PromoCodeUsages");

            migrationBuilder.DropForeignKey(
                name: "FK_WhatsAppProductInquiries_Products_ProductId",
                table: "WhatsAppProductInquiries");

            migrationBuilder.DropIndex(
                name: "IX_WhatsAppProductInquiries_ProductId_CreatedAt",
                table: "WhatsAppProductInquiries");

            migrationBuilder.DropIndex(
                name: "IX_PromoCodeUsages_PromoCodeId_CreatedAt",
                table: "PromoCodeUsages");

            migrationBuilder.DropIndex(
                name: "IX_PromoCodeUsages_UserId_CreatedAt",
                table: "PromoCodeUsages");

            migrationBuilder.DropIndex(
                name: "IX_OrderStatusHistories_OrderId_CreatedAt",
                table: "OrderStatusHistories");

            migrationBuilder.AlterColumn<string>(
                name: "UserAgent",
                table: "WhatsAppProductInquiries",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SellerPhoneNumber",
                table: "WhatsAppProductInquiries",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "ProductLink",
                table: "WhatsAppProductInquiries",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "WhatsAppProductInquiries",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppProductInquiries_ProductId",
                table: "WhatsAppProductInquiries",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodeUsages_PromoCodeId",
                table: "PromoCodeUsages",
                column: "PromoCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodeUsages_UserId",
                table: "PromoCodeUsages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistories_OrderId",
                table: "OrderStatusHistories",
                column: "OrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderStatusHistories_Orders_OrderId",
                table: "OrderStatusHistories",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PromoCodeUsages_AspNetUsers_UserId",
                table: "PromoCodeUsages",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PromoCodeUsages_PromoCodes_PromoCodeId",
                table: "PromoCodeUsages",
                column: "PromoCodeId",
                principalTable: "PromoCodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WhatsAppProductInquiries_Products_ProductId",
                table: "WhatsAppProductInquiries",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
