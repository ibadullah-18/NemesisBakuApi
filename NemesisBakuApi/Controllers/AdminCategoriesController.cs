using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Common;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminCategoriesController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminCategoriesController(
        AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CategoryCreateDto dto,
        CancellationToken cancellationToken)
    {
        var categoryExists =
            await _context.Categories
                .AsNoTracking()
                .AnyAsync(
                    x => x.Name == dto.Name,
                    cancellationToken);

        if (categoryExists)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Kateqoriya artıq mövcuddur"));
        }

        var category = new Category
        {
            Name = dto.Name,
            IconUrl = dto.IconUrl
        };

        _context.Categories.Add(category);

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(
            ApiResponse<Guid>.Ok(category.Id));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken cancellationToken)
    {
        var categories =
            await _context.Categories
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

        return Ok(
            ApiResponse<object>.Ok(categories));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCategory(
        Guid id,
        CancellationToken cancellationToken)
    {
        var category =
            await _context.Categories
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

        if (category == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Kateqoriya tapılmadı"));
        }

        var hasProducts =
            await _context.Products
                .AsNoTracking()
                .AnyAsync(
                    x => x.CategoryId == id,
                    cancellationToken);

        if (hasProducts)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Bu kateqoriyada məhsullar var, " +
                    "əvvəl məhsulları silin"));
        }

        category.IsDeleted = true;
        category.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(
            ApiResponse<string>.Ok(
                "Kateqoriya silindi"));
    }
}