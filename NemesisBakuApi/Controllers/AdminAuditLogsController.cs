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

    public AdminAuditLogsController(
        AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? search,
        [FromQuery] string? action,
        [FromQuery] string? entityName,
        [FromQuery] Guid? userId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page <= 0)
        {
            page = 1;
        }

        if (pageSize <= 0)
        {
            pageSize = 20;
        }

        if (pageSize > 100)
        {
            pageSize = 100;
        }

        var query = _context.AuditLogs
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchValue =
                search.Trim().ToLower();

            query = query.Where(x =>
                x.Action.ToLower()
                    .Contains(searchValue) ||

                x.EntityName.ToLower()
                    .Contains(searchValue) ||

                (x.EntityId != null &&
                 x.EntityId.ToLower()
                     .Contains(searchValue)) ||

                (x.Description != null &&
                 x.Description.ToLower()
                     .Contains(searchValue)) ||

                (x.IpAddress != null &&
                 x.IpAddress.ToLower()
                     .Contains(searchValue)) ||

                (x.UserAgent != null &&
                 x.UserAgent.ToLower()
                     .Contains(searchValue)) ||

                (x.User != null &&
                 x.User.FullName.ToLower()
                     .Contains(searchValue)));
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var actionValue =
                action.Trim().ToLower();

            query = query.Where(
                x => x.Action.ToLower() ==
                     actionValue);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            var entityValue =
                entityName.Trim().ToLower();

            query = query.Where(
                x => x.EntityName.ToLower() ==
                     entityValue);
        }

        if (userId.HasValue)
        {
            query = query.Where(
                x => x.UserId == userId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(
                x => x.CreatedAt >=
                     fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(
                x => x.CreatedAt <=
                     toDate.Value);
        }

        var totalCount = await query.CountAsync(
            cancellationToken);

        var logs = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AuditLogDto
            {
                Id = x.Id,

                UserFullName =
                    x.User != null
                        ? x.User.FullName
                        : null,

                Action = x.Action,
                EntityName = x.EntityName,
                EntityId = x.EntityId,
                Description = x.Description,
                IpAddress = x.IpAddress,
                UserAgent = x.UserAgent,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var result = new PagedResult<AuditLogDto>
        {
            Items = logs,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,

            TotalPages = (int)Math.Ceiling(
                totalCount / (double)pageSize)
        };

        return Ok(
            ApiResponse<PagedResult<AuditLogDto>>
                .Ok(result));
    }
}