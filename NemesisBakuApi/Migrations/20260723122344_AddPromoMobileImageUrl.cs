using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NemesisBakuApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPromoMobileImageUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MobileImageUrl",
                table: "PromoPages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MobileImageUrl",
                table: "PromoPages");
        }
    }
}
