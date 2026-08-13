using FluentValidation;
using NemesisBakuApi.DTOs.Basket;

namespace NemesisBakuApi.Validations;

public class AddToBasketDtoValidator
    : AbstractValidator<AddToBasketDto>
{
    public AddToBasketDtoValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty()
            .WithMessage(
                "Məhsul seçilməlidir");

        RuleFor(x => x.ProductVariantId)
            .NotEmpty()
            .WithMessage(
                "Məhsulun razmer/rəngi seçilməlidir");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage(
                "Miqdar düzgün deyil");
    }
}