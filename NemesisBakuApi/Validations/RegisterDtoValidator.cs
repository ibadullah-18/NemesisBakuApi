using FluentValidation;
using NemesisBakuApi.DTOs.Auth;

namespace NemesisBakuApi.Validations;

public class RegisterDtoValidator
    : AbstractValidator<VerifyRegisterOtpDto>
{
    public RegisterDtoValidator()
    {
        RuleFor(x => x.FullName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
                .WithMessage(
                    "Ad soyad boş ola bilməz")
            .MaximumLength(150)
                .WithMessage(
                    "Ad soyad maksimum 150 simvol " +
                    "ola bilər")
            .Must(HaveAtLeastTwoCharacters)
                .WithMessage(
                    "Ad soyad düzgün daxil edilməlidir");

        RuleFor(x => x.PhoneNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
                .WithMessage(
                    "Telefon nömrəsi boş ola bilməz")
            .MaximumLength(30)
                .WithMessage(
                    "Telefon nömrəsi çox uzundur")
            .Must(BeValidPhoneNumber)
                .WithMessage(
                    "Telefon nömrəsi düzgün deyil");

        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
                .WithMessage(
                    "Email boş ola bilməz")
            .MaximumLength(256)
                .WithMessage(
                    "Email maksimum 256 simvol " +
                    "ola bilər")
            .EmailAddress()
                .WithMessage(
                    "Email formatı düzgün deyil");

        RuleFor(x => x.DateOfBirth)
            .NotEmpty()
                .WithMessage(
                    "Doğum tarixi daxil edilməlidir")
            .LessThanOrEqualTo(DateTime.Today)
                .WithMessage(
                    "Doğum tarixi gələcək tarix " +
                    "ola bilməz");

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

        RuleFor(x => x.ConfirmPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
                .WithMessage(
                    "Şifrə təkrarı boş ola bilməz")
            .Equal(x => x.Password)
                .WithMessage(
                    "Şifrələr uyğun deyil");

        RuleFor(x => x.Code)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
                .WithMessage(
                    "Təsdiq kodu boş ola bilməz")
            .Length(6)
                .WithMessage(
                    "Təsdiq kodu 6 rəqəm olmalıdır")
            .Matches(@"^\d{6}$")
                .WithMessage(
                    "Təsdiq kodu yalnız rəqəmlərdən " +
                    "ibarət olmalıdır");

        RuleFor(x => x.LoyaltyCardCode)
            .MaximumLength(100)
                .When(x =>
                    !string.IsNullOrWhiteSpace(
                        x.LoyaltyCardCode))
                .WithMessage(
                    "Loyallıq kartı kodu maksimum " +
                    "100 simvol ola bilər");

        RuleFor(x => x.TermsAccepted)
            .Equal(true)
                .WithMessage(
                    "İstifadə şərtlərini " +
                    "qəbul etməlisiniz");
    }

    private static bool HaveAtLeastTwoCharacters(
        string value)
    {
        return value
            .Trim()
            .Count(char.IsLetterOrDigit) >= 2;
    }

    private static bool BeValidPhoneNumber(
        string value)
    {
        var digits = new string(
            value.Where(char.IsDigit).ToArray());

        return digits.Length is >= 9 and <= 15;
    }
}