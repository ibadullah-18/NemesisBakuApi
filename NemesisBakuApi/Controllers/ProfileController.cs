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

    public ProfileController(
        UserManager<AppUser> userManager,
        AppDbContext context,
        IWhatsAppService whatsAppService)
    {
        _userManager = userManager;
        _context = context;
        _whatsAppService = whatsAppService;
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
    public async Task<IActionResult> UpdateProfile(UpdateProfileDto dto)
    {
        var userId = GetUserId();

        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user == null)
            return NotFound(ApiResponse<string>.Fail("İstifadəçi tapılmadı"));

        user.FullName = dto.FullName;
        user.Email = dto.Email;
        user.DateOfBirth = dto.DateOfBirth;
        user.LoyaltyCardCode = dto.LoyaltyCardCode;
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

}