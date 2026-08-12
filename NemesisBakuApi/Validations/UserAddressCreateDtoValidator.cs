using FluentValidation;
using NemesisBakuApi.DTOs.Profile;

namespace NemesisBakuApi.Validations;

public class UserAddressCreateDtoValidator
    : AbstractValidator<UserAddressCreateDto>
{
    public UserAddressCreateDtoValidator()
    {
        RuleFor(x => x.Title)
            .MaximumLength(100)
                .When(x =>
                    !string.IsNullOrWhiteSpace(
                        x.Title))
                .WithMessage(
                    "Ünvan başlığı maksimum 100 " +
                    "simvol ola bilər");

        RuleFor(x => x.AddressText)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
                .WithMessage(
                    "Ünvan boş ola bilməz")
            .MaximumLength(1000)
                .WithMessage(
                    "Ünvan maksimum 1000 simvol " +
                    "ola bilər");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90m, 90m)
                .WithMessage(
                    "Enlik koordinatı düzgün deyil");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180m, 180m)
                .WithMessage(
                    "Uzunluq koordinatı düzgün deyil");

        RuleFor(x => x.BuildingNumber)
            .MaximumLength(100)
                .When(x =>
                    !string.IsNullOrWhiteSpace(
                        x.BuildingNumber))
                .WithMessage(
                    "Bina/blok məlumatı maksimum " +
                    "100 simvol ola bilər");

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

        RuleFor(x => x.Note)
            .MaximumLength(1000)
                .When(x =>
                    !string.IsNullOrWhiteSpace(
                        x.Note))
                .WithMessage(
                    "Qeyd maksimum 1000 simvol " +
                    "ola bilər");
    }
}