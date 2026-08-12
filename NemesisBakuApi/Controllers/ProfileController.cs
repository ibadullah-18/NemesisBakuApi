using System.Security.Claims;
using System.Security.Cryptography;
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

    [HttpGet]
    public async Task<IActionResult> GetProfile(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var profile = await _context.Users
            .AsNoTracking()
            .Where(x =>
                x.Id == userId &&
                !x.IsDeleted)
            .Select(x => new ProfileDto
            {
                Id = x.Id,
                FullName = x.FullName,
                PhoneNumber = x.PhoneNumber ?? "",
                Email = x.Email,
                DateOfBirth = x.DateOfBirth,
                ProfileImageUrl = x.ProfileImageUrl,
                LoyaltyCardCode = x.LoyaltyCardCode
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (profile == null)
        {
            return UserNotFound();
        }

        return Ok(
            ApiResponse<ProfileDto>.Ok(profile));
    }

    [HttpPut]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateProfile(
        [FromForm] UpdateProfileDto dto,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var user = await _context.Users
            .FirstOrDefaultAsync(
                x =>
                    x.Id == userId &&
                    !x.IsDeleted,
                cancellationToken);

        if (user == null)
        {
            return UserNotFound();
        }

        if (!string.IsNullOrWhiteSpace(
                dto.FullName))
        {
            user.FullName = dto.FullName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(
                dto.PhoneNumber))
        {
            var normalizedPhone =
                NormalizePhone(dto.PhoneNumber);

            var existingPhoneUser =
                await _userManager.FindByNameAsync(
                    normalizedPhone);

            if (existingPhoneUser != null &&
                existingPhoneUser.Id != user.Id)
            {
                return BadRequest(
                    ApiResponse<string>.Fail(
                        "Bu nömrə artıq istifadə olunur"));
            }

            user.PhoneNumber = normalizedPhone;
            user.UserName = normalizedPhone;

            user.NormalizedUserName =
                _userManager.NormalizeName(
                    normalizedPhone);
        }

        if (dto.DateOfBirth.HasValue)
        {
            user.DateOfBirth = dto.DateOfBirth;
        }

        if (dto.LoyaltyCardCode != null)
        {
            user.LoyaltyCardCode =
                string.IsNullOrWhiteSpace(
                    dto.LoyaltyCardCode)
                    ? null
                    : dto.LoyaltyCardCode.Trim();
        }

        var oldImageUrl = user.ProfileImageUrl;
        string? newImageUrl = null;

        if (dto.ProfileImage != null)
        {
            newImageUrl =
                await _fileService.UploadImageAsync(
                    dto.ProfileImage,
                    "profiles");

            user.ProfileImageUrl = newImageUrl;
        }

        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(
            user);

        if (!result.Succeeded)
        {
            if (!string.IsNullOrWhiteSpace(
                    newImageUrl))
            {
                await TryDeleteImageAsync(
                    newImageUrl);
            }

            return BadRequest(result.Errors);
        }

        if (newImageUrl != null &&
            !string.IsNullOrWhiteSpace(
                oldImageUrl))
        {
            await TryDeleteImageAsync(oldImageUrl);
        }

        return Ok(
            ApiResponse<string>.Ok(
                "Profil uğurla yeniləndi"));
    }

    [HttpPost("send-change-email-otp")]
    public async Task<IActionResult>
        SendChangeEmailOtp(
            SendChangeEmailOtpDto dto,
            CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.Id == userId &&
                    !x.IsDeleted,
                cancellationToken);

        if (user == null)
        {
            return UserNotFound();
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Hazırki email tapılmadı"));
        }

        var newEmail = NormalizeEmail(
            dto.NewEmail);

        if (user.Email.Equals(
                newEmail,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Yeni email hazırki email ilə eynidir"));
        }

        var existingEmailUser =
            await _userManager.FindByEmailAsync(
                newEmail);

        if (existingEmailUser != null &&
            existingEmailUser.Id != user.Id)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Bu email artıq istifadə olunur"));
        }

        await InvalidatePreviousChangeEmailOtpsAsync(
            newEmail,
            cancellationToken);

        var code = CreateOtpCode();

        var otp = new UserOtpCode
        {
            // OTP konkret yeni emailə bağlanır.
            Email = newEmail,
            Code = code,
            Purpose = OtpPurpose.ChangeEmail,
            ExpiresAt =
                DateTime.UtcNow.AddMinutes(5)
        };

        _context.UserOtpCodes.Add(otp);

        await _context.SaveChangesAsync(
            cancellationToken);

        var sent = await _emailService.SendOtpAsync(
            user.Email,
            code);

        if (!sent)
        {
            otp.IsUsed = true;
            otp.UsedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(
                cancellationToken);

            return BadRequest(
                ApiResponse<string>.Fail(
                    "Email təsdiq kodu göndərilmədi"));
        }

        return Ok(
            ApiResponse<string>.Ok(
                "Təsdiq kodu hazırki email " +
                "ünvanınıza göndərildi"));
    }

    [HttpPost("verify-change-email")]
    public async Task<IActionResult> VerifyChangeEmail(
        VerifyChangeEmailDto dto,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var user = await _context.Users
            .FirstOrDefaultAsync(
                x =>
                    x.Id == userId &&
                    !x.IsDeleted,
                cancellationToken);

        if (user == null)
        {
            return UserNotFound();
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Hazırki email tapılmadı"));
        }

        var newEmail = NormalizeEmail(
            dto.NewEmail);

        if (user.Email.Equals(
                newEmail,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Yeni email hazırki email ilə eynidir"));
        }

        var existingEmailUser =
            await _userManager.FindByEmailAsync(
                newEmail);

        if (existingEmailUser != null &&
            existingEmailUser.Id != user.Id)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Bu email artıq istifadə olunur"));
        }

        var now = DateTime.UtcNow;

        var otp = await _context.UserOtpCodes
            .Where(x =>
                x.Email == newEmail &&
                x.Code == dto.Code &&
                x.Purpose ==
                    OtpPurpose.ChangeEmail &&
                !x.IsUsed &&
                x.ExpiresAt > now)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (otp == null)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Təsdiq kodu yanlışdır və ya " +
                    "vaxtı bitib"));
        }

        var oldEmail = user.Email;

        user.Email = newEmail;
        user.NormalizedEmail =
            _userManager.NormalizeEmail(newEmail);

        user.EmailConfirmed = true;
        user.UpdatedAt = now;

        otp.IsUsed = true;
        otp.UsedAt = now;

        var updateResult =
            await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            // Update alınmasa OTP istifadə olunmuş
            // vəziyyətdə database-ə yazılmayacaq.
            return BadRequest(updateResult.Errors);
        }

        await _context.SaveChangesAsync(
            cancellationToken);

        await InvalidateOtherChangeEmailOtpsAsync(
            newEmail,
            otp.Id,
            cancellationToken);

        return Ok(
            ApiResponse<string>.Ok(
                "Email uğurla dəyişdirildi"));
    }

    [HttpGet("addresses")]
    public async Task<IActionResult> GetAddresses(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var addresses =
            await _context.UserAddresses
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(
                    x => x.IsDefault)
                .ThenByDescending(
                    x => x.CreatedAt)
                .Select(x => new UserAddressDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    AddressText = x.AddressText,
                    Latitude = x.Latitude,
                    Longitude = x.Longitude,

                    BuildingNumber =
                        x.BuildingNumber,

                    Floor = x.Floor,
                    Apartment = x.Apartment,
                    Note = x.Note,
                    IsDefault = x.IsDefault
                })
                .ToListAsync(cancellationToken);

        return Ok(
            ApiResponse<List<UserAddressDto>>
                .Ok(addresses));
    }

    [HttpPost("addresses")]
    public async Task<IActionResult> CreateAddress(
        UserAddressCreateDto dto,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (CoordinatesAreMissing(dto))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Xəritədən konum seçilməlidir"));
        }

        if (dto.IsDefault)
        {
            await ClearDefaultAddressesAsync(
                userId,
                null,
                cancellationToken);
        }

        var address = new UserAddress
        {
            UserId = userId,
            Title = GetAddressTitle(dto.Title),

            AddressText =
                dto.AddressText.Trim(),

            Latitude = dto.Latitude,
            Longitude = dto.Longitude,

            BuildingNumber =
                NormalizeOptional(dto.BuildingNumber),

            Floor = NormalizeOptional(dto.Floor),

            Apartment =
                NormalizeOptional(dto.Apartment),

            Note = NormalizeOptional(dto.Note),
            IsDefault = dto.IsDefault
        };

        _context.UserAddresses.Add(address);

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(
            ApiResponse<Guid>.Ok(
                address.Id,
                "Ünvan yadda saxlanıldı"));
    }

    [HttpPut("addresses/{id:guid}")]
    public async Task<IActionResult> UpdateAddress(
        Guid id,
        UserAddressCreateDto dto,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var address =
            await _context.UserAddresses
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == id &&
                        x.UserId == userId,
                    cancellationToken);

        if (address == null)
        {
            return AddressNotFound();
        }

        if (CoordinatesAreMissing(dto))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Xəritədən konum seçilməlidir"));
        }

        if (dto.IsDefault)
        {
            await ClearDefaultAddressesAsync(
                userId,
                id,
                cancellationToken);
        }

        address.Title =
            GetAddressTitle(dto.Title);

        address.AddressText =
            dto.AddressText.Trim();

        address.Latitude = dto.Latitude;
        address.Longitude = dto.Longitude;

        address.BuildingNumber =
            NormalizeOptional(dto.BuildingNumber);

        address.Floor =
            NormalizeOptional(dto.Floor);

        address.Apartment =
            NormalizeOptional(dto.Apartment);

        address.Note =
            NormalizeOptional(dto.Note);

        address.IsDefault = dto.IsDefault;
        address.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(
            ApiResponse<string>.Ok(
                "Ünvan yeniləndi"));
    }

    [HttpPut("addresses/{id:guid}/default")]
    public async Task<IActionResult>
        SetDefaultAddress(
            Guid id,
            CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var address =
            await _context.UserAddresses
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == id &&
                        x.UserId == userId,
                    cancellationToken);

        if (address == null)
        {
            return AddressNotFound();
        }

        await ClearDefaultAddressesAsync(
            userId,
            id,
            cancellationToken);

        address.IsDefault = true;
        address.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(
            ApiResponse<string>.Ok(
                "Default ünvan seçildi"));
    }

    [HttpDelete("addresses/{id:guid}")]
    public async Task<IActionResult> DeleteAddress(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var address =
            await _context.UserAddresses
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == id &&
                        x.UserId == userId,
                    cancellationToken);

        if (address == null)
        {
            return AddressNotFound();
        }

        address.IsDeleted = true;
        address.IsDefault = false;
        address.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(
            ApiResponse<string>.Ok(
                "Ünvan silindi"));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new UnauthorizedAccessException();
        }

        return userId;
    }

    private async Task
        InvalidatePreviousChangeEmailOtpsAsync(
            string newEmail,
            CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        await _context.UserOtpCodes
            .Where(x =>
                x.Email == newEmail &&
                x.Purpose ==
                    OtpPurpose.ChangeEmail &&
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
        InvalidateOtherChangeEmailOtpsAsync(
            string email,
            Guid usedOtpId,
            CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        await _context.UserOtpCodes
            .Where(x =>
                x.Id != usedOtpId &&
                x.Email == email &&
                x.Purpose ==
                    OtpPurpose.ChangeEmail &&
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

    private async Task ClearDefaultAddressesAsync(
        Guid userId,
        Guid? excludedAddressId,
        CancellationToken cancellationToken)
    {
        var query = _context.UserAddresses
            .Where(x =>
                x.UserId == userId &&
                x.IsDefault);

        if (excludedAddressId.HasValue)
        {
            query = query.Where(
                x => x.Id !=
                     excludedAddressId.Value);
        }

        await query.ExecuteUpdateAsync(
            setters => setters
                .SetProperty(
                    x => x.IsDefault,
                    false)
                .SetProperty(
                    x => x.UpdatedAt,
                    DateTime.UtcNow),
            cancellationToken);
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
            // Cloudinary xətası profil update-ni pozmasın.
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
        var digits = new string(
            phone.Where(char.IsDigit).ToArray());

        if (digits.Length == 10 &&
            digits.StartsWith('0'))
        {
            return digits;
        }

        if (digits.Length == 12 &&
            digits.StartsWith("994"))
        {
            return digits;
        }

        return digits;
    }

    private static string GetAddressTitle(
        string? title)
    {
        return string.IsNullOrWhiteSpace(title)
            ? "Ünvan"
            : title.Trim();
    }

    private static string? NormalizeOptional(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static bool CoordinatesAreMissing(
        UserAddressCreateDto dto)
    {
        return
            dto.Latitude == 0 ||
            dto.Longitude == 0;
    }

    private IActionResult UserNotFound()
    {
        return NotFound(
            ApiResponse<string>.Fail(
                "İstifadəçi tapılmadı"));
    }

    private IActionResult AddressNotFound()
    {
        return NotFound(
            ApiResponse<string>.Fail(
                "Ünvan tapılmadı"));
    }
}