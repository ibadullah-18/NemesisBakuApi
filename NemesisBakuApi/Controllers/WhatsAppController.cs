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
    private const string SiteUrl =
        "https://nemesisbaku.az";

    private readonly AppDbContext _context;

    public WhatsAppController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("product-inquiry/{productId:guid}")]
    public async Task<IActionResult> GetProductInquiryLink(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var phoneNumber = await GetStorePhoneAsync(
            cancellationToken);

        if (phoneNumber == null)
        {
            return InvalidStorePhone();
        }

        var product = await _context.Products
            .AsNoTracking()
            .Where(x =>
                x.Id == productId &&
                x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.ProductCode
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (product == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Məhsul tapılmadı"));
        }

        var productLink =
            $"{SiteUrl}/products/{product.Id}";

        var message =
            "Salam, bu məhsul haqqında məlumat " +
            "almaq istəyirəm:\n" +
            $"{product.Name}\n" +
            $"Kod: {product.ProductCode}\n" +
            $"Link: {productLink}";

        var userId = GetUserIdOrNull();
        var ipAddress = HttpContext.Connection
            .RemoteIpAddress?
            .ToString();

        var userAgent =
            Request.Headers.UserAgent.ToString();

        _context.WhatsAppProductInquiries.Add(
            new WhatsAppProductInquiry
            {
                UserId = userId,
                ProductId = product.Id,
                ProductLink = productLink,
                SellerPhoneNumber = phoneNumber,
                IpAddress = ipAddress,
                UserAgent = userAgent
            });

        _context.WhatsAppClickLogs.Add(
            new WhatsAppClickLog
            {
                UserId = userId,
                ProductId = product.Id,
                PageUrl = productLink,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                ClickType = "ProductInquiry"
            });

        await _context.SaveChangesAsync(
            cancellationToken);

        return WhatsAppResult(
            phoneNumber,
            message);
    }

    [Authorize]
    [HttpGet("basket-link")]
    public async Task<IActionResult> GetBasketWhatsAppLink(
        CancellationToken cancellationToken)
    {
        var phoneNumber = await GetStorePhoneAsync(
            cancellationToken);

        if (phoneNumber == null)
        {
            return InvalidStorePhone();
        }

        var userId = GetUserIdOrNull();

        if (!userId.HasValue)
        {
            return Unauthorized(
                ApiResponse<string>.Fail(
                    "Giriş edilməyib"));
        }

        var basketItems = await _context.BasketItems
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .Select(x => new
            {
                x.ProductId,
                x.Product.Name,
                x.Product.ProductCode,
                SizeValue =
                    x.ProductVariant.Size.Value,
                ColorName =
                    x.ProductVariant.Color.Name,
                x.Quantity,
                x.Product.Price,
                x.Product.DiscountPrice
            })
            .ToListAsync(cancellationToken);

        if (basketItems.Count == 0)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Səbət boşdur"));
        }

        var message = new StringBuilder();

        message.AppendLine(
            "Salam, bu məhsullarla maraqlanıram:");

        message.AppendLine();

        foreach (var item in basketItems)
        {
            var price =
                item.DiscountPrice.HasValue &&
                item.DiscountPrice.Value > 0 &&
                item.DiscountPrice.Value < item.Price
                    ? item.DiscountPrice.Value
                    : item.Price;

            message.AppendLine(
                $"Məhsul: {item.Name}");

            message.AppendLine(
                $"Kod: {item.ProductCode}");

            message.AppendLine(
                $"Razmer: {item.SizeValue}");

            message.AppendLine(
                $"Rəng: {item.ColorName}");

            message.AppendLine(
                $"Say: {item.Quantity}");

            message.AppendLine(
                $"Qiymət: {price:0.##} AZN");

            message.AppendLine(
                $"Link: {SiteUrl}/products/" +
                item.ProductId);

            message.AppendLine();
        }

        _context.WhatsAppClickLogs.Add(
            new WhatsAppClickLog
            {
                UserId = userId,
                PageUrl = "Basket",

                IpAddress = HttpContext.Connection
                    .RemoteIpAddress?
                    .ToString(),

                UserAgent =
                    Request.Headers.UserAgent
                        .ToString(),

                ClickType = "BasketInquiry"
            });

        await _context.SaveChangesAsync(
            cancellationToken);

        return WhatsAppResult(
            phoneNumber,
            message.ToString());
    }

    private async Task<string?> GetStorePhoneAsync(
        CancellationToken cancellationToken)
    {
        var value = await _context.StoreInfos
            .AsNoTracking()
            .Select(x => x.WhatsAppNumber)
            .FirstOrDefaultAsync(cancellationToken);

        return NormalizeWhatsAppNumber(value);
    }

    private Guid? GetUserIdOrNull()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId)
            ? userId
            : null;
    }

    private IActionResult InvalidStorePhone()
    {
        return BadRequest(
            ApiResponse<string>.Fail(
                "Mağaza WhatsApp nömrəsi düzgün " +
                "beynəlxalq formatda deyil"));
    }

    private IActionResult WhatsAppResult(
        string phoneNumber,
        string message)
    {
        var url =
            $"https://wa.me/{phoneNumber}?text=" +
            Uri.EscapeDataString(message);

        return Ok(
            ApiResponse<WhatsAppLinkDto>.Ok(
                new WhatsAppLinkDto
                {
                    Url = url
                }));
    }

    private static string? NormalizeWhatsAppNumber(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(
            value.Where(char.IsDigit).ToArray());

        if (digits.StartsWith(
                "00",
                StringComparison.Ordinal))
        {
            digits = digits[2..];
        }

        if (digits.Length == 10 &&
            digits.StartsWith('0'))
        {
            digits =
                AzerbaijanCountryCode + digits[1..];
        }
        else if (digits.Length == 9)
        {
            digits =
                AzerbaijanCountryCode + digits;
        }

        return digits.Length is >= 10 and <= 15
            ? digits
            : null;
    }
}