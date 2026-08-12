using FluentValidation;
using NemesisBakuApi.DTOs.Auth;

namespace NemesisBakuApi.Validations;

public class RefreshTokenDtoValidator
    : AbstractValidator<RefreshTokenDto>
{
    public RefreshTokenDtoValidator()
    {
        RuleFor(x => x.RefreshToken)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
                .WithMessage(
                    "Refresh token boş ola bilməz")
            .MaximumLength(1000)
                .WithMessage(
                    "Refresh token çox uzundur")
            .Must(BeValidBase64Token)
                .WithMessage(
                    "Refresh token formatı düzgün deyil");
    }

    private static bool BeValidBase64Token(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(
                value.Trim());

            return bytes.Length == 64;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}