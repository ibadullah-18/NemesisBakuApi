using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Admin;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
public class AdminUsersController : ControllerBase
{
    private const int MaxUserDetailOrders = 100;

    private readonly AppDbContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly IAuditLogService _auditLogService;
    private readonly IFileService _fileService;

    public AdminUsersController(
        AppDbContext context,
        UserManager<AppUser> userManager,
        IAuditLogService auditLogService,
        IFileService fileService)
    {
        _context = context;
        _userManager = userManager;
        _auditLogService = auditLogService;
        _fileService = fileService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Users
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            var pattern = $"%{value}%";

            query = query.Where(x =>
                EF.Functions.Like(
                    x.FullName,
                    pattern) ||
                (x.PhoneNumber != null &&
                 EF.Functions.Like(
                     x.PhoneNumber,
                     pattern)) ||
                (x.Email != null &&
                 EF.Functions.Like(
                     x.Email,
                     pattern)));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var normalizedRole =
                role.Trim().ToUpperInvariant();

            var roleId = await _context.Roles
                .AsNoTracking()
                .Where(x =>
                    x.NormalizedName == normalizedRole)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (!roleId.HasValue)
            {
                return Ok(
                    ApiResponse<PagedResult<UserListDto>>
                        .Ok(new PagedResult<UserListDto>
                        {
                            Items = new List<UserListDto>(),
                            Page = page,
                            PageSize = pageSize,
                            TotalCount = 0,
                            TotalPages = 0
                        }));
            }

            query = query.Where(user =>
                _context.UserRoles.Any(userRole =>
                    userRole.UserId == user.Id &&
                    userRole.RoleId == roleId.Value));
        }

        var totalCount = await query.CountAsync(
            cancellationToken);

        var users = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.FullName,
                x.PhoneNumber,
                x.Email,
                x.IsActive,
                x.IsDeleted,
                x.LastLoginAt,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var userIds = users
            .Select(x => x.Id)
            .ToList();

        var roleRows = await (
            from userRole in _context.UserRoles
                .AsNoTracking()
            join identityRole in _context.Roles
                .AsNoTracking()
                on userRole.RoleId equals identityRole.Id
            where userIds.Contains(userRole.UserId)
            select new
            {
                userRole.UserId,
                RoleName = identityRole.Name!
            })
            .ToListAsync(cancellationToken);

