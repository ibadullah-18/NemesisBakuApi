using FluentValidation;
using NemesisBakuApi.DTOs.Profile;

namespace NemesisBakuApi.Validations;

public class SendChangeEmailOtpDtoValidator
    : AbstractValidator<SendChangeEmailOtpDto>
{
    public SendChangeEmailOtpDtoValidator()
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
    }
}