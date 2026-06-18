using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.HomeSection;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
public class AdminHomeSectionsController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminHomeSectionsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateHomeSectionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(ApiResponse<string>.Fail("Başlıq boş ola bilməz"));

        if (dto.StartDate >= dto.EndDate)
            return BadRequest(ApiResponse<string>.Fail("Başlama tarixi bitmə tarixindən əvvəl olmalıdır"));

        var section = new HomeSection
        {
            Title = dto.Title.Trim(),
            Subtitle = dto.Subtitle,
            DisplayOrder = dto.DisplayOrder,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            IsActive = dto.IsActive
        };

        if (dto.ProductIds.Any())
        {
            var validProductIds = await _context.Products
                .Where(x => dto.ProductIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync();

            var order = 0;

            foreach (var productId in dto.ProductIds.Distinct())
            {
                if (!validProductIds.Contains(productId))
                    continue;

                section.Products.Add(new HomeSectionProduct
                {
                    ProductId = productId,
                    Order = order++
                });
            }
        }

        _context.HomeSections.Add(section);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<Guid>.Ok(section.Id, "Home section yaradıldı"));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var sections = await _context.HomeSections
            .Include(x => x.Products)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new HomeSectionDto
            {
                Id = x.Id,
                Title = x.Title,
                Subtitle = x.Subtitle,
                DisplayOrder = x.DisplayOrder,
                IsActive = x.IsActive,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                ProductIds = x.Products
                    .OrderBy(p => p.Order)
                    .Select(p => p.ProductId)
                    .ToList()
            })
            .ToListAsync();

        return Ok(ApiResponse<List<HomeSectionDto>>.Ok(sections));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var section = await _context.HomeSections
            .Include(x => x.Products)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (section == null)
            return NotFound(ApiResponse<string>.Fail("Home section tapılmadı"));

        var dto = new HomeSectionDto
        {
            Id = section.Id,
            Title = section.Title,
            Subtitle = section.Subtitle,
            DisplayOrder = section.DisplayOrder,
            IsActive = section.IsActive,
            StartDate = section.StartDate,
            EndDate = section.EndDate,
            ProductIds = section.Products
                .OrderBy(x => x.Order)
                .Select(x => x.ProductId)
                .ToList()
        };

        return Ok(ApiResponse<HomeSectionDto>.Ok(dto));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateHomeSectionDto dto)
    {
        var section = await _context.HomeSections
            .Include(x => x.Products)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (section == null)
            return NotFound(ApiResponse<string>.Fail("Home section tapılmadı"));

        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(ApiResponse<string>.Fail("Başlıq boş ola bilməz"));

        if (dto.StartDate >= dto.EndDate)
            return BadRequest(ApiResponse<string>.Fail("Başlama tarixi bitmə tarixindən əvvəl olmalıdır"));

        section.Title = dto.Title.Trim();
        section.Subtitle = dto.Subtitle;
        section.DisplayOrder = dto.DisplayOrder;
        section.StartDate = dto.StartDate;
        section.EndDate = dto.EndDate;
        section.IsActive = dto.IsActive;
        section.UpdatedAt = DateTime.UtcNow;

        foreach (var oldProduct in section.Products)
        {
            oldProduct.IsDeleted = true;
            oldProduct.UpdatedAt = DateTime.UtcNow;
        }

        if (dto.ProductIds.Any())
        {
            var validProductIds = await _context.Products
                .Where(x => dto.ProductIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync();

            var order = 0;

            foreach (var productId in dto.ProductIds.Distinct())
            {
                if (!validProductIds.Contains(productId))
                    continue;

                section.Products.Add(new HomeSectionProduct
                {
                    ProductId = productId,
                    Order = order++
                });
            }
        }

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Home section yeniləndi"));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var section = await _context.HomeSections
            .Include(x => x.Products)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (section == null)
            return NotFound(ApiResponse<string>.Fail("Home section tapılmadı"));

        section.IsDeleted = true;
        section.IsActive = false;
        section.UpdatedAt = DateTime.UtcNow;

        foreach (var product in section.Products)
        {
            product.IsDeleted = true;
            product.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Home section silindi"));
    }
}