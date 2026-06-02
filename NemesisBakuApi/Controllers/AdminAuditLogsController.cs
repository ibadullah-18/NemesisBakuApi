using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Admin;
using NemesisBakuApi.Helpers;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
public class AdminAuditLogsController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminAuditLogsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _context.AuditLogs
            .Include(x => x.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();

            query = query.Where(x =>
                x.Action.ToLower().Contains(s) ||
                x.EntityName.ToLower().Contains(s) ||
                (x.Description != null && x.Description.ToLower().Contains(s)) ||
                (x.User != null && x.User.FullName.ToLower().Contains(s)));
        }

        var totalCount = await query.CountAsync();

        var logs = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AuditLogDto
            {
                Id = x.Id,
                UserFullName = x.User != null ? x.User.FullName : null,
                Action = x.Action,
                EntityName = x.EntityName,
                EntityId = x.EntityId,
                Description = x.Description,
                IpAddress = x.IpAddress,
                UserAgent = x.UserAgent,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();

        var result = new PagedResult<AuditLogDto>
        {
            Items = logs,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };

        return Ok(ApiResponse<PagedResult<AuditLogDto>>.Ok(result));
    }
}