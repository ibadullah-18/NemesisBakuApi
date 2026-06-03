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
    private readonly AppDbContext _context;

    public WhatsAppController(AppDbContext context)
    {
        _context = context;
    }

    private Guid? GetUserIdOrNull()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return null;

        return Guid.Parse(userId);
    }

    [HttpGet("product-inquiry/{productId}")]
    public async Task<IActionResult> GetProductInquiryLink(Guid productId)
    {
        var storeInfo = await _context.StoreInfos.FirstOrDefaultAsync();

        if (storeInfo == null || string.IsNullOrWhiteSpace(storeInfo.WhatsAppNumber))
            return BadRequest(ApiResponse<string>.Fail("Mağaza WhatsApp nömrəsi təyin edilməyib"));

        var product = await _context.Products
            .Include(x => x.Brand)
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == productId && x.IsActive);

        if (product == null)
            return NotFound(ApiResponse<string>.Fail("Məhsul tapılmadı"));

        var productLink = $"https://nemesisbaku.az/products/{product.Id}";

        var message =
            $"Salam, bu məhsul haqqında məlumat almaq istəyirəm:\n" +
            $"{product.Name}\n" +
            $"Kod: {product.ProductCode}\n" +
            $"Link: {productLink}";

        var encodedMessage = Uri.EscapeDataString(message);

        var url = $"https://wa.me/{storeInfo.WhatsAppNumber}?text={encodedMessage}";

        _context.WhatsAppProductInquiries.Add(new WhatsAppProductInquiry
        {
            UserId = GetUserIdOrNull(),
            ProductId = product.Id,
            ProductLink = productLink,
            SellerPhoneNumber = storeInfo.WhatsAppNumber,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        });

        _context.WhatsAppClickLogs.Add(new WhatsAppClickLog
        {
            UserId = GetUserIdOrNull(),
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
        var storeInfo = await _context.StoreInfos.FirstOrDefaultAsync();

        if (storeInfo == null || string.IsNullOrWhiteSpace(storeInfo.WhatsAppNumber))
            return BadRequest(ApiResponse<string>.Fail("Mağaza WhatsApp nömrəsi təyin edilməyib"));

        var userId = GetUserIdOrNull();

        if (userId == null)
            return Unauthorized(ApiResponse<string>.Fail("Giriş edilməyib"));

        var basketItems = await _context.BasketItems
            .Include(x => x.Product)
            .Include(x => x.ProductVariant)
                .ThenInclude(v => v.Size)
            .Include(x => x.ProductVariant)
                .ThenInclude(v => v.Color)
            .Where(x => x.UserId == userId.Value)
            .ToListAsync();

        if (!basketItems.Any())
            return BadRequest(ApiResponse<string>.Fail("Səbət boşdur"));

        var sb = new StringBuilder();

        sb.AppendLine("Salam, bu məhsullarla maraqlanıram:");
        sb.AppendLine();

        foreach (var item in basketItems)
        {
            var productLink = $"https://nemesisbaku.az/products/{item.ProductId}";

            var price = item.Product.IsDiscounted && item.Product.DiscountPrice.HasValue
                ? item.Product.DiscountPrice.Value
                : item.Product.Price;

            sb.AppendLine($"Məhsul: {item.Product.Name}");
            sb.AppendLine($"Kod: {item.Product.ProductCode}");
            sb.AppendLine($"Razmer: {item.ProductVariant.Size.Value}");
            sb.AppendLine($"Rəng: {item.ProductVariant.Color.Name}");
            sb.AppendLine($"Say: {item.Quantity}");
            sb.AppendLine($"Qiymət: {price} AZN");
            sb.AppendLine($"Link: {productLink}");
            sb.AppendLine();
        }

        var encodedMessage = Uri.EscapeDataString(sb.ToString());

        var url = $"https://wa.me/{storeInfo.WhatsAppNumber}?text={encodedMessage}";

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