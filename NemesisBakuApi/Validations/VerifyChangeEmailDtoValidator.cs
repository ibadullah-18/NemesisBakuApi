using FluentValidation;
using NemesisBakuApi.DTOs.Profile;

namespace NemesisBakuApi.Validations;

public class VerifyChangeEmailDtoValidator
    : AbstractValidator<VerifyChangeEmailDto>
{
    public VerifyChangeEmailDtoValidator()
    {
        RuleFor(x => x.NewEmail)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
                .WithMessage(
                    "Yeni email boş ola bilməz")
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