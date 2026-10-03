using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NemesisBakuApi.Migrations
{
    /// <inheritdoc />
    public partial class AddMetroDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryPricingRule",
                table: "Orders",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MetroDistanceKm",
                table: "Orders",
                type: "decimal(12,4)",
                precision: 12,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MetroStationId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetroStationName",
                table: "Orders",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MetroStations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetroStations", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "MetroStations",
                columns: new[] { "Id", "Address", "CreatedAt", "IsActive", "IsDeleted", "Latitude", "Longitude", "Name", "UpdatedAt", "Version" },
                values: new object[,]
                {
                    { new Guid("c8012026-1003-4000-8000-000000000001"), "Zərdabi prospekti — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.403402m, 49.807583m, "20 Yanvar", null, new Guid("c8012026-1003-4000-8000-000000000001") },
                    { new Guid("c8012026-1003-4000-8000-000000000002"), "Dilarə Əliyeva küçəsi — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.379712m, 49.848843m, "28 May", null, new Guid("c8012026-1003-4000-8000-000000000002") },
                    { new Guid("c8012026-1003-4000-8000-000000000003"), "Ceyhun Səlimov küçəsi — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.402263m, 49.819552m, "8 Noyabr", null, new Guid("c8012026-1003-4000-8000-000000000003") },
                    { new Guid("c8012026-1003-4000-8000-000000000004"), "Bakı Sumqayıt yolu — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.42176m, 49.796061m, "Avtovağzal", null, new Guid("c8012026-1003-4000-8000-000000000004") },
                    { new Guid("c8012026-1003-4000-8000-000000000005"), "Süleyman sani Axundov küçəsi — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.425958m, 49.842833m, "Azadlıq prospekti", null, new Guid("c8012026-1003-4000-8000-000000000005") },
                    { new Guid("c8012026-1003-4000-8000-000000000006"), "Ələsgər Qayıbov küçəsi — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.414152m, 49.879007m, "Bakmil", null, new Guid("c8012026-1003-4000-8000-000000000006") },
                    { new Guid("c8012026-1003-4000-8000-000000000007"), "Dilarə Əliyeva küçəsi — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.379585m, 49.848716m, "Cəfər Cabbarlı", null, new Guid("c8012026-1003-4000-8000-000000000007") },
                    { new Guid("c8012026-1003-4000-8000-000000000008"), "Süleyman sani Axundov küçəsi — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.424955m, 49.860638m, "Dərnəgül", null, new Guid("c8012026-1003-4000-8000-000000000008") },
                    { new Guid("c8012026-1003-4000-8000-000000000009"), "Bəxtiyar Vahabzadə küçəsi — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.375083m, 49.812417m, "Elmlər Akademiyası", null, new Guid("c8012026-1003-4000-8000-000000000009") },
                    { new Guid("c8012026-1003-4000-8000-000000000010"), "Məhəmməd Hadi küçəsi — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.385988m, 49.953697m, "Əhmədli", null, new Guid("c8012026-1003-4000-8000-000000000010") },
                    { new Guid("c8012026-1003-4000-8000-000000000011"), "Fətəli xan Xoyski küçəsi — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.400468m, 49.850587m, "Gənclik", null, new Guid("c8012026-1003-4000-8000-000000000011") },
                    { new Guid("c8012026-1003-4000-8000-000000000012"), "Məhəmməd Hadi küçəsi — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.373863m, 49.953849m, "Həzi Aslanov", null, new Guid("c8012026-1003-4000-8000-000000000012") },
                    { new Guid("c8012026-1003-4000-8000-000000000013"), "İstiqlaliyyət küçəsi — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.365899m, 49.831529m, "İçərişəhər", null, new Guid("c8012026-1003-4000-8000-000000000013") },
                    { new Guid("c8012026-1003-4000-8000-000000000014"), "Şərifzadə küçəsi — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.391379m, 49.802764m, "İnşaatçılar", null, new Guid("c8012026-1003-4000-8000-000000000014") },
                    { new Guid("c8012026-1003-4000-8000-000000000015"), "Heydər Əliyev prospekti — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.419433m, 49.919821m, "Koroğlu", null, new Guid("c8012026-1003-4000-8000-000000000015") },
                    { new Guid("c8012026-1003-4000-8000-000000000016"), "Cavadxan küçəsi — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.412073m, 49.815008m, "Memar Əcəmi", null, new Guid("c8012026-1003-4000-8000-000000000016") },
                    { new Guid("c8012026-1003-4000-8000-000000000017"), "Cavadxan küçəsi — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.410365m, 49.811939m, "Memar Əcəmi 2", null, new Guid("c8012026-1003-4000-8000-000000000017") },
                    { new Guid("c8012026-1003-4000-8000-000000000018"), "Qara Qarayev prospekti — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.411849m, 49.942465m, "Neftçilər", null, new Guid("c8012026-1003-4000-8000-000000000018") },
                    { new Guid("c8012026-1003-4000-8000-000000000019"), "Əhməd Rəcəbli küçəsi — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.40254m, 49.868876m, "Nəriman Nərimanov", null, new Guid("c8012026-1003-4000-8000-000000000019") },
                    { new Guid("c8012026-1003-4000-8000-000000000020"), "Svetlana Məmmədova küçəsi — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.424112m, 49.824204m, "Nəsimi", null, new Guid("c8012026-1003-4000-8000-000000000020") },
                    { new Guid("c8012026-1003-4000-8000-000000000021"), "Cəfər Cabbarlı küçəsi — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.37919m, 49.830063m, "Nizami", null, new Guid("c8012026-1003-4000-8000-000000000021") },
                    { new Guid("c8012026-1003-4000-8000-000000000022"), "Qara Qarayev prospekti — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.417506m, 49.93554m, "Qara Qarayev", null, new Guid("c8012026-1003-4000-8000-000000000022") },
                    { new Guid("c8012026-1003-4000-8000-000000000023"), "Xocalı prospekti — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.383066m, 49.871971m, "Şah İsmayıl Xətai", null, new Guid("c8012026-1003-4000-8000-000000000023") },
                    { new Guid("c8012026-1003-4000-8000-000000000024"), "Bülbül prospekti — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.371751m, 49.844644m, "Sahil", null, new Guid("c8012026-1003-4000-8000-000000000024") },
                    { new Guid("c8012026-1003-4000-8000-000000000025"), "Rövşən Əliyev küçəsi — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.414779m, 49.892668m, "Ulduz", null, new Guid("c8012026-1003-4000-8000-000000000025") },
                    { new Guid("c8012026-1003-4000-8000-000000000026"), "Qara Qarayev prospekti — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.398585m, 49.952052m, "Xalqlar Dostluğu", null, new Guid("c8012026-1003-4000-8000-000000000026") },
                    { new Guid("c8012026-1003-4000-8000-000000000027"), "Dairəvi yol — Çıxış 1", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, 40.421499m, 49.778066m, "Xocəsən", null, new Guid("c8012026-1003-4000-8000-000000000027") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetroStations_Name",
                table: "MetroStations",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetroStations");

            migrationBuilder.DropColumn(
                name: "DeliveryPricingRule",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "MetroDistanceKm",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "MetroStationId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "MetroStationName",
                table: "Orders");
        }
    }
}