        var rolesByUser = roleRows
            .GroupBy(x => x.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IList<string>)group
                    .Select(x => x.RoleName)
                    .ToList());

        var items = users
            .Select(user => new UserListDto
            {
                Id = user.Id,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber ?? "",
                Email = user.Email,
                IsActive = user.IsActive,
                IsDeleted = user.IsDeleted,
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt,

                Roles = rolesByUser.TryGetValue(
                    user.Id,
                    out var roles)
                        ? roles
                        : new List<string>()
            })
            .ToList();

        var result = new PagedResult<UserListDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,

            TotalPages = (int)Math.Ceiling(
                totalCount / (double)pageSize)
        };

        return Ok(
            ApiResponse<PagedResult<UserListDto>>
                .Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUserDetail(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(x =>
                x.Id == id &&
                !x.IsDeleted)
            .Select(x => new
            {
                x.Id,
                x.FullName,
                x.PhoneNumber,
                x.Email,
                x.DateOfBirth,
                x.ProfileImageUrl,
                x.LoyaltyCardCode,
                x.IsActive,
                x.LastLoginAt,
                x.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "İstifadəçi tapılmadı"));
        }

        var orders = await _context.Orders
            .AsNoTracking()
            .Where(x => x.UserId == user.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(MaxUserDetailOrders)
            .Select(x => new UserOrderMiniDto
            {
                Id = x.Id,
                OrderNumber = x.OrderNumber,
                TotalPrice = x.TotalPrice,
                Status = x.Status,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var orderCount = await _context.Orders
            .AsNoTracking()
            .CountAsync(
                x => x.UserId == user.Id,
                cancellationToken);

        var basketItemCount =
            await _context.BasketItems
                .AsNoTracking()
                .CountAsync(
                    x => x.UserId == user.Id,
                    cancellationToken);

        var favoriteCount =
            await _context.Favorites
                .AsNoTracking()
                .CountAsync(
                    x => x.UserId == user.Id,
                    cancellationToken);

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

            BasketItemCount = basketItemCount,
            FavoriteCount = favoriteCount,
            OrderCount = orderCount,
            Orders = orders
        };

        return Ok(
            ApiResponse<UserDetailDto>.Ok(dto));
    }

    [HttpPost("create-admin")]
    public async Task<IActionResult> CreateAdmin(
        CreateAdminDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.Password != dto.ConfirmPassword)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Şifrələr uyğun deyil"));
        }

        var phoneNumber = dto.PhoneNumber?.Trim();

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Telefon nömrəsi boş ola bilməz"));
        }

        var existingUser =
            await _userManager.FindByNameAsync(
                phoneNumber);

        if (existingUser != null)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Bu nömrə artıq qeydiyyatdan keçib"));
        }

        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            var existingEmail =
                await _userManager.FindByEmailAsync(
                    dto.Email.Trim());

            if (existingEmail != null)
            {
                return BadRequest(
                    ApiResponse<string>.Fail(
                        "Bu email artıq qeydiyyatdan keçib"));
            }
        }

        var admin = new AppUser
        {
            FullName = dto.FullName.Trim(),
            UserName = phoneNumber,
            PhoneNumber = phoneNumber,

            Email = string.IsNullOrWhiteSpace(dto.Email)
                ? null
                : dto.Email.Trim(),

            IsActive = true
        };

        var createResult =
            await _userManager.CreateAsync(
                admin,
                dto.Password);

        if (!createResult.Succeeded)
        {
            return BadRequest(createResult.Errors);
        }

        var roleResult =
            await _userManager.AddToRoleAsync(
                admin,
                "Admin");

        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(admin);
            return BadRequest(roleResult.Errors);
        }

        await WriteAuditLogAsync(
            "CreateAdmin",
            admin,
            $"Admin yaradıldı: {admin.FullName}");

        return Ok(
            ApiResponse<Guid>.Ok(
                admin.Id,
                "Admin uğurla yaradıldı"));
    }

    [HttpPut("{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await FindActiveRecordAsync(
            id,
            cancellationToken);

        if (user == null)
        {
            return UserNotFound();
        }

        if (await IsSuperAdminAsync(user))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "SuperAdmin deaktiv edilə bilməz"));
        }

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        await RevokeRefreshTokensAsync(
            user.Id,
            cancellationToken);

        await WriteAuditLogAsync(
            "DeactivateUser",
            user,
            $"İstifadəçi deaktiv edildi: " +
            user.FullName);

        return Ok(
            ApiResponse<string>.Ok(
                "İstifadəçi deaktiv edildi"));
    }

    [HttpPut("{id:guid}/activate")]
    public async Task<IActionResult> ActivateUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await FindActiveRecordAsync(
            id,
            cancellationToken);

        if (user == null)
        {
            return UserNotFound();
        }

        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        await WriteAuditLogAsync(
            "ActivateUser",
            user,
            $"İstifadəçi aktiv edildi: " +
            user.FullName);

        return Ok(
            ApiResponse<string>.Ok(
                "İstifadəçi aktiv edildi"));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await FindActiveRecordAsync(
            id,
            cancellationToken);

        if (user == null)
        {
            return UserNotFound();
        }

        if (await IsSuperAdminAsync(user))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "SuperAdmin silinə bilməz"));
        }

        var profileImageUrl = user.ProfileImageUrl;

        user.ProfileImageUrl = null;
        user.IsDeleted = true;
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        await RevokeRefreshTokensAsync(
            user.Id,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(
                profileImageUrl))
        {
            await TryDeleteImageAsync(
                profileImageUrl);
        }

        await WriteAuditLogAsync(
            "DeleteUser",
            user,
            $"İstifadəçi silindi: {user.FullName}");

        return Ok(
            ApiResponse<string>.Ok(
                "İstifadəçi silindi"));
    }

    private async Task<AppUser?> FindActiveRecordAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await _context.Users
            .FirstOrDefaultAsync(
                x =>
                    x.Id == id &&
                    !x.IsDeleted,
                cancellationToken);
    }

    private async Task<bool> IsSuperAdminAsync(
        AppUser user)
    {
        return await _userManager.IsInRoleAsync(
            user,
            "SuperAdmin");
    }

    private async Task RevokeRefreshTokensAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _context.RefreshTokens
            .Where(x =>
                x.UserId == userId &&
                !x.IsRevoked &&
                !x.IsUsed)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.IsRevoked,
                        true)
                    .SetProperty(
                        x => x.UpdatedAt,
                        DateTime.UtcNow),
                cancellationToken);
    }

    private IActionResult UserNotFound()
    {
        return NotFound(
            ApiResponse<string>.Fail(
                "İstifadəçi tapılmadı"));
    }

    private Guid? GetAdminIdOrNull()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId)
            ? userId
            : null;
    }

    private async Task WriteAuditLogAsync(
        string action,
        AppUser user,
        string description)
    {
        await _auditLogService.CreateAsync(
            GetAdminIdOrNull(),
            action,
            "User",
            user.Id.ToString(),
            description,
            HttpContext.Connection
                .RemoteIpAddress?
                .ToString(),
            Request.Headers.UserAgent.ToString());
    }

    private async Task TryDeleteImageAsync(
        string imageUrl)
    {
        try
        {
            await _fileService.DeleteImageAsync(
                imageUrl);
        }
        catch
        {
            // Şəkil servisi xətası user silinməsini pozmasın.
        }
    }
}