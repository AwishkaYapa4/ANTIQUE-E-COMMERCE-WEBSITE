using AntiqueStore.Domain.Common;
using AntiqueStore.Domain.Enums;

namespace AntiqueStore.Domain.Entities;

/// <summary>
/// Application profile linked to a Firebase identity by <see cref="FirebaseUid"/>.
/// Passwords are never stored; Firebase owns authentication, MySQL owns business data.
/// </summary>
public class User : Entity
{
    public string FirebaseUid { get; set; } = "";
    public string Email { get; set; } = "";
    public bool EmailVerified { get; set; }
    public string? DisplayName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PhotoUrl { get; set; }
    public UserRole Role { get; set; } = UserRole.Customer;
    public bool IsDisabled { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public List<UserAddress> Addresses { get; set; } = [];
}

public class UserAddress : Entity
{
    public int UserId { get; set; }
    public User? User { get; set; }
    public string Label { get; set; } = "Home";
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Line1 { get; set; } = "";
    public string? Line2 { get; set; }
    public string City { get; set; } = "";
    public string District { get; set; } = "";
    public string? PostalCode { get; set; }
    public string Country { get; set; } = "Sri Lanka";
    public bool IsDefault { get; set; }
}
