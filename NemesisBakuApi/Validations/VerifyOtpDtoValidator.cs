using FluentValidation;
using NemesisBakuApi.DTOs.Auth;

namespace NemesisBakuApi.Validations;

public class VerifyOtpDtoValidator
    : AbstractValidator<VerifyOtpDto>
{
    public VerifyOtpDtoValidator()
    {
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
    }
}