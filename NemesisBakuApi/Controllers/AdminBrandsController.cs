using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Common;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin, Admin")]
public class AdminBrandsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IFileService _fileService;

    public AdminBrandsController(
        AppDbContext context,
        IFileService fileService)
    {
        _context = context;
        _fileService = fileService;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] BrandCreateDto dto)
    {
        var name = dto.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(ApiResponse<string>.Fail("Brend adı boş ola bilməz"));

        if (await _context.Brands.AnyAsync(x => x.Name == name))
            return BadRequest(ApiResponse<string>.Fail("Brend artıq mövcuddur"));

        string? imageUrl = null;

        if (dto.Image != null)
        {
            imageUrl = await _fileService.UploadImageAsync(dto.Image, "brands");
        }

        var brand = new Brand
        {
            Name = name,
            ImageUrl = imageUrl
        };

        _context.Brands.Add(brand);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<Guid>.Ok(brand.Id, "Brend yaradıldı"));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var brands = await _context.Brands
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.ImageUrl
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(brands));
    }

    [HttpPut("{id}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update(Guid id, [FromForm] BrandCreateDto dto)
    {
        var brand = await _context.Brands.FirstOrDefaultAsync(x => x.Id == id);

        if (brand == null)
            return NotFound(ApiResponse<string>.Fail("Brend tapılmadı"));

        var name = dto.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(ApiResponse<string>.Fail("Brend adı boş ola bilməz"));

        var exists = await _context.Brands
            .AnyAsync(x => x.Id != id && x.Name == name);

        if (exists)
            return BadRequest(ApiResponse<string>.Fail("Bu adda brend artıq mövcuddur"));

        if (dto.Image != null)
        {
            if (!string.IsNullOrWhiteSpace(brand.ImageUrl))
            {
                await _fileService.DeleteImageAsync(brand.ImageUrl);
            }

            brand.ImageUrl = await _fileService.UploadImageAsync(dto.Image, "brands");
        }

        brand.Name = name;
        brand.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Brend yeniləndi"));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var brand = await _context.Brands.FirstOrDefaultAsync(x => x.Id == id);

        if (brand == null)
            return NotFound(ApiResponse<string>.Fail("Brend tapılmadı"));

        if (!string.IsNullOrWhiteSpace(brand.ImageUrl))
        {
            await _fileService.DeleteImageAsync(brand.ImageUrl);
        }

        brand.IsDeleted = true;
        brand.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Brend silindi"));
    }
}