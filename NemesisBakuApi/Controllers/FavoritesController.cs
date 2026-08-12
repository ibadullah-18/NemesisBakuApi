using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public FavoritesController(
        AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId()
    {
        var userIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
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

                ProductName =
                    x.Product.Name,

                ProductCode =
                    x.Product.ProductCode,

                Price =
                    x.Product.Price,

                DiscountPrice =
                    x.Product.DiscountPrice,

                IsDiscounted =
                    x.Product.IsDiscounted,

                MainImageUrl =
                    x.Product.Images
                        .OrderByDescending(
                            image => image.IsMain)
                        .ThenBy(
                            image => image.Order)
                        .Select(
                            image => image.ImageUrl)
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

        var favorite = await _context.Favorites
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                x =>
                    x.UserId == userId &&
                    x.ProductId == productId,
                cancellationToken);

        if (favorite == null)
        {
            _context.Favorites.Add(
                new Favorite
                {
                    UserId = userId,
                    ProductId = productId
                });

            await _context.SaveChangesAsync(
                cancellationToken);

            return Ok(
                ApiResponse<string>.Ok(
                    "Favorilərə əlavə edildi"));
        }

        favorite.IsDeleted = !favorite.IsDeleted;
        favorite.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(
            ApiResponse<string>.Ok(
                "Favori statusu dəyişdirildi"));
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

        var favorite = await _context.Favorites
            .FirstOrDefaultAsync(
                x =>
                    x.UserId == userId &&
                    x.ProductId == productId,
                cancellationToken);

        if (favorite == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Favorit tapılmadı"));
        }

        _context.Favorites.Remove(favorite);

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(
            ApiResponse<string>.Ok(
                "Favoritdən silindi"));
    }
}