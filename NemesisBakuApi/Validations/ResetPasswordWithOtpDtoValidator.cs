using FluentValidation;
using NemesisBakuApi.DTOs.Auth;

namespace NemesisBakuApi.Validations;

public class ResetPasswordWithOtpDtoValidator
    : AbstractValidator<ResetPasswordWithOtpDto>
{
    public ResetPasswordWithOtpDtoValidator()
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

        RuleFor(x => x.NewPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
                .WithMessage(
                    "Yeni şifrə boş ola bilməz")
            .MinimumLength(6)
                .WithMessage(
                    "Yeni şifrə minimum 6 simvol " +
                    "olmalıdır")
            .MaximumLength(100)
                .WithMessage(
                    "Yeni şifrə maksimum 100 simvol " +
                    "ola bilər");

        RuleFor(x => x.ConfirmNewPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
                .WithMessage(
                    "Şifrə təkrarı boş ola bilməz")
            .Equal(x => x.NewPassword)
                .WithMessage(
                    "Şifrələr uyğun deyil");
    }
}