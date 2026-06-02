using FluentValidation;
using NemesisBakuApi.DTOs.Auth;

namespace NemesisBakuApi.Validations;

public class RegisterDtoValidator : AbstractValidator<VerifyRegisterOtpDto>
{
    public RegisterDtoValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Ad soyad boş ola bilməz");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Telefon nömrəsi boş ola bilməz");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Şifrə boş ola bilməz")
            .MinimumLength(6).WithMessage("Şifrə minimum 6 simvol olmalıdır");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Şifrələr uyğun deyil");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Təsdiq kodu boş ola bilməz");
    }
}