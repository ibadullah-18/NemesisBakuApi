using NemesisBakuApi.Enums;

namespace NemesisBakuApi.DTOs.Admin;

public class UserDetailDto
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string? Email { get; set; }

    public DateTime? DateOfBirth { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? LoyaltyCardCode { get; set; }

    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public int BasketItemCount { get; set; }
    public int FavoriteCount { get; set; }
    public int OrderCount { get; set; }

    public List<UserOrderMiniDto> Orders { get; set; } = new();
}

public class UserOrderMiniDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = null!;
    public decimal TotalPrice { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}