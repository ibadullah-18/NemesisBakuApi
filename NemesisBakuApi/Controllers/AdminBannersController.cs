using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Banner;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
public class AdminBannersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IFileService _fileService;

    public AdminBannersController(
        AppDbContext context,
        IFileService fileService)
    {
        _context = context;
        _fileService = fileService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromForm] CreateBannerDto dto)
    {
        if (dto.File == null || dto.File.Length == 0)
            return BadRequest(ApiResponse<string>.Fail("Banner şəkli seçilməlidir"));

        var imageUrl = await _fileService.UploadImageAsync(dto.File, "banners");

        var banner = new Banner
        {
            Title = dto.Title,
            Description = dto.Description,
            ImageUrl = imageUrl,
            ButtonText = dto.ButtonText,
            ButtonUrl = dto.ButtonUrl,
            SortOrder = dto.SortOrder,
            IsActive = true
        };

        _context.Banners.Add(banner);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<Guid>.Ok(banner.Id, "Banner yaradıldı"));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var banners = await _context.Banners
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

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromForm] UpdateBannerDto dto)
    {
        var banner = await _context.Banners.FirstOrDefaultAsync(x => x.Id == id);

        if (banner == null)
            return NotFound(ApiResponse<string>.Fail("Banner tapılmadı"));

        if (dto.File != null && dto.File.Length > 0)
        {
            if (!string.IsNullOrWhiteSpace(banner.ImageUrl))
                await _fileService.DeleteImageAsync(banner.ImageUrl);

            banner.ImageUrl = await _fileService.UploadImageAsync(dto.File, "banners");
        }

        banner.Title = dto.Title;
        banner.Description = dto.Description;
        banner.ButtonText = dto.ButtonText;
        banner.ButtonUrl = dto.ButtonUrl;
        banner.IsActive = dto.IsActive;
        banner.SortOrder = dto.SortOrder;
        banner.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Banner yeniləndi"));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var banner = await _context.Banners.FirstOrDefaultAsync(x => x.Id == id);

        if (banner == null)
            return NotFound(ApiResponse<string>.Fail("Banner tapılmadı"));

        if (!string.IsNullOrWhiteSpace(banner.ImageUrl))
            await _fileService.DeleteImageAsync(banner.ImageUrl);

        banner.IsDeleted = true;
        banner.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Banner silindi"));
    }
}