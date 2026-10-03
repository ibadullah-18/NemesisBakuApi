using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NemesisBakuApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPageViewEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EventId",
                table: "SiteVisits",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteVisits_EventId",
                table: "SiteVisits",
                column: "EventId",
                unique: true,
                filter: "[EventId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SiteVisits_EventId",
                table: "SiteVisits");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "SiteVisits");
        }
    }
}
