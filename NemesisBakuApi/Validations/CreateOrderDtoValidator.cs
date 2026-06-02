using FluentValidation;
using NemesisBakuApi.DTOs.Order;
using NemesisBakuApi.Enums;

namespace NemesisBakuApi.Validations;

public class CreateOrderDtoValidator : AbstractValidator<CreateOrderDto>
{
    public CreateOrderDtoValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Sifariş üçün məhsul seçilməlidir");

        RuleFor(x => x.CustomerFullName)
            .NotEmpty().WithMessage("Ad soyad boş ola bilməz");

        RuleFor(x => x.CustomerPhoneNumber)
            .NotEmpty().WithMessage("Telefon nömrəsi boş ola bilməz");

        When(x => x.DeliveryType == DeliveryType.HomeDelivery, () =>
        {
            RuleFor(x => x.AddressText)
                .NotEmpty().WithMessage("Ünvan məcburidir");

            RuleFor(x => x.Latitude)
                .NotNull().WithMessage("Xəritədən konum seçilməlidir");

            RuleFor(x => x.Longitude)
                .NotNull().WithMessage("Xəritədən konum seçilməlidir");

            RuleFor(x => x.DeliveryDate)
                .NotNull().WithMessage("Çatdırılma tarixi seçilməlidir");

            RuleFor(x => x.DeliveryTimeRange)
                .NotEmpty().WithMessage("Çatdırılma saat aralığı seçilməlidir");
        });
    }
}