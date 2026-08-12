using FluentValidation;
using NemesisBakuApi.DTOs.Profile;

namespace NemesisBakuApi.Validations;

public class UpdateProfileDtoValidator
    : AbstractValidator<UpdateProfileDto>
{
    public UpdateProfileDtoValidator()
    {
        RuleFor(x => x.FullName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
                .When(x => x.FullName != null)
                .WithMessage(
                    "Ad soyad boş ola bilməz")
            .MaximumLength(150)
                .When(x => x.FullName != null)
                .WithMessage(
                    "Ad soyad maksimum 150 simvol " +
                    "ola bilər")
            .Must(HaveEnoughCharacters)
                .When(x =>
                    !string.IsNullOrWhiteSpace(
                        x.FullName))
                .WithMessage(
                    "Ad soyad düzgün daxil edilməlidir");

        RuleFor(x => x.PhoneNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
                .When(x => x.PhoneNumber != null)
                .WithMessage(
                    "Telefon nömrəsi boş ola bilməz")
            .MaximumLength(30)
                .When(x => x.PhoneNumber != null)
                .WithMessage(
                    "Telefon nömrəsi çox uzundur")
            .Must(BeValidPhoneNumber)
                .When(x =>
                    !string.IsNullOrWhiteSpace(
                        x.PhoneNumber))
                .WithMessage(
                    "Telefon nömrəsi düzgün deyil");

        RuleFor(x => x.DateOfBirth)
            .LessThanOrEqualTo(DateTime.Today)
                .When(x => x.DateOfBirth.HasValue)
                .WithMessage(
                    "Doğum tarixi gələcək tarix " +
                    "ola bilməz");

        RuleFor(x => x.LoyaltyCardCode)
            .MaximumLength(100)
                .When(x =>
                    !string.IsNullOrWhiteSpace(
                        x.LoyaltyCardCode))
                .WithMessage(
                    "Loyallıq kartı kodu maksimum " +
                    "100 simvol ola bilər");
    }

    private static bool HaveEnoughCharacters(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value
            .Trim()
            .Count(char.IsLetterOrDigit) >= 2;
    }

    private static bool BeValidPhoneNumber(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var digits = new string(
            value.Where(char.IsDigit).ToArray());

        return digits.Length is >= 9 and <= 15;
    }
}