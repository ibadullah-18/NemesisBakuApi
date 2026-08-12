using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminSizesController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminSizesController(
        AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        string size,
        CancellationToken cancellationToken)
    {
        var sizeExists =
            await _context.Sizes
                .AsNoTracking()
                .AnyAsync(
                    x => x.Value == size,
                    cancellationToken);

        if (sizeExists)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Razmer artıq mövcuddur"));
        }

        var entity = new Size
        {
            Value = size
        };

        _context.Sizes.Add(entity);

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(
            ApiResponse<Guid>.Ok(entity.Id));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken cancellationToken)
    {
        var sizes = await _context.Sizes
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Value)
            .ToListAsync(cancellationToken);

        return Ok(
            ApiResponse<object>.Ok(sizes));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteSize(
        Guid id,
        CancellationToken cancellationToken)
    {
        var size = await _context.Sizes
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (size == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Razmer tapılmadı"));
        }

        var hasVariants =
            await _context.ProductVariants
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.SizeId == id &&
                        !x.IsDeleted,
                    cancellationToken);

        if (hasVariants)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Bu razmer məhsul variantlarında " +
                    "istifadə olunur. Əvvəl həmin " +
                    "variantları silin"));
        }

        size.IsDeleted = true;
        size.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(
            ApiResponse<string>.Ok(
                "Razmer silindi"));
    }
}