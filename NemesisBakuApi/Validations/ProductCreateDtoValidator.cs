using FluentValidation;
using NemesisBakuApi.DTOs.Product;

namespace NemesisBakuApi.Validations;

public class ProductCreateDtoValidator : AbstractValidator<ProductCreateDto>
{
    public ProductCreateDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Məhsul adı boş ola bilməz")
            .MaximumLength(150).WithMessage("Məhsul adı maksimum 150 simvol ola bilər");

        RuleFor(x => x.ProductCode)
            .NotEmpty().WithMessage("Məhsul kodu boş ola bilməz")
            .MaximumLength(50).WithMessage("Məhsul kodu maksimum 50 simvol ola bilər");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Qiymət 0-dan böyük olmalıdır");

        RuleFor(x => x.DiscountPrice)
            .LessThan(x => x.Price)
            .When(x => x.DiscountPrice.HasValue)
            .WithMessage("Endirim qiyməti əsas qiymətdən aşağı olmalıdır");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Kateqoriya seçilməlidir");

        RuleFor(x => x.BrandId)
            .NotEmpty().WithMessage("Brend seçilməlidir");

        RuleFor(x => x.Variants)
            .NotEmpty().WithMessage("Ən azı bir variant əlavə edilməlidir");
    }
}