using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Profile;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Enums;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;
using System.Security.Claims;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _context;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IFileService _fileService;

    public ProfileController(
        UserManager<AppUser> userManager,
        AppDbContext context,
        IWhatsAppService whatsAppService,
        IFileService fileService)
    {
        _userManager = userManager;
        _context = context;
        _whatsAppService = whatsAppService;
        _fileService = fileService;
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException();

        return Guid.Parse(userId);
    }

    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetUserId();

        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user == null)
            return NotFound(ApiResponse<string>.Fail("İstifadəçi tapılmadı"));

        var dto = new ProfileDto
        {
            Id = user.Id,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber ?? "",
            Email = user.Email,
            DateOfBirth = user.DateOfBirth,
            ProfileImageUrl = user.ProfileImageUrl,
            LoyaltyCardCode = user.LoyaltyCardCode
        };

        return Ok(ApiResponse<ProfileDto>.Ok(dto));
    }

    [HttpPut]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileDto dto)
    {
        var userId = GetUserId();

        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user == null)
            return NotFound(ApiResponse<string>.Fail("İstifadəçi tapılmadı"));

        if (!string.IsNullOrWhiteSpace(dto.FullName))
            user.FullName = dto.FullName;

        if (dto.Email != null)
            user.Email = dto.Email;

        if (dto.DateOfBirth.HasValue)
            user.DateOfBirth = dto.DateOfBirth;

        if (dto.LoyaltyCardCode != null)
            user.LoyaltyCardCode = dto.LoyaltyCardCode;

        if (dto.ProfileImage != null)
        {
            if (!string.IsNullOrWhiteSpace(user.ProfileImageUrl))
            {
                await _fileService.DeleteImageAsync(user.ProfileImageUrl);
            }

            user.ProfileImageUrl = await _fileService.UploadImageAsync(
                dto.ProfileImage,
                "profiles");
        }

        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return Ok(ApiResponse<string>.Ok("Profil uğurla yeniləndi"));
    }

    [HttpPost("send-change-phone-otp")]
    public async Task<IActionResult> SendChangePhoneOtp(SendChangePhoneOtpDto dto)
    {
        var existingUser = await _userManager.FindByNameAsync(dto.NewPhoneNumber);

        if (existingUser != null)
            return BadRequest(ApiResponse<string>.Fail("Bu nömrə artıq istifadə olunur"));

        var code = Random.Shared.Next(100000, 999999).ToString();

        var otp = new UserOtpCode
        {
            PhoneNumber = dto.NewPhoneNumber,
            Code = code,
            Purpose = OtpPurpose.ChangePhoneNumber,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        _context.UserOtpCodes.Add(otp);
        await _context.SaveChangesAsync();

        var sent = await _whatsAppService.SendOtpAsync(dto.NewPhoneNumber, code);

        if (!sent)
            return BadRequest(ApiResponse<string>.Fail("WhatsApp kodu göndərilmədi"));

        return Ok(ApiResponse<string>.Ok("Yeni nömrəyə təsdiq kodu göndərildi"));
    }

    [HttpPost("verify-change-phone")]
    public async Task<IActionResult> VerifyChangePhone(VerifyChangePhoneDto dto)
    {
        var userId = GetUserId();

        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user == null)
            return NotFound(ApiResponse<string>.Fail("İstifadəçi tapılmadı"));

        var existingUser = await _userManager.FindByNameAsync(dto.NewPhoneNumber);

        if (existingUser != null && existingUser.Id != user.Id)
            return BadRequest(ApiResponse<string>.Fail("Bu nömrə artıq istifadə olunur"));

        var otp = await _context.UserOtpCodes
            .Where(x =>
                x.PhoneNumber == dto.NewPhoneNumber &&
                x.Code == dto.Code &&
                x.Purpose == OtpPurpose.ChangePhoneNumber &&
                !x.IsUsed &&
                x.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp == null)
            return BadRequest(ApiResponse<string>.Fail("Təsdiq kodu yanlışdır və ya vaxtı bitib"));

        user.PhoneNumber = dto.NewPhoneNumber;
        user.UserName = dto.NewPhoneNumber;
        user.UpdatedAt = DateTime.UtcNow;

        otp.IsUsed = true;
        otp.UsedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Telefon nömrəsi uğurla dəyişdirildi"));
    }

    [HttpGet("addresses")]
    public async Task<IActionResult> GetAddresses()
    {
        var userId = GetUserId();

        var addresses = await _context.UserAddresses
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new UserAddressDto
            {
                Id = x.Id,
                Title = x.Title,
                AddressText = x.AddressText,
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                BuildingNumber = x.BuildingNumber,
                Floor = x.Floor,
                Apartment = x.Apartment,
                Note = x.Note,
                IsDefault = x.IsDefault
            })
            .ToListAsync();

        return Ok(ApiResponse<List<UserAddressDto>>.Ok(addresses));
    }

    [HttpPost("addresses")]
    public async Task<IActionResult> CreateAddress(UserAddressCreateDto dto)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(dto.AddressText))
            return BadRequest(ApiResponse<string>.Fail("Ünvan boş ola bilməz"));

        if (dto.Latitude == 0 || dto.Longitude == 0)
            return BadRequest(ApiResponse<string>.Fail("Xəritədən konum seçilməlidir"));

        if (dto.IsDefault)
        {
            var oldDefaults = await _context.UserAddresses
                .Where(x => x.UserId == userId && x.IsDefault)
                .ToListAsync();

            foreach (var address in oldDefaults)
                address.IsDefault = false;
        }

        var addressEntity = new UserAddress
        {
            UserId = userId,
            Title = string.IsNullOrWhiteSpace(dto.Title) ? "Ünvan" : dto.Title,
            AddressText = dto.AddressText,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            BuildingNumber = dto.BuildingNumber,
            Floor = dto.Floor,
            Apartment = dto.Apartment,
            Note = dto.Note,
            IsDefault = dto.IsDefault
        };

        _context.UserAddresses.Add(addressEntity);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<Guid>.Ok(addressEntity.Id, "Ünvan yadda saxlanıldı"));
    }

    [HttpPut("addresses/{id}")]
    public async Task<IActionResult> UpdateAddress(Guid id, UserAddressCreateDto dto)
    {
        var userId = GetUserId();

        var address = await _context.UserAddresses
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (address == null)
            return NotFound(ApiResponse<string>.Fail("Ünvan tapılmadı"));

        if (string.IsNullOrWhiteSpace(dto.AddressText))
            return BadRequest(ApiResponse<string>.Fail("Ünvan boş ola bilməz"));

        if (dto.Latitude == 0 || dto.Longitude == 0)
            return BadRequest(ApiResponse<string>.Fail("Xəritədən konum seçilməlidir"));

        if (dto.IsDefault)
        {
            var oldDefaults = await _context.UserAddresses
                .Where(x => x.UserId == userId && x.Id != id && x.IsDefault)
                .ToListAsync();

            foreach (var item in oldDefaults)
                item.IsDefault = false;
        }

        address.Title = string.IsNullOrWhiteSpace(dto.Title) ? "Ünvan" : dto.Title;
        address.AddressText = dto.AddressText;
        address.Latitude = dto.Latitude;
        address.Longitude = dto.Longitude;
        address.BuildingNumber = dto.BuildingNumber;
        address.Floor = dto.Floor;
        address.Apartment = dto.Apartment;
        address.Note = dto.Note;
        address.IsDefault = dto.IsDefault;
        address.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Ünvan yeniləndi"));
    }

    [HttpPut("addresses/{id}/default")]
    public async Task<IActionResult> SetDefaultAddress(Guid id)
    {
        var userId = GetUserId();

        var address = await _context.UserAddresses
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (address == null)
            return NotFound(ApiResponse<string>.Fail("Ünvan tapılmadı"));

        var allAddresses = await _context.UserAddresses
            .Where(x => x.UserId == userId)
            .ToListAsync();

        foreach (var item in allAddresses)
            item.IsDefault = false;

        address.IsDefault = true;
        address.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Default ünvan seçildi"));
    }

    [HttpDelete("addresses/{id}")]
    public async Task<IActionResult> DeleteAddress(Guid id)
    {
        var userId = GetUserId();

        var address = await _context.UserAddresses
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (address == null)
            return NotFound(ApiResponse<string>.Fail("Ünvan tapılmadı"));

        address.IsDeleted = true;
        address.IsDefault = false;
        address.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Ünvan silindi"));
    }

}