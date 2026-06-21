using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NemesisBakuApi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniqueIndexFromPromoPageProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PromoPageProducts_PromoPageId_ProductId",
                table: "PromoPageProducts");

            migrationBuilder.CreateIndex(
                name: "IX_PromoPageProducts_PromoPageId",
                table: "PromoPageProducts",
                column: "PromoPageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PromoPageProducts_PromoPageId",
                table: "PromoPageProducts");

            migrationBuilder.CreateIndex(
                name: "IX_PromoPageProducts_PromoPageId_ProductId",
                table: "PromoPageProducts",
                columns: new[] { "PromoPageId", "ProductId" },
                unique: true);
        }
    }
}
