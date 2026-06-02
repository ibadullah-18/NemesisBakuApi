using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Admin;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;
using System.Security.Claims;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
public class AdminUsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly IAuditLogService _auditLogService;

    public AdminUsersController(
    AppDbContext context,
    UserManager<AppUser> userManager,
    IAuditLogService auditLogService)
    {
        _context = context;
        _userManager = userManager;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page <= 0)
            page = 1;

        if (pageSize <= 0)
            pageSize = 20;

        if (pageSize > 100)
            pageSize = 100;

        var query = _userManager.Users
            .Where(x => !x.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();

            query = query.Where(x =>
                x.FullName.ToLower().Contains(s) ||
                (x.PhoneNumber != null && x.PhoneNumber.ToLower().Contains(s)) ||
                (x.Email != null && x.Email.ToLower().Contains(s)));
        }

        var totalCount = await query.CountAsync();

        var users = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var resultItems = new List<UserListDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);

            if (!string.IsNullOrWhiteSpace(role) &&
                !roles.Any(r => r.Equals(role, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            resultItems.Add(new UserListDto
            {
                Id = user.Id,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber ?? "",
                Email = user.Email,
                IsActive = user.IsActive,
                IsDeleted = user.IsDeleted,
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt,
                Roles = roles
            });
        }

        var result = new PagedResult<UserListDto>
        {
            Items = resultItems,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };

        return Ok(ApiResponse<PagedResult<UserListDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserDetail(Guid id)
    {
        var user = await _userManager.Users
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (user == null)
            return NotFound(ApiResponse<string>.Fail("İstifadəçi tapılmadı"));

        var orders = await _context.Orders
            .Where(x => x.UserId == user.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new UserOrderMiniDto
            {
                Id = x.Id,
                OrderNumber = x.OrderNumber,
                TotalPrice = x.TotalPrice,
                Status = x.Status,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();

        var dto = new UserDetailDto
        {
            Id = user.Id,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber ?? "",
            Email = user.Email,
            DateOfBirth = user.DateOfBirth,
            ProfileImageUrl = user.ProfileImageUrl,
            LoyaltyCardCode = user.LoyaltyCardCode,
            IsActive = user.IsActive,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,

            BasketItemCount = await _context.BasketItems.CountAsync(x => x.UserId == user.Id),
            FavoriteCount = await _context.Favorites.CountAsync(x => x.UserId == user.Id),
            OrderCount = orders.Count,

            Orders = orders
        };

        return Ok(ApiResponse<UserDetailDto>.Ok(dto));
    }

    [HttpPost("create-admin")]
    public async Task<IActionResult> CreateAdmin(CreateAdminDto dto)
    {
        if (dto.Password != dto.ConfirmPassword)
            return BadRequest(ApiResponse<string>.Fail("Şifrələr uyğun deyil"));

        var existingUser = await _userManager.FindByNameAsync(dto.PhoneNumber);

        if (existingUser != null)
            return BadRequest(ApiResponse<string>.Fail("Bu nömrə artıq qeydiyyatdan keçib"));

        var admin = new AppUser
        {
            FullName = dto.FullName,
            UserName = dto.PhoneNumber,
            PhoneNumber = dto.PhoneNumber,
            Email = dto.Email,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(admin, dto.Password);

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        await _userManager.AddToRoleAsync(admin, "Admin");

        await WriteAuditLogAsync(
            "CreateAdmin",
            "User",
            admin.Id.ToString(),
            $"Admin yaradıldı: {admin.FullName}");

        return Ok(ApiResponse<Guid>.Ok(admin.Id, "Admin uğurla yaradıldı"));
    }

    [HttpPut("{id}/deactivate")]
    public async Task<IActionResult> DeactivateUser(Guid id)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (user == null)
            return NotFound(ApiResponse<string>.Fail("İstifadəçi tapılmadı"));

        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Contains("SuperAdmin"))
            return BadRequest(ApiResponse<string>.Fail("SuperAdmin deaktiv edilə bilməz"));

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);

        await WriteAuditLogAsync(
            "DeactivateUser",
            "User",
            user.Id.ToString(),
            $"İstifadəçi deaktiv edildi: {user.FullName}");

        return Ok(ApiResponse<string>.Ok("İstifadəçi deaktiv edildi"));
    }

    [HttpPut("{id}/activate")]
    public async Task<IActionResult> ActivateUser(Guid id)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (user == null)
            return NotFound(ApiResponse<string>.Fail("İstifadəçi tapılmadı"));

        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);

        await WriteAuditLogAsync(
            "ActivateUser", 
            "User",
            user.Id.ToString(),
            $"İstifadəçi aktiv edildi: {user.FullName}");

        return Ok(ApiResponse<string>.Ok("İstifadəçi aktiv edildi"));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (user == null)
            return NotFound(ApiResponse<string>.Fail("İstifadəçi tapılmadı"));

        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Contains("SuperAdmin"))
            return BadRequest(ApiResponse<string>.Fail("SuperAdmin silinə bilməz"));

        user.IsDeleted = true;
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);

        await WriteAuditLogAsync(
            "DeleteUser",
            "User",
            user.Id.ToString(),
            $"İstifadəçi silindi: {user.FullName}");

        return Ok(ApiResponse<string>.Ok("İstifadəçi silindi"));
    }

    private Guid? GetUserIdOrNull()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return null;

        return Guid.Parse(userId);
    }

    private async Task WriteAuditLogAsync(
        string action,
        string entityName,
        string? entityId,
        string? description)
    {
        await _auditLogService.CreateAsync(
            GetUserIdOrNull(),
            action,
            entityName,
            entityId,
            description,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
    }
}