using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NemesisBakuApi.Migrations
{
    /// <inheritdoc />
    public partial class AddShowcaseGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShowcaseGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShowcaseGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShowcaseBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShowcaseGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    Shape = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExternalUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    MobileImageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ImageAlt = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Subtitle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: true),
                    AfterProductsDescription = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShowcaseBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShowcaseBlocks_ShowcaseGroups_ShowcaseGroupId",
                        column: x => x.ShowcaseGroupId,
                        principalTable: "ShowcaseGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShowcaseBlockProducts",
                columns: table => new
                {
                    ShowcaseBlockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShowcaseBlockProducts", x => new { x.ShowcaseBlockId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_ShowcaseBlockProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShowcaseBlockProducts_ShowcaseBlocks_ShowcaseBlockId",
                        column: x => x.ShowcaseBlockId,
                        principalTable: "ShowcaseBlocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShowcaseBlockProducts_ProductId",
                table: "ShowcaseBlockProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ShowcaseBlocks_ShowcaseGroupId",
                table: "ShowcaseBlocks",
                column: "ShowcaseGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ShowcaseBlocks_Slug",
                table: "ShowcaseBlocks",
                column: "Slug",
                unique: true,
                filter: "[Slug] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ShowcaseGroups_DisplayOrder_CreatedAt",
                table: "ShowcaseGroups",
                columns: new[] { "DisplayOrder", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShowcaseBlockProducts");

            migrationBuilder.DropTable(
                name: "ShowcaseBlocks");

            migrationBuilder.DropTable(
                name: "ShowcaseGroups");
        }
    }
}
