using AntiqueStore.Domain.Common;
using AntiqueStore.Domain.Enums;

namespace AntiqueStore.Domain.Entities;

public class Review : Entity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int Rating { get; set; }
    public string? Title { get; set; }
    public string Body { get; set; } = "";
    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;
    /// <summary>Set by the server only when a qualifying delivered/paid order exists.</summary>
    public bool IsVerifiedPurchase { get; set; }
    public int? OrderId { get; set; }
    public DateTime? ModeratedAt { get; set; }
    public int? ModeratedByUserId { get; set; }
}

public class Enquiry : Entity
{
    public EnquiryType Type { get; set; }
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public int? UserId { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string? Subject { get; set; }
    public string Message { get; set; } = "";
    public EnquiryStatus Status { get; set; } = EnquiryStatus.New;
    public string? AdminNotes { get; set; }
    /// <summary>SHA-256 of client IP (raw IPs are not stored).</summary>
    public string? IpHash { get; set; }
}

public class Testimonial : Entity
{
    public string Quote { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string? AuthorLocation { get; set; }
    public int? Rating { get; set; }
    public bool IsPublished { get; set; }
    public int SortOrder { get; set; }
}

public class NewsletterSubscriber : Entity
{
    public string Email { get; set; } = "";
    public NewsletterStatus Status { get; set; } = NewsletterStatus.Subscribed;
    public string? Source { get; set; }
    public string UnsubscribeToken { get; set; } = "";
}
