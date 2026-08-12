using FluentValidation;
using NemesisBakuApi.DTOs.Auth;

namespace NemesisBakuApi.Validations;

public class SendOtpDtoValidator
    : AbstractValidator<SendOtpDto>
{
    public SendOtpDtoValidator()
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
    }
}