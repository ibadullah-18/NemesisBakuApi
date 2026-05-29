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
        return Ok(ApiResponse<object>.Ok(
            await _context.Sizes
                .OrderBy(x => x.Value)
                .ToListAsync()));
    }
}
