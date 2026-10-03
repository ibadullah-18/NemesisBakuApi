using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NemesisBakuApi.Migrations
{
    /// <inheritdoc />
    public partial class AddTrafficStatisticsPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrafficStatisticsPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrafficStatisticsPeriods", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrafficStatisticsPeriods");
        }
    }
}
