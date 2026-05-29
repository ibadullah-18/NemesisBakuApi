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
public class AdminBrandsController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminBrandsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Create(LookupCreateDto dto)
    {
        if (await _context.Brands.AnyAsync(x => x.Name == dto.Name))
            return BadRequest(ApiResponse<string>.Fail("Brend artıq mövcuddur"));

        var brand = new Brand
        {
            Name = dto.Name
        };

        _context.Brands.Add(brand);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<Guid>.Ok(brand.Id));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(ApiResponse<object>.Ok(
            await _context.Brands
                .OrderBy(x => x.Name)
                .ToListAsync()));
    }
}