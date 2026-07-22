using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;
using NemesisBakuApi.Settings;

namespace NemesisBakuApi.Controllers;

[ApiController]
[Route("api/telegram")]
public class TelegramController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ITelegramBotService _telegramBotService;
    private readonly TelegramSettings _settings;
    private readonly ILogger<TelegramController> _logger;

    public TelegramController(
        UserManager<AppUser> userManager,
        ITelegramBotService telegramBotService,
        IOptions<TelegramSettings> options,
        ILogger<TelegramController> logger)
    {
        _userManager = userManager;
        _telegramBotService = telegramBotService;
        _settings = options.Value;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(
        [FromBody] JsonElement update,
        CancellationToken cancellationToken)
    {
        if (!IsValidWebhookSecret())
            return Unauthorized();

        if (!update.TryGetProperty("message", out var message))
            return Ok();

        if (!TryGetInt64(message, "chat", "id", out var chatId))
            return Ok();

        if (!TryGetInt64(message, "from", "id", out var telegramUserId))
            return Ok();

        if (message.TryGetProperty("text", out var textElement))
        {
            var text = textElement.GetString();

            if (!string.IsNullOrWhiteSpace(text) &&
                text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
            {
                await _telegramBotService.SendContactRequestAsync(
                    chatId,
                    cancellationToken);

                return Ok();
            }
        }

        if (!message.TryGetProperty("contact", out var contact))
            return Ok();

        if (!contact.TryGetProperty("user_id", out var contactUserIdElement) ||
            !contactUserIdElement.TryGetInt64(out var contactUserId) ||
            contactUserId != telegramUserId)
        {
            await _telegramBotService.SendTextAsync(
                chatId,
                "Yalnız öz telefon nömrənizi paylaşa bilərsiniz.",
                cancellationToken: cancellationToken);

            return Ok();
        }

        var phoneNumber = contact.TryGetProperty(
            "phone_number",
            out var phoneElement)
                ? NormalizePhone(phoneElement.GetString())
                : null;

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            await _telegramBotService.SendTextAsync(
                chatId,
                "Telefon nömrəsi oxunmadı. Yenidən cəhd edin.",
                cancellationToken: cancellationToken);

            return Ok();
        }

        var phoneSuffix = phoneNumber[^Math.Min(9, phoneNumber.Length)..];

        var candidates = await _userManager.Users
            .Where(x =>
                !x.IsDeleted &&
                x.IsActive &&
                x.PhoneNumber != null &&
                x.PhoneNumber.EndsWith(phoneSuffix))
            .ToListAsync(cancellationToken);

        var user = candidates.FirstOrDefault(x =>
            NormalizePhone(x.PhoneNumber) == phoneNumber);

        if (user == null)
        {
            await _telegramBotService.SendTextAsync(
                chatId,
                "Bu telefon nömrəsi ilə aktiv nemesisbaku admin hesabı tapılmadı.",
                removeKeyboard: true,
                cancellationToken: cancellationToken);

            return Ok();
        }

        var roles = await _userManager.GetRolesAsync(user);

        if (!roles.Contains("Admin") && !roles.Contains("SuperAdmin"))
        {
            await _telegramBotService.SendTextAsync(
                chatId,
                "Bu bot yalnız Admin və SuperAdmin hesabları üçündür.",
                removeKeyboard: true,
                cancellationToken: cancellationToken);

            return Ok();
        }

        var chatOwner = await _userManager.Users
            .FirstOrDefaultAsync(
                x => x.TelegramChatId == chatId && x.Id != user.Id,
                cancellationToken);

        if (chatOwner != null)
        {
            await _telegramBotService.SendTextAsync(
                chatId,
                "Bu Telegram hesabı artıq başqa admin hesabına qoşulub.",
                removeKeyboard: true,
                cancellationToken: cancellationToken);

            return Ok();
        }

        user.TelegramChatId = chatId;
        user.TelegramUsername = TryGetString(message, "from", "username");
        user.TelegramNotificationsEnabled = true;
        user.TelegramLinkedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        var updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            _logger.LogError(
                "Telegram hesabı adminə bağlanmadı. UserId: {UserId}, Errors: {Errors}",
                user.Id,
                string.Join(
                    ", ",
                    updateResult.Errors.Select(x => x.Description)));

            await _telegramBotService.SendTextAsync(
                chatId,
                "Telegram bağlantısı yaradılmadı. Bir qədər sonra yenidən cəhd edin.",
                removeKeyboard: true,
                cancellationToken: cancellationToken);

            return Ok();
        }

        await _telegramBotService.SendTextAsync(
            chatId,
            $"Hazırdır, {user.FullName}. Yeni sifariş bildirişləri bu çata gələcək.",
            removeKeyboard: true,
            cancellationToken: cancellationToken);

        return Ok();
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var user = await GetCurrentUserAsync();

        if (user == null)
            return Unauthorized(ApiResponse<string>.Fail("İstifadəçi tapılmadı"));

        return Ok(ApiResponse<object>.Ok(new
        {
            isLinked = user.TelegramChatId.HasValue,
            notificationsEnabled = user.TelegramNotificationsEnabled,
            linkedAt = user.TelegramLinkedAt,
            botUrl = $"https://t.me/{_settings.BotUsername.TrimStart('@')}?start=admin"
        }));
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpDelete("connection")]
    public async Task<IActionResult> Disconnect()
    {
        var user = await GetCurrentUserAsync();

        if (user == null)
            return Unauthorized(ApiResponse<string>.Fail("İstifadəçi tapılmadı"));

        user.TelegramChatId = null;
        user.TelegramUsername = null;
        user.TelegramNotificationsEnabled = false;
        user.TelegramLinkedAt = null;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
            return BadRequest(ApiResponse<string>.Fail("Telegram bağlantısı silinmədi"));

        return Ok(ApiResponse<string>.Ok("Telegram bağlantısı silindi"));
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
            return null;

        return await _userManager.Users.FirstOrDefaultAsync(x => x.Id == userId);
    }

    private bool IsValidWebhookSecret()
    {
        if (!_telegramBotService.IsConfigured)
            return false;

        var receivedSecret = Request.Headers[
            "X-Telegram-Bot-Api-Secret-Token"].ToString();

        return !string.IsNullOrWhiteSpace(receivedSecret) &&
               string.Equals(
                   receivedSecret,
                   _settings.WebhookSecret,
                   StringComparison.Ordinal);
    }

    private static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var digits = new string(value.Where(char.IsDigit).ToArray());

        if (digits.StartsWith("00994"))
            digits = digits[2..];

        if (digits.StartsWith("994") && digits.Length == 12)
            return digits;

        if (digits.Length == 10 && digits.StartsWith('0'))
            return $"994{digits[1..]}";

        if (digits.Length == 9)
            return $"994{digits}";

        return digits;
    }

    private static bool TryGetInt64(
        JsonElement root,
        string parentName,
        string propertyName,
        out long value)
    {
        value = default;

        return root.TryGetProperty(parentName, out var parent) &&
               parent.TryGetProperty(propertyName, out var property) &&
               property.TryGetInt64(out value);
    }

    private static string? TryGetString(
        JsonElement root,
        string parentName,
        string propertyName)
    {
        return root.TryGetProperty(parentName, out var parent) &&
               parent.TryGetProperty(propertyName, out var property)
            ? property.GetString()
            : null;
    }
}
