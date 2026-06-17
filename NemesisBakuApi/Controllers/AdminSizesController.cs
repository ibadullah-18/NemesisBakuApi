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

    public AdminSizesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Create(string size)
    {
        if (await _context.Sizes.AnyAsync(x => x.Value == size))
            return BadRequest(ApiResponse<string>.Fail("Razmer artıq mövcuddur"));

        var entity = new Size
        {
            Value = size
        };

        _context.Sizes.Add(entity);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<Guid>.Ok(entity.Id));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var sizes = await _context.Sizes
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Value)
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(sizes));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSize(Guid id)
    {
        var size = await _context.Sizes
            .FirstOrDefaultAsync(x => x.Id == id);

        if (size == null)
            return NotFound(ApiResponse<string>.Fail("Razmer tapılmadı"));

        var hasVariants = await _context.ProductVariants
            .AnyAsync(x => x.SizeId == id && !x.IsDeleted);

        if (hasVariants)
            return BadRequest(ApiResponse<string>.Fail(
                "Bu razmer məhsul variantlarında istifadə olunur. Əvvəl həmin variantları silin"));

        size.IsDeleted = true;
        size.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Razmer silindi"));
    }
}
