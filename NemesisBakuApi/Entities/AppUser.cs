using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Identity;

namespace NemesisBakuApi.Entities;

public class AppUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = null!;
    public DateTime? DateOfBirth { get; set; }
    public string? ProfileImageUrl { get; set; }

    public string? LoyaltyCardCode { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;

    public bool TermsAccepted { get; set; } = false;
    public DateTime? TermsAcceptedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<BasketItem> BasketItems { get; set; } = new List<BasketItem>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<UserAddress> Addresses { get; set; } = new List<UserAddress>();
}