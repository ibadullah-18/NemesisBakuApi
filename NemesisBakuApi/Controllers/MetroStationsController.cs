using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.Helpers;

namespace NemesisBakuApi.Controllers;

[ApiController, Route("api/[controller]")]
public class MetroStationsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(ApiResponse<object>.Ok(
        await db.MetroStations.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Latitude, x.Longitude, x.Address }).ToListAsync(ct)));
}
