using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Favorite;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FavoritesController : ControllerBase
{
    private readonly AppDbContext _context;

    public FavoritesController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new UnauthorizedAccessException();
        }

        return userId;
    }

    [HttpGet]
    public async Task<IActionResult> GetFavorites(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var favorites = await _context.Favorites
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new FavoriteDto
            {
                ProductId = x.ProductId,
                ProductName = x.Product.Name,
                ProductCode = x.Product.ProductCode,
                Price = x.Product.Price,

                DiscountPrice =
                    x.Product.DiscountPrice,

                IsDiscounted =
                    x.Product.IsDiscounted,

                MainImageUrl = x.Product.Images
                    .OrderByDescending(
                        image => image.IsMain)
                    .ThenBy(image => image.Order)
                    .Select(image => image.ImageUrl)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return Ok(
            ApiResponse<List<FavoriteDto>>
                .Ok(favorites));
    }

    [HttpPost("{productId:guid}")]
    public async Task<IActionResult> ToggleFavorite(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var productExists =
            await _context.Products
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.Id == productId &&
                        x.IsActive,
                    cancellationToken);

        if (!productExists)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Məhsul tapılmadı"));
        }

        var toggledRows =
            await ToggleExistingAsync(
                userId,
                productId,
                cancellationToken);

        if (toggledRows > 0)
        {
            return FavoriteToggleSuccess();
        }

        _context.Favorites.Add(
            new Favorite
            {
                UserId = userId,
                ProductId = productId
            });

        try
        {
            await _context.SaveChangesAsync(
                cancellationToken);

            return Ok(
                ApiResponse<string>.Ok(
                    "Favorilərə əlavə edildi"));
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            // Paralel sorğu favorini yaradıbsa,
            // ikinci sorğunu atomik tamamlayırıq.
            _context.ChangeTracker.Clear();

            await ToggleExistingAsync(
                userId,
                productId,
                cancellationToken);

            return FavoriteToggleSuccess();
        }
    }

    [HttpGet("check/{productId:guid}")]
    public async Task<IActionResult> CheckFavorite(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var exists = await _context.Favorites
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.UserId == userId &&
                    x.ProductId == productId,
                cancellationToken);

        return Ok(
            ApiResponse<bool>.Ok(exists));
    }

    [HttpDelete("{productId:guid}")]
    public async Task<IActionResult> Remove(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var updatedRows =
            await _context.Favorites
                .Where(x =>
                    x.UserId == userId &&
                    x.ProductId == productId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            x => x.IsDeleted,
                            true)
                        .SetProperty(
                            x => x.UpdatedAt,
                            DateTime.UtcNow),
                    cancellationToken);

        if (updatedRows == 0)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Favorit tapılmadı"));
        }

        return Ok(
            ApiResponse<string>.Ok(
                "Favoritdən silindi"));
    }

    private Task<int> ToggleExistingAsync(
        Guid userId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        return _context.Favorites
            .IgnoreQueryFilters()
            .Where(x =>
                x.UserId == userId &&
                x.ProductId == productId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.IsDeleted,
                        x => !x.IsDeleted)
                    .SetProperty(
                        x => x.UpdatedAt,
                        DateTime.UtcNow),
                cancellationToken);
    }

    private IActionResult FavoriteToggleSuccess()
    {
        return Ok(
            ApiResponse<string>.Ok(
                "Favori statusu dəyişdirildi"));
    }

    private static bool IsUniqueConstraintViolation(
        DbUpdateException exception)
    {
        return exception.InnerException is SqlException
        {
            Number: 2601 or 2627
        };
    }
}