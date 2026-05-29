using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Campaign;
using NemesisBakuApi.Helpers;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CampaignsController : ControllerBase
{
    private readonly AppDbContext _context;

    public CampaignsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var now = DateTime.UtcNow;

        var campaigns = await _context.Campaigns
            .Where(x =>
                x.IsActive &&
                x.StartDate <= now &&
                (!x.EndDate.HasValue || x.EndDate.Value >= now))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new CampaignDto
            {
                Id = x.Id,
                Title = x.Title,
                Description = x.Description,
                ImageUrl = x.ImageUrl,
                RedirectUrl = x.RedirectUrl,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                IsActive = x.IsActive
            })
            .ToListAsync();

        return Ok(ApiResponse<List<CampaignDto>>.Ok(campaigns));
    }
}