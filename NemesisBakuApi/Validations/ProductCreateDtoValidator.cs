using FluentValidation;
using NemesisBakuApi.DTOs.Product;

namespace NemesisBakuApi.Validations;

public class ProductCreateDtoValidator
    : AbstractValidator<ProductCreateDto>
{
    public ProductCreateDtoValidator()
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
                .WithMessage(
                    "Məhsul adı boş ola bilməz")
            .MaximumLength(150)
                .WithMessage(
                    "Məhsul adı maksimum 150 simvol " +
                    "ola bilər");

        RuleFor(x => x.Description)
            .MaximumLength(5000)
                .When(x =>
                    !string.IsNullOrWhiteSpace(
                        x.Description))
                .WithMessage(
                    "Məhsul açıqlaması maksimum " +
                    "5000 simvol ola bilər");

        RuleFor(x => x.ProductCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
                .WithMessage(
                    "Məhsul kodu boş ola bilməz")
            .MaximumLength(50)
                .WithMessage(
                    "Məhsul kodu maksimum 50 simvol " +
                    "ola bilər");

        RuleFor(x => x.Model)
            .MaximumLength(150)
                .When(x =>
                    !string.IsNullOrWhiteSpace(
                        x.Model))
                .WithMessage(
                    "Model maksimum 150 simvol " +
                    "ola bilər");

        RuleFor(x => x.Price)
            .GreaterThan(0)
                .WithMessage(
                    "Qiymət 0-dan böyük olmalıdır")
            .LessThanOrEqualTo(1_000_000)
                .WithMessage(
                    "Qiymət icazə verilən həddi aşır");

        RuleFor(x => x.DiscountPrice)
            .GreaterThan(0)
                .When(x =>
                    x.DiscountPrice.HasValue)
                .WithMessage(
                    "Endirim qiyməti 0-dan böyük " +
                    "olmalıdır")
            .LessThan(x => x.Price)
                .When(x =>
                    x.DiscountPrice.HasValue)
                .WithMessage(
                    "Endirim qiyməti əsas qiymətdən " +
                    "aşağı olmalıdır");

        RuleFor(x => x.CategoryId)
            .NotEmpty()
                .WithMessage(
                    "Kateqoriya seçilməlidir");

        RuleFor(x => x.BrandId)
            .NotEmpty()
                .WithMessage(
                    "Brend seçilməlidir");

        RuleFor(x => x.Variants)
            .Cascade(CascadeMode.Stop)
            .NotNull()
                .WithMessage(
                    "Məhsul variantları göndərilməyib")
            .NotEmpty()
                .WithMessage(
                    "Ən azı bir variant əlavə edilməlidir")
            .Must(variants =>
                variants.Count <= 500)
                .WithMessage(
                    "Bir məhsulda maksimum 500 " +
                    "variant ola bilər")
            .Must(HaveUniqueVariants)
                .WithMessage(
                    "Eyni razmer və rəng variantı " +
                    "təkrar əlavə edilə bilməz");

        RuleForEach(x => x.Variants)
            .ChildRules(variant =>
            {
                variant.RuleFor(x => x.SizeId)
                    .NotEmpty()
                    .WithMessage(
                        "Variant üçün razmer " +
                        "seçilməlidir");

                variant.RuleFor(x => x.ColorId)
                    .NotEmpty()
                    .WithMessage(
                        "Variant üçün rəng " +
                        "seçilməlidir");

                variant.RuleFor(x => x.StockCount)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage(
                        "Stok sayı mənfi ola bilməz")
                    .LessThanOrEqualTo(1_000_000)
                    .WithMessage(
                        "Stok sayı icazə verilən " +
                        "həddi aşır");
            });
    }

    private static bool HaveUniqueVariants(
        List<ProductVariantCreateDto> variants)
    {
        return variants
            .Select(x => new
            {
                x.SizeId,
                x.ColorId
            })
            .Distinct()
            .Count() == variants.Count;
    }
}