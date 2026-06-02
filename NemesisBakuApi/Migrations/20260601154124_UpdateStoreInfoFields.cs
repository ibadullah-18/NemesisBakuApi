using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NemesisBakuApi.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStoreInfoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BasketItems_Products_ProductId1",
                table: "BasketItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariants_Products_ProductId",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_BasketItems_ProductId1",
                table: "BasketItems");

            migrationBuilder.DropColumn(
                name: "ProductId1",
                table: "BasketItems");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "StoreInfos",
                newName: "WorkingHours");

            migrationBuilder.AddColumn<string>(
                name: "AboutContent",
                table: "StoreInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AboutTitle",
                table: "StoreInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryAbsheronSumgaitText",
                table: "StoreInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryBakuText",
                table: "StoreInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryContent",
                table: "StoreInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryRegionsText",
                table: "StoreInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryTitle",
                table: "StoreInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExchangePolicyContent",
                table: "StoreInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacebookUrl",
                table: "StoreInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "StoreInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MissionContent",
                table: "StoreInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentAndCheckText",
                table: "StoreInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnExceptionsContent",
                table: "StoreInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnPolicyContent",
                table: "StoreInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnPolicyTitle",
                table: "StoreInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnProcessContent",
                table: "StoreInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slogan",
                table: "StoreInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisionContent",
                table: "StoreInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhyChooseUsContent",
                table: "StoreInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Apartment",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuildingNumber",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryDistanceKm",
                table: "Orders",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Floor",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariants_Products_ProductId",
                table: "ProductVariants",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariants_Products_ProductId",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "AboutContent",
                table: "StoreInfos");

            migrationBuilder.DropColumn(
                name: "AboutTitle",
                table: "StoreInfos");

            migrationBuilder.DropColumn(
                name: "DeliveryAbsheronSumgaitText",
                table: "StoreInfos");

            migrationBuilder.DropColumn(
                name: "DeliveryBakuText",
                table: "StoreInfos");

            migrationBuilder.DropColumn(
                name: "DeliveryContent",
                table: "StoreInfos");

            migrationBuilder.DropColumn(
                name: "DeliveryRegionsText",
                table: "StoreInfos");

            migrationBuilder.DropColumn(
                name: "DeliveryTitle",
                table: "StoreInfos");

            migrationBuilder.DropColumn(
                name: "ExchangePolicyContent",
                table: "StoreInfos");

            migrationBuilder.DropColumn(
                name: "FacebookUrl",
                table: "StoreInfos");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "StoreInfos");

            migrationBuilder.DropColumn(
                name: "MissionContent",
                table: "StoreInfos");

            migrationBuilder.DropColumn(
                name: "PaymentAndCheckText",
                table: "StoreInfos");

            migrationBuilder.DropColumn(
                name: "ReturnExceptionsContent",
                table: "StoreInfos");

            migrationBuilder.DropColumn(
                name: "ReturnPolicyContent",
                table: "StoreInfos");

            migrationBuilder.DropColumn(
                name: "ReturnPolicyTitle",
                table: "StoreInfos");

            migrationBuilder.DropColumn(
                name: "ReturnProcessContent",
                table: "StoreInfos");

            migrationBuilder.DropColumn(
                name: "Slogan",
                table: "StoreInfos");

            migrationBuilder.DropColumn(
                name: "VisionContent",
                table: "StoreInfos");

            migrationBuilder.DropColumn(
                name: "WhyChooseUsContent",
                table: "StoreInfos");

            migrationBuilder.DropColumn(
                name: "Apartment",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BuildingNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryDistanceKm",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Floor",
                table: "Orders");

            migrationBuilder.RenameColumn(
                name: "WorkingHours",
                table: "StoreInfos",
                newName: "Description");

            migrationBuilder.AddColumn<Guid>(
                name: "ProductId1",
                table: "BasketItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BasketItems_ProductId1",
                table: "BasketItems",
                column: "ProductId1");

            migrationBuilder.AddForeignKey(
                name: "FK_BasketItems_Products_ProductId1",
                table: "BasketItems",
                column: "ProductId1",
                principalTable: "Products",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariants_Products_ProductId",
                table: "ProductVariants",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
