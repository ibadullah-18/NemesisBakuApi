using FluentValidation;
using NemesisBakuApi.DTOs.Auth;

namespace NemesisBakuApi.Validations;

public class LoginDtoValidator
    : AbstractValidator<LoginDto>
{
    public LoginDtoValidator()
    {
        RuleFor(x => x.EmailOrPhoneNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
                .WithMessage(
                    "Email və ya telefon nömrəsi " +
                    "boş ola bilməz")
            .MaximumLength(256)
                .WithMessage(
                    "Email və ya telefon nömrəsi " +
                    "çox uzundur")
            .Must(BeValidLoginValue)
                .WithMessage(
                    "Email və ya telefon nömrəsi " +
                    "düzgün deyil");

        RuleFor(x => x.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
                .WithMessage(
                    "Şifrə boş ola bilməz")
            .MinimumLength(6)
                .WithMessage(
                    "Şifrə minimum 6 simvol olmalıdır")
            .MaximumLength(100)
                .WithMessage(
                    "Şifrə maksimum 100 simvol " +
                    "ola bilər");
    }

    private static bool BeValidLoginValue(
        string value)
    {
        var trimmedValue = value.Trim();

        if (trimmedValue.Contains('@'))
        {
            return BeValidEmail(trimmedValue);
        }

        var digits = new string(
            trimmedValue
                .Where(char.IsDigit)
                .ToArray());

        return digits.Length is >= 9 and <= 15;
    }

    private static bool BeValidEmail(
        string value)
    {
        try
        {
            var address =
                new System.Net.Mail.MailAddress(
                    value);

            return address.Address.Equals(
                value,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}