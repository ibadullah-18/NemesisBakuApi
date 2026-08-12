using FluentValidation;
using NemesisBakuApi.DTOs.Order;
using NemesisBakuApi.Enums;

namespace NemesisBakuApi.Validations;

public class CreateOrderDtoValidator
    : AbstractValidator<CreateOrderDto>
{
    public CreateOrderDtoValidator()
    {
        RuleFor(x => x.Items)
            .Cascade(CascadeMode.Stop)
            .NotNull()
                .WithMessage(
                    "Sifariş məhsulları göndərilməyib")
            .NotEmpty()
                .WithMessage(
                    "Sifariş üçün məhsul seçilməlidir")
            .Must(items =>
                items.Count <= 100)
                .WithMessage(
                    "Bir sifarişdə maksimum 100 " +
                    "səbət məhsulu ola bilər")
            .Must(HaveUniqueBasketItems)
                .WithMessage(
                    "Eyni səbət məhsulu sifarişə " +
                    "bir neçə dəfə əlavə edilə bilməz");

        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(x => x.BasketItemId)
                    .NotEmpty()
                    .WithMessage(
                        "Səbət məhsulunun ID-si " +
                        "boş ola bilməz");
            });

        RuleFor(x => x.CustomerFullName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
                .WithMessage(
                    "Ad soyad boş ola bilməz")
            .MaximumLength(150)
                .WithMessage(
                    "Ad soyad maksimum 150 simvol " +
                    "ola bilər");

        RuleFor(x => x.CustomerPhoneNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
                .WithMessage(
                    "Telefon nömrəsi boş ola bilməz")
            .MaximumLength(30)
                .WithMessage(
                    "Telefon nömrəsi çox uzundur")
            .Must(BeValidPhoneNumber)
                .WithMessage(
                    "Telefon nömrəsi düzgün deyil");

        RuleFor(x => x.DeliveryType)
            .IsInEnum()
                .WithMessage(
                    "Çatdırılma növü düzgün deyil");

        RuleFor(x => x.PaymentMethod)
            .IsInEnum()
                .WithMessage(
                    "Ödəniş üsulu düzgün deyil");

        RuleFor(x => x.Note)
            .MaximumLength(1000)
                .When(x =>
                    !string.IsNullOrWhiteSpace(x.Note))
                .WithMessage(
                    "Qeyd maksimum 1000 simvol " +
                    "ola bilər");

        RuleFor(x => x.PromoCode)
            .MaximumLength(100)
                .When(x =>
                    !string.IsNullOrWhiteSpace(
                        x.PromoCode))
                .WithMessage(
                    "Promo kod maksimum 100 simvol " +
                    "ola bilər");

        When(
            x => x.DeliveryType ==
                 DeliveryType.HomeDelivery,
            () =>
            {
                RuleFor(x => x.AddressText)
                    .Cascade(CascadeMode.Stop)
                    .NotEmpty()
                        .WithMessage(
                            "Ünvan məcburidir")
                    .MaximumLength(1000)
                        .WithMessage(
                            "Ünvan maksimum 1000 " +
                            "simvol ola bilər");

                RuleFor(x => x.Latitude)
                    .Cascade(CascadeMode.Stop)
                    .NotNull()
                        .WithMessage(
                            "Xəritədən konum " +
                            "seçilməlidir")
                    .InclusiveBetween(-90m, 90m)
                        .WithMessage(
                            "Enlik koordinatı " +
                            "düzgün deyil");

                RuleFor(x => x.Longitude)
                    .Cascade(CascadeMode.Stop)
                    .NotNull()
                        .WithMessage(
                            "Xəritədən konum " +
                            "seçilməlidir")
                    .InclusiveBetween(-180m, 180m)
                        .WithMessage(
                            "Uzunluq koordinatı " +
                            "düzgün deyil");

                RuleFor(x => x.DeliveryDate)
                    .NotNull()
                        .WithMessage(
                            "Çatdırılma tarixi " +
                            "seçilməlidir");

                RuleFor(x => x.DeliveryTimeRange)
                    .Cascade(CascadeMode.Stop)
                    .NotEmpty()
                        .WithMessage(
                            "Çatdırılma saat aralığı " +
                            "seçilməlidir")
                    .MaximumLength(100)
                        .WithMessage(
                            "Çatdırılma saat aralığı " +
                            "çox uzundur");

                RuleFor(x => x.BuildingNumber)
                    .MaximumLength(100)
                        .When(x =>
                            !string.IsNullOrWhiteSpace(
                                x.BuildingNumber))
                        .WithMessage(
                            "Bina/blok məlumatı " +
                            "maksimum 100 simvol ola bilər");

                RuleFor(x => x.Floor)
                    .MaximumLength(50)
                        .When(x =>
                            !string.IsNullOrWhiteSpace(
                                x.Floor))
                        .WithMessage(
                            "Mərtəbə məlumatı maksimum " +
                            "50 simvol ola bilər");

                RuleFor(x => x.Apartment)
                    .MaximumLength(50)
                        .When(x =>
                            !string.IsNullOrWhiteSpace(
                                x.Apartment))
                        .WithMessage(
                            "Mənzil məlumatı maksimum " +
                            "50 simvol ola bilər");

                RuleFor(x => x.AddressTitle)
                    .MaximumLength(100)
                        .When(x =>
                            !string.IsNullOrWhiteSpace(
                                x.AddressTitle))
                        .WithMessage(
                            "Ünvan başlığı maksimum " +
                            "100 simvol ola bilər");
            });
    }

    private static bool HaveUniqueBasketItems(
        List<OrderItemCreateDto> items)
    {
        return items
            .Select(x => x.BasketItemId)
            .Distinct()
            .Count() == items.Count;
    }

    private static bool BeValidPhoneNumber(
        string value)
    {
        var digits = new string(
            value.Where(char.IsDigit).ToArray());

        return digits.Length is >= 9 and <= 15;
    }
}