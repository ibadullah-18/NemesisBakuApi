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

    public FavoritesController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException();

        return Guid.Parse(userId);
    }

    [HttpGet]
    public async Task<IActionResult> GetFavorites()
    {
        var userId = GetUserId();

        var favorites = await _context.Favorites
            .Include(x => x.Product)
                .ThenInclude(p => p.Images)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new FavoriteDto
            {
                ProductId = x.ProductId,
                ProductName = x.Product.Name,
                ProductCode = x.Product.ProductCode,

                Price = x.Product.Price,
                DiscountPrice = x.Product.DiscountPrice,
                IsDiscounted = x.Product.IsDiscounted,

                MainImageUrl = x.Product.Images
                    .OrderByDescending(i => i.IsMain)
                    .ThenBy(i => i.Order)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(ApiResponse<List<FavoriteDto>>.Ok(favorites));
    }

    [HttpPost("{productId}")]
    public async Task<IActionResult> ToggleFavorite(Guid productId)
    {
        var userId = GetUserId();

        var productExists = await _context.Products
            .AnyAsync(x => x.Id == productId && x.IsActive);

        if (!productExists)
            return NotFound(ApiResponse<string>.Fail("Məhsul tapılmadı"));

        var favorite = await _context.Favorites
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.ProductId == productId);

        if (favorite == null)
        {
            _context.Favorites.Add(new Favorite
            {
                UserId = userId,
                ProductId = productId
            });

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.Ok("Favorilərə əlavə edildi"));
        }

        if (favorite.IsDeleted)
        {
            favorite.IsDeleted = false;
            favorite.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            favorite.IsDeleted = true;
            favorite.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Favori statusu dəyişdirildi"));
    }

    [HttpGet("check/{productId}")]
    public async Task<IActionResult> CheckFavorite(Guid productId)
    {
        var userId = GetUserId();

        var exists = await _context.Favorites
            .AnyAsync(x =>
                x.UserId == userId &&
                x.ProductId == productId);

        return Ok(ApiResponse<bool>.Ok(exists));
    }

    [HttpDelete("{productId}")]
    [Authorize]
    public async Task<IActionResult> Remove(Guid productId)
    {
        var userId = GetUserId();

        var favorite = await _context.Favorites
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.ProductId == productId);

        if (favorite == null)
            return NotFound(ApiResponse<string>.Fail("Favorit tapılmadı"));

        _context.Favorites.Remove(favorite);

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Favoritdən silindi"));
    }
}