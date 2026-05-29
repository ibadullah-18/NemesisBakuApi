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
    private readonly IWhatsAppService _whatsAppService;

    public AuthController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        JwtTokenGenerator jwtTokenGenerator,
        AppDbContext context,
        IWhatsAppService whatsAppService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _context = context;
        _whatsAppService = whatsAppService;
    }


    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        if (dto.Password != dto.ConfirmPassword)
        {
            return BadRequest(ApiResponse<string>.Fail("Şifrələr uyğun deyil"));
        }

        var existingUser = await _userManager.FindByNameAsync(dto.PhoneNumber);

        if (existingUser != null)
        {
            return BadRequest(ApiResponse<string>.Fail("Bu nömrə artıq qeydiyyatdan keçib"));
        }

        var user = new AppUser
        {
            FullName = dto.FullName,
            UserName = dto.PhoneNumber,
            PhoneNumber = dto.PhoneNumber,
            Email = dto.Email,
            DateOfBirth = dto.DateOfBirth,
            LoyaltyCardCode = dto.LoyaltyCardCode
        };

        var result = await _userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        await _userManager.AddToRoleAsync(user, "User");

        return Ok(ApiResponse<string>.Ok(
            "Qeydiyyat uğurla tamamlandı"));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var user = await _userManager.FindByNameAsync(dto.PhoneNumber);

        if (user == null)
        {
            return Unauthorized(ApiResponse<string>.Fail("Nömrə və ya şifrə yanlışdır"));
        }
        if (!user.IsActive || user.IsDeleted)
        {
            return Unauthorized(ApiResponse<string>.Fail("Hesab aktiv deyil"));
        }

        var result = await _signInManager.CheckPasswordSignInAsync(
            user,
            dto.Password,
            false);

        if (!result.Succeeded)
        {
            return Unauthorized(ApiResponse<string>.Fail("Nömrə və ya şifrə yanlışdır"));
        }

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
        var existingUser = await _userManager.FindByNameAsync(dto.PhoneNumber);

        if (existingUser != null)
        {
            return BadRequest(ApiResponse<string>.Fail("Bu nömrə artıq qeydiyyatdan keçib"));
        }

        var code = new Random().Next(100000, 999999).ToString();

        var otp = new UserOtpCode
        {
            PhoneNumber = dto.PhoneNumber,
            Code = code,
            Purpose = OtpPurpose.Register,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        _context.UserOtpCodes.Add(otp);
        await _context.SaveChangesAsync();

        var sent = await _whatsAppService.SendOtpAsync(dto.PhoneNumber, code);

        if (!sent)
        {
            return BadRequest(ApiResponse<string>.Fail("WhatsApp kodu göndərilmədi"));
        }

        return Ok(ApiResponse<string>.Ok("Təsdiq kodu WhatsApp-a göndərildi"));
    }

    [HttpPost("verify-register-otp")]
    public async Task<IActionResult> VerifyRegisterOtp(VerifyRegisterOtpDto dto)
    {
        if (dto.Password != dto.ConfirmPassword)
        {
            return BadRequest(ApiResponse<string>.Fail("Şifrələr uyğun deyil"));
        }

        var existingUser = await _userManager.FindByNameAsync(dto.PhoneNumber);

        if (existingUser != null)
        {
            return BadRequest(ApiResponse<string>.Fail("Bu nömrə artıq qeydiyyatdan keçib"));
        }

        var otp = await _context.UserOtpCodes
            .Where(x =>
                x.PhoneNumber == dto.PhoneNumber &&
                x.Code == dto.Code &&
                x.Purpose == OtpPurpose.Register &&
                !x.IsUsed &&
                x.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp == null)
        {
            return BadRequest(ApiResponse<string>.Fail("Təsdiq kodu yanlışdır və ya vaxtı bitib"));
        }

        var user = new AppUser
        {
            FullName = dto.FullName,
            UserName = dto.PhoneNumber,
            PhoneNumber = dto.PhoneNumber,
            Email = dto.Email,
            DateOfBirth = dto.DateOfBirth,
            LoyaltyCardCode = dto.LoyaltyCardCode
        };

        var result = await _userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        await _userManager.AddToRoleAsync(user, "User");

        otp.IsUsed = true;
        otp.UsedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Qeydiyyat uğurla tamamlandı"));
    }

    [HttpPost("send-forgot-password-otp")]
    public async Task<IActionResult> SendForgotPasswordOtp(SendOtpDto dto)
    {
        var user = await _userManager.FindByNameAsync(dto.PhoneNumber);

        if (user == null)
        {
            return BadRequest(ApiResponse<string>.Fail("Bu nömrəli istifadəçi tapılmadı"));
        }

        var code = new Random().Next(100000, 999999).ToString();

        var otp = new UserOtpCode
        {
            PhoneNumber = dto.PhoneNumber,
            Code = code,
            Purpose = OtpPurpose.ForgotPassword,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        _context.UserOtpCodes.Add(otp);
        await _context.SaveChangesAsync();

        var sent = await _whatsAppService.SendOtpAsync(dto.PhoneNumber, code);

        if (!sent)
        {
            return BadRequest(ApiResponse<string>.Fail("WhatsApp kodu göndərilmədi"));
        }

        return Ok(ApiResponse<string>.Ok("Şifrə yeniləmə kodu WhatsApp-a göndərildi"));
    }

    [HttpPost("reset-password-with-otp")]
    public async Task<IActionResult> ResetPasswordWithOtp(ResetPasswordWithOtpDto dto)
    {
        if (dto.NewPassword != dto.ConfirmNewPassword)
        {
            return BadRequest(ApiResponse<string>.Fail("Şifrələr uyğun deyil"));
        }

        var user = await _userManager.FindByNameAsync(dto.PhoneNumber);

        if (user == null)
        {
            return BadRequest(ApiResponse<string>.Fail("Bu nömrəli istifadəçi tapılmadı"));
        }

        var otp = await _context.UserOtpCodes
            .Where(x =>
                x.PhoneNumber == dto.PhoneNumber &&
                x.Code == dto.Code &&
                x.Purpose == OtpPurpose.ForgotPassword &&
                !x.IsUsed &&
                x.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp == null)
        {
            return BadRequest(ApiResponse<string>.Fail("Təsdiq kodu yanlışdır və ya vaxtı bitib"));
        }

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);

        var result = await _userManager.ResetPasswordAsync(
            user,
            resetToken,
            dto.NewPassword);

        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        otp.IsUsed = true;
        otp.UsedAt = DateTime.UtcNow;

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
        {
            return Unauthorized(ApiResponse<string>.Fail("Refresh token yanlışdır və ya vaxtı bitib"));
        }

        refreshToken.IsUsed = true;
        refreshToken.UsedAt = DateTime.UtcNow;

        var user = refreshToken.User;

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