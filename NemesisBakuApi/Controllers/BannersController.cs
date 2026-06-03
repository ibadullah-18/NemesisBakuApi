using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Banner;
using NemesisBakuApi.Helpers;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BannersController : ControllerBase
{
    private readonly AppDbContext _context;

    public BannersController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetActive()
    {
        var banners = await _context.Banners
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new BannerDto
            {
                Id = x.Id,
                Title = x.Title,
                Description = x.Description,
                ImageUrl = x.ImageUrl,
                ButtonText = x.ButtonText,
                ButtonUrl = x.ButtonUrl,
                IsActive = x.IsActive,
                SortOrder = x.SortOrder
            })
            .ToListAsync();

        return Ok(ApiResponse<List<BannerDto>>.Ok(banners));
    }
}