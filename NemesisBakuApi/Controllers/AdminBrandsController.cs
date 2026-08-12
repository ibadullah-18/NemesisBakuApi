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
[Authorize(Roles = "SuperAdmin,Admin")]
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
    public async Task<IActionResult> Create(
        [FromForm] BrandCreateDto dto,
        CancellationToken cancellationToken)
    {
        var name = dto.Name?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Brend adı boş ola bilməz"));
        }

        var exists = await _context.Brands
            .AsNoTracking()
            .AnyAsync(
                x => x.Name == name,
                cancellationToken);

        if (exists)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Brend artıq mövcuddur"));
        }

        string? imageUrl = null;

        if (dto.Image != null)
        {
            imageUrl = await _fileService
                .UploadImageAsync(
                    dto.Image,
                    "brands");
        }

        var brand = new Brand
        {
            Name = name,
            ImageUrl = imageUrl
        };

        _context.Brands.Add(brand);

        try
        {
            await _context.SaveChangesAsync(
                cancellationToken);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                await TryDeleteImageAsync(imageUrl);
            }

            throw;
        }

        return Ok(
            ApiResponse<Guid>.Ok(
                brand.Id,
                "Brend yaradıldı"));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken cancellationToken)
    {
        var brands = await _context.Brands
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.ImageUrl
            })
            .ToListAsync(cancellationToken);

        return Ok(
            ApiResponse<object>.Ok(brands));
    }

    [HttpPut("{id:guid}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromForm] BrandCreateDto dto,
        CancellationToken cancellationToken)
    {
        var brand = await _context.Brands
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (brand == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Brend tapılmadı"));
        }

        var name = dto.Name?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Brend adı boş ola bilməz"));
        }

        var exists = await _context.Brands
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.Id != id &&
                    x.Name == name,
                cancellationToken);

        if (exists)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Bu adda brend artıq mövcuddur"));
        }

        var oldImageUrl = brand.ImageUrl;
        string? newImageUrl = null;

        if (dto.Image != null)
        {
            newImageUrl = await _fileService
                .UploadImageAsync(
                    dto.Image,
                    "brands");

            brand.ImageUrl = newImageUrl;
        }

        brand.Name = name;
        brand.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync(
                cancellationToken);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(newImageUrl))
            {
                await TryDeleteImageAsync(newImageUrl);
            }

            throw;
        }

        if (newImageUrl != null &&
            !string.IsNullOrWhiteSpace(oldImageUrl))
        {
            await TryDeleteImageAsync(oldImageUrl);
        }

        return Ok(
            ApiResponse<string>.Ok(
                "Brend yeniləndi"));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var brand = await _context.Brands
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (brand == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Brend tapılmadı"));
        }

        var imageUrl = brand.ImageUrl;

        brand.IsDeleted = true;
        brand.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            await TryDeleteImageAsync(imageUrl);
        }

        return Ok(
            ApiResponse<string>.Ok(
                "Brend silindi"));
    }

    private async Task TryDeleteImageAsync(
        string imageUrl)
    {
        try
        {
            await _fileService.DeleteImageAsync(
                imageUrl);
        }
        catch
        {
            // Cloudinary xətası database əməliyyatını pozmasın.
        }
    }
}