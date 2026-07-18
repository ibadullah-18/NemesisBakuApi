using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Auth;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Enums;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly JwtTokenGenerator _jwtTokenGenerator;
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IFileService _fileService;

    public AuthController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        JwtTokenGenerator jwtTokenGenerator,
        AppDbContext context,
        IEmailService emailService,
        IFileService fileService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _context = context;
        _emailService = emailService;
        _fileService = fileService;
    }

    private static string CreateOtpCode()
    {
        return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
    }

    private async Task InvalidatePreviousOtpsAsync(
        string email,
        OtpPurpose purpose)
    {
        var previousCodes = await _context.UserOtpCodes
            .Where(x =>
                x.Email == email &&
                x.Purpose == purpose &&
                !x.IsUsed)
            .ToListAsync();

        foreach (var previousCode in previousCodes)
        {
            previousCode.IsUsed = true;
            previousCode.UsedAt = DateTime.UtcNow;
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var login = dto.EmailOrPhoneNumber.Trim();

        AppUser? user = login.Contains("@")
            ? await _userManager.FindByEmailAsync(login)
            : await _userManager.FindByNameAsync(login);

        if (user == null)
            return Unauthorized(ApiResponse<string>.Fail("Email/nömrə və ya şifrə yanlışdır"));

        if (!user.IsActive || user.IsDeleted)
            return Unauthorized(ApiResponse<string>.Fail("Hesab aktiv deyil"));

        var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, false);

        if (!result.Succeeded)
            return Unauthorized(ApiResponse<string>.Fail("Email/nömrə və ya şifrə yanlışdır"));

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var accessToken = await _jwtTokenGenerator.GenerateTokenAsync(user);

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            Token = RefreshTokenGenerator.Generate(),
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        var roles = await _userManager.GetRolesAsync(user);

        var response = new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            UserId = user.Id.ToString(),
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber ?? "",
            Email = user.Email,
            Roles = roles
        };

        return Ok(ApiResponse<AuthResponseDto>.Ok(response));
    }

    [HttpPost("send-register-otp")]
    public async Task<IActionResult> SendRegisterOtp(SendOtpDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var existingUser = await _userManager.FindByEmailAsync(email);

        if (existingUser != null)
            return BadRequest(ApiResponse<string>.Fail("Bu email artıq qeydiyyatdan keçib"));

        await InvalidatePreviousOtpsAsync(email, OtpPurpose.Register);
        var code = CreateOtpCode();

        var otp = new UserOtpCode
        {
            Email = email,
            Code = code,
            Purpose = OtpPurpose.Register,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        _context.UserOtpCodes.Add(otp);
        await _context.SaveChangesAsync();

        var sent = await _emailService.SendOtpAsync(email, code);

        if (!sent)
        {
            otp.IsUsed = true;
            otp.UsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return BadRequest(ApiResponse<string>.Fail("Email təsdiq kodu göndərilmədi"));
        }

        return Ok(ApiResponse<string>.Ok("Təsdiq kodu emailə göndərildi"));
    }

    [HttpPost("verify-register-otp")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> VerifyRegisterOtp([FromForm] VerifyRegisterOtpDto dto)
    {
        if (!dto.TermsAccepted)
            return BadRequest(ApiResponse<string>.Fail("İstifadə şərtlərini qəbul etməlisiniz"));

        if (dto.Password != dto.ConfirmPassword)
            return BadRequest(ApiResponse<string>.Fail("Şifrələr uyğun deyil"));

        var existingPhone = await _userManager.FindByNameAsync(dto.PhoneNumber);

        if (existingPhone != null)
            return BadRequest(ApiResponse<string>.Fail("Bu nömrə artıq qeydiyyatdan keçib"));

        var email = dto.Email.Trim().ToLowerInvariant();
        var existingEmail = await _userManager.FindByEmailAsync(email);

        if (existingEmail != null)
            return BadRequest(ApiResponse<string>.Fail("Bu email artıq qeydiyyatdan keçib"));

        var otp = await _context.UserOtpCodes
            .Where(x =>
                x.Email == email &&
                x.Code == dto.Code &&
                x.Purpose == OtpPurpose.Register &&
                !x.IsUsed &&
                x.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp == null)
            return BadRequest(ApiResponse<string>.Fail("Təsdiq kodu yanlışdır və ya vaxtı bitib"));

        string? profileImageUrl = null;

        if (dto.ProfileImage != null)
        {
            try
            {
                profileImageUrl = await _fileService.UploadImageAsync(
                    dto.ProfileImage,
                    "profiles");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<string>.Fail(ex.Message));
            }
        }

        var user = new AppUser
        {
            FullName = dto.FullName,
            UserName = dto.PhoneNumber,
            PhoneNumber = dto.PhoneNumber,
            Email = email,
            DateOfBirth = dto.DateOfBirth,
            LoyaltyCardCode = dto.LoyaltyCardCode,
            ProfileImageUrl = profileImageUrl,
            TermsAccepted = true,
            TermsAcceptedAt = DateTime.UtcNow,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            if (!string.IsNullOrWhiteSpace(profileImageUrl))
                await _fileService.DeleteImageAsync(profileImageUrl);

            return BadRequest(result.Errors);
        }

        await _userManager.AddToRoleAsync(user, "User");

        otp.IsUsed = true;
        otp.UsedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _emailService.SendWelcomeAsync(email, dto.FullName);

        return Ok(ApiResponse<string>.Ok("Qeydiyyat uğurla tamamlandı"));
    }

    [HttpPost("send-forgot-password-otp")]
    public async Task<IActionResult> SendForgotPasswordOtp(SendOtpDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var user = await _userManager.FindByEmailAsync(email);

        if (user == null)
            return BadRequest(ApiResponse<string>.Fail("Bu email ilə istifadəçi tapılmadı"));

        await InvalidatePreviousOtpsAsync(email, OtpPurpose.ForgotPassword);
        var code = CreateOtpCode();

        var otp = new UserOtpCode
        {
            Email = email,
            Code = code,
            Purpose = OtpPurpose.ForgotPassword,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        _context.UserOtpCodes.Add(otp);
        await _context.SaveChangesAsync();

        var sent = await _emailService.SendOtpAsync(email, code);

        if (!sent)
        {
            otp.IsUsed = true;
            otp.UsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return BadRequest(ApiResponse<string>.Fail("Email təsdiq kodu göndərilmədi"));
        }

        return Ok(ApiResponse<string>.Ok("Şifrə yeniləmə kodu emailə göndərildi"));
    }

    [HttpPost("reset-password-with-otp")]
    public async Task<IActionResult> ResetPasswordWithOtp(ResetPasswordWithOtpDto dto)
    {
        if (dto.NewPassword != dto.ConfirmNewPassword)
            return BadRequest(ApiResponse<string>.Fail("Şifrələr uyğun deyil"));

        var email = dto.Email.Trim().ToLowerInvariant();

        var user = await _userManager.FindByEmailAsync(email);

        if (user == null)
            return BadRequest(ApiResponse<string>.Fail("Bu email ilə istifadəçi tapılmadı"));

        var otp = await _context.UserOtpCodes
            .Where(x =>
                x.Email == email &&
                x.Code == dto.Code &&
                x.Purpose == OtpPurpose.ForgotPassword &&
                !x.IsUsed &&
                x.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp == null)
            return BadRequest(ApiResponse<string>.Fail("Təsdiq kodu yanlışdır və ya vaxtı bitib"));

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);

        var result = await _userManager.ResetPasswordAsync(
            user,
            resetToken,
            dto.NewPassword);

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        otp.IsUsed = true;
        otp.UsedAt = DateTime.UtcNow;

        var activeRefreshTokens = await _context.RefreshTokens
            .Where(x =>
                x.UserId == user.Id &&
                !x.IsUsed &&
                !x.IsRevoked)
            .ToListAsync();

        foreach (var token in activeRefreshTokens)
            token.IsRevoked = true;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Şifrə uğurla yeniləndi"));
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken(RefreshTokenDto dto)
    {
        var refreshToken = await _context.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x =>
                x.Token == dto.RefreshToken &&
                !x.IsRevoked &&
                !x.IsUsed &&
                x.ExpiresAt > DateTime.UtcNow);

        if (refreshToken == null)
            return Unauthorized(ApiResponse<string>.Fail("Refresh token yanlışdır və ya vaxtı bitib"));

        refreshToken.IsUsed = true;
        refreshToken.UsedAt = DateTime.UtcNow;

        var user = refreshToken.User;

        if (user == null || !user.IsActive || user.IsDeleted)
        {
            refreshToken.IsRevoked = true;
            await _context.SaveChangesAsync();
            return Unauthorized(ApiResponse<string>.Fail("Hesab aktiv deyil"));
        }

        var newAccessToken = await _jwtTokenGenerator.GenerateTokenAsync(user);

        var newRefreshToken = new RefreshToken
        {
            UserId = user.Id,
            Token = RefreshTokenGenerator.Generate(),
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _context.RefreshTokens.Add(newRefreshToken);

        await _context.SaveChangesAsync();

        var roles = await _userManager.GetRolesAsync(user);

        var response = new AuthResponseDto
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken.Token,
            UserId = user.Id.ToString(),
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber ?? "",
            Email = user.Email,
            Roles = roles
        };

        return Ok(ApiResponse<AuthResponseDto>.Ok(response));
    }
}
