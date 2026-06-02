using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Store;
using NemesisBakuApi.Helpers;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StoreInfoController : ControllerBase
{
    private readonly AppDbContext _context;

    public StoreInfoController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var store = await _context.StoreInfos
            .FirstOrDefaultAsync();

        if (store == null)
            return NotFound(ApiResponse<string>.Fail("Store məlumatı tapılmadı"));

        var dto = new StoreInfoDto
        {
            Id = store.Id,

            StoreName = store.StoreName,
            Slogan = store.Slogan,
            LogoUrl = store.LogoUrl,

            AboutTitle = store.AboutTitle,
            AboutContent = store.AboutContent,

            MissionContent = store.MissionContent,
            VisionContent = store.VisionContent,
            WhyChooseUsContent = store.WhyChooseUsContent,

            ReturnPolicyTitle = store.ReturnPolicyTitle,
            ReturnPolicyContent = store.ReturnPolicyContent,
            ExchangePolicyContent = store.ExchangePolicyContent,
            ReturnExceptionsContent = store.ReturnExceptionsContent,
            ReturnProcessContent = store.ReturnProcessContent,

            DeliveryTitle = store.DeliveryTitle,
            DeliveryContent = store.DeliveryContent,
            DeliveryBakuText = store.DeliveryBakuText,
            DeliveryAbsheronSumgaitText = store.DeliveryAbsheronSumgaitText,
            DeliveryRegionsText = store.DeliveryRegionsText,
            PaymentAndCheckText = store.PaymentAndCheckText,

            PhoneNumber = store.PhoneNumber,
            WhatsAppNumber = store.WhatsAppNumber,
            Email = store.Email,

            Address = store.Address,
            Latitude = store.Latitude,
            Longitude = store.Longitude,

            WorkingHours = store.WorkingHours,

            InstagramUrl = store.InstagramUrl,
            TikTokUrl = store.TikTokUrl,
            FacebookUrl = store.FacebookUrl
        };

        return Ok(ApiResponse<StoreInfoDto>.Ok(dto));
    }
}