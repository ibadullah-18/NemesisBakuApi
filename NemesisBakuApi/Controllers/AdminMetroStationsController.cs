using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Controllers;

public class MetroStationInput
{
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    [Required, Range(-90d, 90d)] public decimal? Latitude { get; set; }
    [Required, Range(-180d, 180d)] public decimal? Longitude { get; set; }
    [MaxLength(300)] public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? Version { get; set; }
}

[ApiController, Route("api/[controller]"), Authorize(Roles = "SuperAdmin,Admin")]
public class AdminMetroStationsController(AppDbContext db, IAuditLogService audit, ILogger<AdminMetroStationsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(ApiResponse<object>.Ok(
        await db.MetroStations.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct)));

    [HttpPost]
    public Task<IActionResult> Create(MetroStationInput input, CancellationToken ct) => Save(null, input, ct);

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, MetroStationInput input, CancellationToken ct) => Save(id, input, ct);

    private async Task<IActionResult> Save(Guid? id, MetroStationInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || !input.Latitude.HasValue || !input.Longitude.HasValue ||
            input.Latitude is < -90 or > 90 || input.Longitude is < -180 or > 180)
            return BadRequest(ApiResponse<string>.Fail("Metro adını və etibarlı koordinatları daxil edin."));
        var station = id.HasValue ? await db.MetroStations.FirstOrDefaultAsync(x => x.Id == id, ct) : new MetroStation();
        if (station == null) return NotFound(ApiResponse<string>.Fail("Metro tapılmadı."));
        if (id.HasValue && station.Version != input.Version)
            return Conflict(ApiResponse<string>.Fail("Metro məlumatı dəyişib. Siyahını yeniləyin."));
        var name = input.Name.Trim();
        if (await db.MetroStations.AnyAsync(x => x.Id != station.Id && x.Name == name, ct))
            return Conflict(ApiResponse<string>.Fail("Bu adda metro artıq mövcuddur."));
        station.Name = name; station.Latitude = Math.Round(input.Latitude.Value, 6); station.Longitude = Math.Round(input.Longitude.Value, 6);
        station.Address = input.Address?.Trim(); station.IsActive = input.IsActive;
        station.Version = Guid.NewGuid(); station.UpdatedAt = DateTime.UtcNow;
        if (!id.HasValue) db.MetroStations.Add(station);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(ApiResponse<string>.Fail("Metro məlumatı dəyişib. Siyahını yeniləyin.")); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { return Conflict(ApiResponse<string>.Fail("Bu adda metro artıq mövcuddur.")); }
        try
        {
        await audit.CreateAsync(Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null,
            id.HasValue ? "Update" : "Create", "MetroStation", station.Id.ToString(),
            $"{station.Name}: {station.Latitude}, {station.Longitude}; Active: {station.IsActive}",
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
        }
        catch (Exception ex) { logger.LogError(ex, "Metro station {StationId} saved, but audit logging failed", station.Id); }
        return Ok(ApiResponse<object>.Ok(station));
    }
}
