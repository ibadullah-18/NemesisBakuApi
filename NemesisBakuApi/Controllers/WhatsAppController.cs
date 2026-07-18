using System;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.WhatsApp;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WhatsAppController : ControllerBase
{
    private const string AzerbaijanCountryCode = "994";
    private readonly AppDbContext _context;

    public WhatsAppController(AppDbContext context)
    {
        _context = context;
    }

    private Guid? GetUserIdOrNull()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static string? NormalizeWhatsAppNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var digits = new string(value.Where(char.IsDigit).ToArray());

        if (digits.StartsWith("00", StringComparison.Ordinal))
            digits = digits[2..];

        if (digits.Length == 10 && digits.StartsWith('0'))
            digits = AzerbaijanCountryCode + digits[1..];
        else if (digits.Length == 9)
            digits = AzerbaijanCountryCode + digits;

        return digits.Length is >= 10 and <= 15 ? digits : null;
    }

    private static string BuildWhatsAppUrl(string phoneNumber, string message)
    {
        return $"https://wa.me/{phoneNumber}?text={Uri.EscapeDataString(message)}";
    }

    private async Task<(StoreInfo? StoreInfo, string? PhoneNumber)> GetStoreWhatsAppAsync()
    {
        var storeInfo = await _context.StoreInfos
            .AsNoTracking()
            .FirstOrDefaultAsync();

        return (storeInfo, NormalizeWhatsAppNumber(storeInfo?.WhatsAppNumber));
    }

    [HttpGet("product-inquiry/{productId:guid}")]
    public async Task<IActionResult> GetProductInquiryLink(Guid productId)
    {
        var (_, phoneNumber) = await GetStoreWhatsAppAsync();

        if (phoneNumber == null)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Mağaza WhatsApp nömrəsi düzgün beynəlxalq formatda deyil"));
        }

        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == productId && x.IsActive);

        if (product == null)
            return NotFound(ApiResponse<string>.Fail("Məhsul tapılmadı"));

        var productLink = $"https://nemesisbaku.az/products/{product.Id}";

        var message =
            $"Salam, bu məhsul haqqında məlumat almaq istəyirəm:\n" +
            $"{product.Name}\n" +
            $"Kod: {product.ProductCode}\n" +
            $"Link: {productLink}";

        var url = BuildWhatsAppUrl(phoneNumber, message);
        var userId = GetUserIdOrNull();

        _context.WhatsAppProductInquiries.Add(new WhatsAppProductInquiry
        {
            UserId = userId,
            ProductId = product.Id,
            ProductLink = productLink,
            SellerPhoneNumber = phoneNumber,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        });

        _context.WhatsAppClickLogs.Add(new WhatsAppClickLog
        {
            UserId = userId,
            ProductId = product.Id,
            PageUrl = productLink,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            ClickType = "ProductInquiry"
        });

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<WhatsAppLinkDto>.Ok(new WhatsAppLinkDto
        {
            Url = url
        }));
    }

    [Authorize]
    [HttpGet("basket-link")]
    public async Task<IActionResult> GetBasketWhatsAppLink()
    {
        var (_, phoneNumber) = await GetStoreWhatsAppAsync();

        if (phoneNumber == null)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Mağaza WhatsApp nömrəsi düzgün beynəlxalq formatda deyil"));
        }

        var userId = GetUserIdOrNull();

        if (userId == null)
            return Unauthorized(ApiResponse<string>.Fail("Giriş edilməyib"));

        var basketItems = await _context.BasketItems
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.ProductVariant)
                .ThenInclude(v => v.Size)
            .Include(x => x.ProductVariant)
                .ThenInclude(v => v.Color)
            .Where(x => x.UserId == userId.Value)
            .ToListAsync();

        if (basketItems.Count == 0)
            return BadRequest(ApiResponse<string>.Fail("Səbət boşdur"));

        var message = new StringBuilder();

        message.AppendLine("Salam, bu məhsullarla maraqlanıram:");
        message.AppendLine();

        foreach (var item in basketItems)
        {
            var productLink = $"https://nemesisbaku.az/products/{item.ProductId}";

            var hasDiscount =
                item.Product.DiscountPrice.HasValue &&
                item.Product.DiscountPrice.Value > 0 &&
                item.Product.DiscountPrice.Value < item.Product.Price;

            var price = hasDiscount
                ? item.Product.DiscountPrice!.Value
                : item.Product.Price;

            message.AppendLine($"Məhsul: {item.Product.Name}");
            message.AppendLine($"Kod: {item.Product.ProductCode}");
            message.AppendLine($"Razmer: {item.ProductVariant.Size.Value}");
            message.AppendLine($"Rəng: {item.ProductVariant.Color.Name}");
            message.AppendLine($"Say: {item.Quantity}");
            message.AppendLine($"Qiymət: {price:0.##} AZN");
            message.AppendLine($"Link: {productLink}");
            message.AppendLine();
        }

        var url = BuildWhatsAppUrl(phoneNumber, message.ToString());

        _context.WhatsAppClickLogs.Add(new WhatsAppClickLog
        {
            UserId = userId,
            PageUrl = "Basket",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            ClickType = "BasketInquiry"
        });

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<WhatsAppLinkDto>.Ok(new WhatsAppLinkDto
        {
            Url = url
        }));
    }
}