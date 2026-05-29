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

    public AdminCategoriesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CategoryCreateDto dto)
    {
        if (await _context.Categories.AnyAsync(x => x.Name == dto.Name))
            return BadRequest(ApiResponse<string>.Fail("Kateqoriya artıq mövcuddur"));

        var category = new Category
        {
            Name = dto.Name,
            IconUrl = dto.IconUrl
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<Guid>.Ok(category.Id));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(ApiResponse<object>.Ok(
            await _context.Categories
                .OrderBy(x => x.Name)
                .ToListAsync()));
    }
}