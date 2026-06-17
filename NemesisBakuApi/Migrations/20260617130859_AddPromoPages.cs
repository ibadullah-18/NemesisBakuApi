using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NemesisBakuApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPromoPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PromoPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    SlotNumber = table.Column<int>(type: "int", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoPages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromoPageProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromoPageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoPageProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromoPageProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PromoPageProducts_PromoPages_PromoPageId",
                        column: x => x.PromoPageId,
                        principalTable: "PromoPages",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PromoPageProducts_ProductId",
                table: "PromoPageProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoPageProducts_PromoPageId_ProductId",
                table: "PromoPageProducts",
                columns: new[] { "PromoPageId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromoPages_Type_SlotNumber",
                table: "PromoPages",
                columns: new[] { "Type", "SlotNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromoPageProducts");

            migrationBuilder.DropTable(
                name: "PromoPages");
        }
    }
}
