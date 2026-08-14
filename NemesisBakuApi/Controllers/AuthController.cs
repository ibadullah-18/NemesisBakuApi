using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private const int MaximumOtpAttempts = 5;

    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly JwtTokenGenerator _jwtTokenGenerator;
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IFileService _fileService;
    private readonly OtpCodeHasher _otpCodeHasher;

    public AuthController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        JwtTokenGenerator jwtTokenGenerator,
        AppDbContext context,
        IEmailService emailService,
        IFileService fileService,
        OtpCodeHasher otpCodeHasher)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _context = context;
        _emailService = emailService;
        _fileService = fileService;
        _otpCodeHasher = otpCodeHasher;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginDto dto,
        CancellationToken cancellationToken)
    {
        var login = dto.EmailOrPhoneNumber.Trim();

        AppUser? user = login.Contains('@')
            ? await _userManager.FindByEmailAsync(
                login.ToLowerInvariant())
            : await _userManager.FindByNameAsync(
                NormalizePhone(login));

        if (user == null ||
            !user.IsActive ||
            user.IsDeleted)
        {
            return InvalidLogin();
        }

        var signInResult =
            await _signInManager
                .CheckPasswordSignInAsync(
                    user,
                    dto.Password,
                    lockoutOnFailure: true);

        if (!signInResult.Succeeded)
        {
            return InvalidLogin();
        }

        user.LastLoginAt = DateTime.UtcNow;

        var updateResult =
            await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            return BadRequest(updateResult.Errors);
        }

        var response = await CreateAuthResponseAsync(
            user,
            cancellationToken);

        return Ok(
            ApiResponse<AuthResponseDto>.Ok(response));
    }

    [HttpPost("send-register-otp")]
    [EnableRateLimiting("otp")]
    public async Task<IActionResult> SendRegisterOtp(
        SendOtpDto dto,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(dto.Email);

        var existingUser =
            await _userManager.FindByEmailAsync(email);

        if (existingUser != null)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Bu email artıq qeydiyyatdan keçib"));
        }

        return await CreateAndSendOtpAsync(
            email,
            email,
            OtpPurpose.Register,
            "Təsdiq kodu emailə göndərildi",
            cancellationToken);
    }

    [HttpPost("verify-register-otp")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> VerifyRegisterOtp(
        [FromForm] VerifyRegisterOtpDto dto,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(dto.Email);
        var phone = NormalizePhone(dto.PhoneNumber);

        var existingPhone =
            await _userManager.FindByNameAsync(phone);

        if (existingPhone != null)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Bu nömrə artıq qeydiyyatdan keçib"));
        }

        var existingEmail =
            await _userManager.FindByEmailAsync(email);

        if (existingEmail != null)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Bu email artıq qeydiyyatdan keçib"));
        }

        var otp = await FindAndVerifyOtpAsync(
            email,
            OtpPurpose.Register,
            dto.Code,
            cancellationToken);

        if (otp == null)
        {
            return InvalidOtp();
        }

        string? profileImageUrl = null;

        if (dto.ProfileImage != null)
        {
            profileImageUrl =
                await _fileService.UploadImageAsync(
                    dto.ProfileImage,
                    "profiles");
        }

        var user = new AppUser
        {
            FullName = dto.FullName.Trim(),
            UserName = phone,
            PhoneNumber = phone,
            Email = email,
            DateOfBirth = dto.DateOfBirth,

            LoyaltyCardCode =
                string.IsNullOrWhiteSpace(
                    dto.LoyaltyCardCode)
                    ? null
                    : dto.LoyaltyCardCode.Trim(),

            ProfileImageUrl = profileImageUrl,
            TermsAccepted = true,
            TermsAcceptedAt = DateTime.UtcNow,
            EmailConfirmed = true,
            IsActive = true
        };

        var createResult =
            await _userManager.CreateAsync(
                user,
                dto.Password);

        if (!createResult.Succeeded)
        {
            if (!string.IsNullOrWhiteSpace(
                    profileImageUrl))
            {
                await TryDeleteImageAsync(
                    profileImageUrl);
            }

            return BadRequest(createResult.Errors);
        }

        var roleResult =
            await _userManager.AddToRoleAsync(
                user,
                "User");

        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);

            if (!string.IsNullOrWhiteSpace(
                    profileImageUrl))
            {
                await TryDeleteImageAsync(
                    profileImageUrl);
            }

            return BadRequest(roleResult.Errors);
        }

        MarkOtpAsUsed(otp);

        await _context.SaveChangesAsync(
            cancellationToken);

        await _emailService.SendWelcomeAsync(
            email,
            user.FullName);

        return Ok(
            ApiResponse<string>.Ok(
                "Qeydiyyat uğurla tamamlandı"));
    }

    [HttpPost("send-forgot-password-otp")]
    [EnableRateLimiting("otp")]
    public async Task<IActionResult>
        SendForgotPasswordOtp(
            SendOtpDto dto,
            CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(dto.Email);

        var user =
            await _userManager.FindByEmailAsync(email);

        if (user == null ||
            user.IsDeleted ||
            !user.IsActive)
        {
            // Email-in sistemdə olub-olmadığını
            // kənar şəxslərə göstərmirik.
            return Ok(
                ApiResponse<string>.Ok(
                    "Email sistemdə mövcuddursa, " +
                    "şifrə yeniləmə kodu göndərildi"));
        }

        return await CreateAndSendOtpAsync(
            email,
            email,
            OtpPurpose.ForgotPassword,
            "Şifrə yeniləmə kodu emailə göndərildi",
            cancellationToken);
    }

    [HttpPost("reset-password-with-otp")]
    public async Task<IActionResult>
        ResetPasswordWithOtp(
            ResetPasswordWithOtpDto dto,
            CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(dto.Email);

        var user =
            await _userManager.FindByEmailAsync(email);

        if (user == null ||
            user.IsDeleted ||
            !user.IsActive)
        {
            return InvalidOtp();
        }

        var otp = await FindAndVerifyOtpAsync(
            email,
            OtpPurpose.ForgotPassword,
            dto.Code,
            cancellationToken);

        if (otp == null)
        {
            return InvalidOtp();
        }

        var resetToken =
            await _userManager
                .GeneratePasswordResetTokenAsync(user);

        var resetResult =
            await _userManager.ResetPasswordAsync(
                user,
                resetToken,
                dto.NewPassword);

        if (!resetResult.Succeeded)
        {
            return BadRequest(resetResult.Errors);
        }

        MarkOtpAsUsed(otp);

        await RevokeActiveRefreshTokensAsync(
            user.Id,
            cancellationToken);

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(
            ApiResponse<string>.Ok(
                "Şifrə uğurla yeniləndi"));
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken(
        RefreshTokenDto dto,
        CancellationToken cancellationToken)
    {
        var tokenHash =
            RefreshTokenGenerator.Hash(
                dto.RefreshToken);

        var replacementRawToken =
            RefreshTokenGenerator.Generate();

        var replacementTokenHash =
            RefreshTokenGenerator.Hash(
                replacementRawToken);

        try
        {
            return await _context
                .ExecuteResilientTransactionAsync<IActionResult>(
                async transactionCancellationToken =>
                {
                    var existingReplacement =
                        await _context.RefreshTokens
                            .Include(x => x.User)
                            .FirstOrDefaultAsync(
                                x =>
                                    x.TokenHash ==
                                    replacementTokenHash,
                                transactionCancellationToken);

                    if (existingReplacement != null &&
                        existingReplacement.User != null &&
                        existingReplacement.User.IsActive &&
                        !existingReplacement.User.IsDeleted)
                    {
                        var existingResponse =
                            await BuildAuthResponseAsync(
                                existingReplacement.User,
                                replacementRawToken);

                        return Ok(
                            ApiResponse<AuthResponseDto>.Ok(
                                existingResponse));
                    }

                    var refreshToken =
                        await _context.RefreshTokens
                            .Include(x => x.User)
                            .FirstOrDefaultAsync(
                                x => x.TokenHash == tokenHash,
                                transactionCancellationToken);

                    if (refreshToken == null)
                    {
                        return InvalidRefreshToken();
                    }

                    var user = refreshToken.User;

                    if (user == null ||
                        !user.IsActive ||
                        user.IsDeleted)
                    {
                        refreshToken.IsRevoked = true;
                        refreshToken.RevokedAt = DateTime.UtcNow;

                        await _context.SaveChangesAsync(
                            transactionCancellationToken);

                        return Unauthorized(
                            ApiResponse<string>.Fail(
                                "Hesab aktiv deyil"));
                    }

                    if (refreshToken.IsRevoked ||
                        refreshToken.IsUsed ||
                        refreshToken.ExpiresAt <= DateTime.UtcNow)
                    {
                        return InvalidRefreshToken();
                    }

                    refreshToken.IsUsed = true;
                    refreshToken.UsedAt = DateTime.UtcNow;

                    var response = await CreateAuthResponseAsync(
                        user,
                        transactionCancellationToken,
                        saveChanges: false,
                        rawRefreshToken:
                            replacementRawToken);

                    await _context.SaveChangesAsync(
                        transactionCancellationToken);

                    return Ok(
                        ApiResponse<AuthResponseDto>.Ok(response));
                },
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();

            return InvalidRefreshToken();
        }
    }

    private async Task<AuthResponseDto>
        CreateAuthResponseAsync(
            AppUser user,
            CancellationToken cancellationToken,
            bool saveChanges = true,
            string? rawRefreshToken = null)
    {
        rawRefreshToken ??=
            RefreshTokenGenerator.Generate();

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,

            TokenHash =
                RefreshTokenGenerator.Hash(
                    rawRefreshToken),

            ExpiresAt =
                DateTime.UtcNow.AddDays(30)
        };

        _context.RefreshTokens.Add(refreshToken);

        if (saveChanges)
        {
            await _context.SaveChangesAsync(
                cancellationToken);
        }

        return await BuildAuthResponseAsync(
            user,
            rawRefreshToken);
    }

    private async Task<AuthResponseDto>
        BuildAuthResponseAsync(
            AppUser user,
            string rawRefreshToken)
    {
        var accessToken =
            await _jwtTokenGenerator
                .GenerateTokenAsync(user);

        var roles =
            await _userManager.GetRolesAsync(user);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            UserId = user.Id.ToString(),
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber ?? "",
            Email = user.Email,
            Roles = roles
        };
    }

    private async Task<IActionResult>
        CreateAndSendOtpAsync(
            string otpIdentity,
            string recipientEmail,
            OtpPurpose purpose,
            string successMessage,
            CancellationToken cancellationToken)
    {
        await InvalidatePreviousOtpsAsync(
            otpIdentity,
            purpose,
            cancellationToken);

        var code = CreateOtpCode();

        var otp = new UserOtpCode
        {
            Email = otpIdentity,

            Code = _otpCodeHasher.Hash(
                otpIdentity,
                purpose,
                code),

            Purpose = purpose,
            FailedAttemptCount = 0,

            ExpiresAt =
                DateTime.UtcNow.AddMinutes(5)
        };

        _context.UserOtpCodes.Add(otp);

        await _context.SaveChangesAsync(
            cancellationToken);

        var sent = await _emailService.SendOtpAsync(
            recipientEmail,
            code);

        if (!sent)
        {
            MarkOtpAsUsed(otp);

            await _context.SaveChangesAsync(
                cancellationToken);

            return BadRequest(
                ApiResponse<string>.Fail(
                    "Email təsdiq kodu göndərilmədi"));
        }

        return Ok(
            ApiResponse<string>.Ok(successMessage));
    }

    private async Task<UserOtpCode?>
        FindAndVerifyOtpAsync(
            string email,
            OtpPurpose purpose,
            string code,
            CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var otp = await _context.UserOtpCodes
            .Where(x =>
                x.Email == email &&
                x.Purpose == purpose &&
                !x.IsUsed &&
                x.ExpiresAt > now)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (otp == null)
        {
            return null;
        }

        var isValid = _otpCodeHasher.Verify(
            email,
            purpose,
            code,
            otp.Code);

        if (isValid)
        {
            return otp;
        }

        otp.FailedAttemptCount++;

        if (otp.FailedAttemptCount >=
            MaximumOtpAttempts)
        {
            MarkOtpAsUsed(otp);
        }

        await _context.SaveChangesAsync(
            cancellationToken);

        return null;
    }

    private async Task InvalidatePreviousOtpsAsync(
        string email,
        OtpPurpose purpose,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        await _context.UserOtpCodes
            .Where(x =>
                x.Email == email &&
                x.Purpose == purpose &&
                !x.IsUsed)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.IsUsed,
                        true)
                    .SetProperty(
                        x => x.UsedAt,
                        now),
                cancellationToken);
    }

    private async Task
        RevokeActiveRefreshTokensAsync(
            Guid userId,
            CancellationToken cancellationToken)
    {
        await _context.RefreshTokens
            .Where(x =>
                x.UserId == userId &&
                !x.IsUsed &&
                !x.IsRevoked)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.IsRevoked,
                        true)
                    .SetProperty(
                        x => x.RevokedAt,
                        DateTime.UtcNow)
                    .SetProperty(
                        x => x.UpdatedAt,
                        DateTime.UtcNow),
                cancellationToken);
    }

    private static void MarkOtpAsUsed(
        UserOtpCode otp)
    {
        otp.IsUsed = true;
        otp.UsedAt = DateTime.UtcNow;
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
            // Şəkil təmizlənməsi auth prosesini pozmasın.
        }
    }

    private static string CreateOtpCode()
    {
        return RandomNumberGenerator
            .GetInt32(100000, 1000000)
            .ToString();
    }

    private static string NormalizeEmail(
        string email)
    {
        return email
            .Trim()
            .ToLowerInvariant();
    }

    private static string NormalizePhone(
        string phone)
    {
        return new string(
            phone.Where(char.IsDigit).ToArray());
    }

    private IActionResult InvalidLogin()
    {
        return Unauthorized(
            ApiResponse<string>.Fail(
                "Email/nömrə və ya şifrə yanlışdır"));
    }

    private IActionResult InvalidOtp()
    {
        return BadRequest(
            ApiResponse<string>.Fail(
                "Təsdiq kodu yanlışdır və ya " +
                "vaxtı bitib"));
    }

    private IActionResult InvalidRefreshToken()
    {
        return Unauthorized(
            ApiResponse<string>.Fail(
                "Refresh token yanlışdır və ya " +
                "vaxtı bitib"));
    }
}