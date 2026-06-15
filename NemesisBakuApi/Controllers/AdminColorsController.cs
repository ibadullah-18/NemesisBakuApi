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
public class AdminColorsController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminColorsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Create(ColorCreateDto dto)
    {
        if (await _context.Colors.AnyAsync(x => x.Name == dto.Name))
            return BadRequest(ApiResponse<string>.Fail("Rəng artıq mövcuddur"));

        var color = new Color
        {
            Name = dto.Name,
            HexCode = dto.HexCode
        };

        _context.Colors.Add(color);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<Guid>.Ok(color.Id));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(ApiResponse<object>.Ok(
            await _context.Colors
                .OrderBy(x => x.Name)
                .ToListAsync()));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteColor(Guid id)
    {
        var color = await _context.Colors
            .FirstOrDefaultAsync(x => x.Id == id);

        if (color == null)
            return NotFound(ApiResponse<string>.Fail("Rəng tapılmadı"));

        var hasVariants = await _context.ProductVariants
            .AnyAsync(x => x.ColorId == id);

        if (hasVariants)
            return BadRequest(ApiResponse<string>.Fail(
                "Bu rəng məhsullarda istifadə olunur"));

        color.IsDeleted = true;
        color.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Rəng silindi"));
    }
}