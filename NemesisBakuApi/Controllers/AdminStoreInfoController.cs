using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Store;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
public class AdminStoreInfoController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IFileService _fileService;

    public AdminStoreInfoController(
        AppDbContext context,
        IFileService fileService)
    {
        _context = context;
        _fileService = fileService;
    }

    [HttpPut]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update(
        [FromForm] StoreInfoUpdateDto dto,
        CancellationToken cancellationToken)
    {
        var store = await _context.StoreInfos
            .FirstOrDefaultAsync(cancellationToken);

        if (store == null)
        {
            store = new StoreInfo();
            _context.StoreInfos.Add(store);
        }

        var oldLogoUrl = store.LogoUrl;
        string? newLogoUrl = null;

        if (dto.LogoFile != null)
        {
            newLogoUrl = await _fileService.UploadImageAsync(
                dto.LogoFile,
                "store");

            store.LogoUrl = newLogoUrl;
        }

        ApplyChanges(store, dto);
        store.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync(
                cancellationToken);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(newLogoUrl))
            {
                await TryDeleteImageAsync(newLogoUrl);
            }

            throw;
        }

        if (newLogoUrl != null &&
            !string.IsNullOrWhiteSpace(oldLogoUrl))
        {
            await TryDeleteImageAsync(oldLogoUrl);
        }

        return Ok(
            ApiResponse<StoreInfoDto>.Ok(
                ToDto(store),
                "Store məlumatları yeniləndi"));
    }

    private static void ApplyChanges(
        StoreInfo store,
        StoreInfoUpdateDto dto)
    {
        store.StoreName = dto.StoreName;
        store.Slogan = dto.Slogan;

        store.AboutTitle = dto.AboutTitle;
        store.AboutContent = dto.AboutContent;

        store.MissionContent = dto.MissionContent;
        store.VisionContent = dto.VisionContent;
        store.WhyChooseUsContent =
            dto.WhyChooseUsContent;

        store.ReturnPolicyTitle =
            dto.ReturnPolicyTitle;

        store.ReturnPolicyContent =
            dto.ReturnPolicyContent;

        store.ExchangePolicyContent =
            dto.ExchangePolicyContent;

        store.ReturnExceptionsContent =
            dto.ReturnExceptionsContent;

        store.ReturnProcessContent =
            dto.ReturnProcessContent;

        store.DeliveryTitle = dto.DeliveryTitle;
        store.DeliveryContent = dto.DeliveryContent;
        store.DeliveryBakuText =
            dto.DeliveryBakuText;

        store.DeliveryAbsheronSumgaitText =
            dto.DeliveryAbsheronSumgaitText;

        store.DeliveryRegionsText =
            dto.DeliveryRegionsText;

        store.PaymentAndCheckText =
            dto.PaymentAndCheckText;

        store.PhoneNumber = dto.PhoneNumber;
        store.WhatsAppNumber = dto.WhatsAppNumber;
        store.Email = dto.Email;

        store.Address = dto.Address;
        store.Latitude = dto.Latitude;
        store.Longitude = dto.Longitude;

        store.WorkingHours = dto.WorkingHours;

        store.InstagramUrl = dto.InstagramUrl;
        store.TikTokUrl = dto.TikTokUrl;
        store.FacebookUrl = dto.FacebookUrl;
    }

    private static StoreInfoDto ToDto(
        StoreInfo store)
    {
        return new StoreInfoDto
        {
            Id = store.Id,
            StoreName = store.StoreName,
            Slogan = store.Slogan,
            LogoUrl = store.LogoUrl,

            AboutTitle = store.AboutTitle,
            AboutContent = store.AboutContent,

            MissionContent = store.MissionContent,
            VisionContent = store.VisionContent,

            WhyChooseUsContent =
                store.WhyChooseUsContent,

            ReturnPolicyTitle =
                store.ReturnPolicyTitle,

            ReturnPolicyContent =
                store.ReturnPolicyContent,

            ExchangePolicyContent =
                store.ExchangePolicyContent,

            ReturnExceptionsContent =
                store.ReturnExceptionsContent,

            ReturnProcessContent =
                store.ReturnProcessContent,

            DeliveryTitle = store.DeliveryTitle,
            DeliveryContent = store.DeliveryContent,

            DeliveryBakuText =
                store.DeliveryBakuText,

            DeliveryAbsheronSumgaitText =
                store.DeliveryAbsheronSumgaitText,

            DeliveryRegionsText =
                store.DeliveryRegionsText,

            PaymentAndCheckText =
                store.PaymentAndCheckText,

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
            // Cloudinary xətası update-i pozmasın.
        }
    }
}