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
    private readonly IFileService _fileService;
    private readonly IEmailService _emailService;

    public ProfileController(
        UserManager<AppUser> userManager,
        AppDbContext context,
        IFileService fileService,
        IEmailService emailService)
    {
        _userManager = userManager;
        _context = context;
        _fileService = fileService;
        _emailService = emailService;
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
            user.FullName = dto.FullName.Trim();

        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
        {
            var normalizedPhone = NormalizePhone(dto.PhoneNumber);

            var existingPhoneUser = await _userManager.FindByNameAsync(normalizedPhone);

            if (existingPhoneUser != null && existingPhoneUser.Id != user.Id)
                return BadRequest(ApiResponse<string>.Fail("Bu nömrə artıq istifadə olunur"));

            user.PhoneNumber = normalizedPhone;
            user.UserName = normalizedPhone;
            user.NormalizedUserName = normalizedPhone.ToUpper();
        }

        if (dto.DateOfBirth.HasValue)
            user.DateOfBirth = dto.DateOfBirth;

        if (dto.LoyaltyCardCode != null)
            user.LoyaltyCardCode = dto.LoyaltyCardCode;

        if (dto.ProfileImage != null)
        {
            if (!string.IsNullOrWhiteSpace(user.ProfileImageUrl))
                await _fileService.DeleteImageAsync(user.ProfileImageUrl);

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

    [HttpPost("send-change-email-otp")]
    public async Task<IActionResult> SendChangeEmailOtp(SendChangeEmailOtpDto dto)
    {
        var userId = GetUserId();

        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user == null)
            return NotFound(ApiResponse<string>.Fail("İstifadəçi tapılmadı"));

        if (string.IsNullOrWhiteSpace(user.Email))
            return BadRequest(ApiResponse<string>.Fail("Hazırki email tapılmadı"));

        if (string.IsNullOrWhiteSpace(dto.NewEmail))
            return BadRequest(ApiResponse<string>.Fail("Yeni email boş ola bilməz"));

        var newEmail = dto.NewEmail.Trim();

        if (user.Email.Equals(newEmail, StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<string>.Fail("Yeni email hazırki email ilə eynidir"));

        var existingEmailUser = await _userManager.FindByEmailAsync(newEmail);

        if (existingEmailUser != null && existingEmailUser.Id != user.Id)
            return BadRequest(ApiResponse<string>.Fail("Bu email artıq istifadə olunur"));

        var code = Random.Shared.Next(100000, 999999).ToString();

        var otp = new UserOtpCode
        {
            Email = user.Email,
            Code = code,
            Purpose = OtpPurpose.ChangeEmail,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        _context.UserOtpCodes.Add(otp);
        await _context.SaveChangesAsync();

        var sent = await _emailService.SendOtpAsync(user.Email, code);

        if (!sent)
            return BadRequest(ApiResponse<string>.Fail("Email təsdiq kodu göndərilmədi"));

        return Ok(ApiResponse<string>.Ok("Təsdiq kodu hazırki email ünvanınıza göndərildi"));
    }

    [HttpPost("verify-change-email")]
    public async Task<IActionResult> VerifyChangeEmail(VerifyChangeEmailDto dto)
    {
        var userId = GetUserId();

        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user == null)
            return NotFound(ApiResponse<string>.Fail("İstifadəçi tapılmadı"));

        if (string.IsNullOrWhiteSpace(user.Email))
            return BadRequest(ApiResponse<string>.Fail("Hazırki email tapılmadı"));

        if (string.IsNullOrWhiteSpace(dto.NewEmail))
            return BadRequest(ApiResponse<string>.Fail("Yeni email boş ola bilməz"));

        var newEmail = dto.NewEmail.Trim();

        var existingEmailUser = await _userManager.FindByEmailAsync(newEmail);

        if (existingEmailUser != null && existingEmailUser.Id != user.Id)
            return BadRequest(ApiResponse<string>.Fail("Bu email artıq istifadə olunur"));

        var otp = await _context.UserOtpCodes
            .Where(x =>
                x.Email == user.Email &&
                x.Code == dto.Code &&
                x.Purpose == OtpPurpose.ChangeEmail &&
                !x.IsUsed &&
                x.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp == null)
            return BadRequest(ApiResponse<string>.Fail("Təsdiq kodu yanlışdır və ya vaxtı bitib"));

        user.Email = newEmail;
        user.NormalizedEmail = newEmail.ToUpper();
        user.EmailConfirmed = true;
        user.UpdatedAt = DateTime.UtcNow;

        otp.IsUsed = true;
        otp.UsedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Email uğurla dəyişdirildi"));
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

        if (dto.Latitude < -90 || dto.Latitude > 90)
            return BadRequest(ApiResponse<string>.Fail("Latitude düzgün deyil"));

        if (dto.Longitude < -180 || dto.Longitude > 180)
            return BadRequest(ApiResponse<string>.Fail("Longitude düzgün deyil"));

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

        if (dto.Latitude < -90 || dto.Latitude > 90)
            return BadRequest(ApiResponse<string>.Fail("Latitude düzgün deyil"));

        if (dto.Longitude < -180 || dto.Longitude > 180)
            return BadRequest(ApiResponse<string>.Fail("Longitude düzgün deyil"));

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

    private static string NormalizePhone(string phone)
    {
        return phone
            .Replace("+", "")
            .Replace(" ", "")
            .Replace("(", "")
            .Replace(")", "")
            .Replace("-", "");
    }
}