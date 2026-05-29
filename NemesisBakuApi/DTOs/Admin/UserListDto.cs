namespace NemesisBakuApi.DTOs.Admin;

public class UserListDto
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string? Email { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public IList<string> Roles { get; set; } = new List<string>();
}