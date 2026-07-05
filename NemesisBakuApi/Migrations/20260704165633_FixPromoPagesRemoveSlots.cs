using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NemesisBakuApi.Migrations
{
    /// <inheritdoc />
    public partial class FixPromoPagesRemoveSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PromoPages_Type_SlotNumber",
                table: "PromoPages");

            migrationBuilder.DropColumn(
                name: "SlotNumber",
                table: "PromoPages");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "PromoPages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SlotNumber",
                table: "PromoPages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "PromoPages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PromoPages_Type_SlotNumber",
                table: "PromoPages",
                columns: new[] { "Type", "SlotNumber" },
                unique: true);
        }
    }
}
