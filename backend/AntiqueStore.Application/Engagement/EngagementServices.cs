using System.Security.Cryptography;
using System.Text;
using AntiqueStore.Application.Common;
using AntiqueStore.Application.Customization;
using AntiqueStore.Domain.Entities;
using AntiqueStore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AntiqueStore.Application.Engagement;

public sealed record EnquiryRequest(
    int? ProductId, string Name, string Email, string? Phone, string? Subject, string Message,
    // Honeypot: real users never fill this hidden field.
    string? Website, long? FormStartedAt);

public sealed record EnquirySubmittedDto(bool Received, string Message);

public sealed record AdminEnquiryQuery : PageRequest
{
    public EnquiryStatus? Status { get; init; }
    public EnquiryType? Type { get; init; }
    public string? Q { get; init; }
}

public sealed record EnquiryDto(
    int Id, EnquiryType Type, int? ProductId, string? ProductName, string? ProductSlug, string Name, string Email,
    string? Phone, string? Subject, string Message, EnquiryStatus Status, string? AdminNotes, DateTime CreatedAt);

public sealed record UpdateEnquiryRequest(EnquiryStatus Status, string? AdminNotes);

public sealed class EnquiryService(IAppDbContext db, ICurrentUser currentUser, IAuditLogger audit, TimeProvider clock)
{
    private static readonly EnquirySubmittedDto Accepted = new(true, "Thank you — we've received your message and will reply soon.");

    public async Task<EnquirySubmittedDto> SubmitAsync(EnquiryRequest r, EnquiryType type, CancellationToken ct)
    {
        // Spam heuristics: honeypot filled, or form submitted implausibly fast. Bots get a normal-looking
        // response so they don't learn what was detected; the message is stored as Spam for review.
        var now = clock.GetUtcNow();
        var tooFast = r.FormStartedAt is { } started && now.ToUnixTimeMilliseconds() - started < 2500;
        var isSpam = !string.IsNullOrWhiteSpace(r.Website) || tooFast;

        if (type == EnquiryType.Product)
        {
            if (r.ProductId is not { } pid || !await db.Products.AnyAsync(p => p.Id == pid && p.Status == ProductStatus.Published, ct))
                throw RequestValidationException.For("productId", "This item could not be found.");
        }

        var ipHash = currentUser.IpAddress is { } ip
            ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("enquiry:" + ip))).ToLowerInvariant()
            : null;

        // Per-IP daily cap (in addition to the API rate limiter).
        if (ipHash is not null)
        {
            var since = now.UtcDateTime.AddDays(-1);
            if (await db.Enquiries.CountAsync(e => e.IpHash == ipHash && e.CreatedAt >= since, ct) >= 20)
                throw new BusinessRuleException("You've sent several messages today. Please wait before sending more.", "rate_limited");
        }

        db.Enquiries.Add(new Enquiry
        {
            Type = type, ProductId = type == EnquiryType.Product ? r.ProductId : null, UserId = currentUser.UserId,
            Name = r.Name.Trim(), Email = r.Email.Trim(), Phone = Text.TrimToNull(r.Phone), Subject = Text.TrimToNull(r.Subject),
            Message = r.Message.Trim(), Status = isSpam ? EnquiryStatus.Spam : EnquiryStatus.New, IpHash = ipHash
        });
        await db.SaveChangesAsync(ct);
        return Accepted;
    }

    public async Task<PagedResult<EnquiryDto>> ListAsync(AdminEnquiryQuery q, CancellationToken ct)
    {
        var e = db.Enquiries.AsNoTracking();
        if (q.Status is { } s) e = e.Where(x => x.Status == s);
        else e = e.Where(x => x.Status != EnquiryStatus.Spam);
        if (q.Type is { } t) e = e.Where(x => x.Type == t);
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var term = q.Q.Trim();
            e = e.Where(x => x.Name.Contains(term) || x.Email.Contains(term) || x.Message.Contains(term));
        }
        return await e.OrderByDescending(x => x.CreatedAt).ToPagedAsync(q, x => x.Select(i => new EnquiryDto(
            i.Id, i.Type, i.ProductId, i.Product != null ? i.Product.Name : null, i.Product != null ? i.Product.Slug : null,
            i.Name, i.Email, i.Phone, i.Subject, i.Message, i.Status, i.AdminNotes, i.CreatedAt)), ct);
    }

    public async Task<EnquiryDto> UpdateAsync(int id, UpdateEnquiryRequest r, CancellationToken ct)
    {
        var e = await db.Enquiries.Include(x => x.Product).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Enquiry", id);
        e.Status = r.Status;
        e.AdminNotes = Text.TrimToNull(r.AdminNotes);
        audit.Log("enquiry.update", "Enquiry", id, $"Enquiry from {e.Email} → {r.Status}");
        await db.SaveChangesAsync(ct);
        return new EnquiryDto(e.Id, e.Type, e.ProductId, e.Product?.Name, e.Product?.Slug, e.Name, e.Email, e.Phone, e.Subject,
            e.Message, e.Status, e.AdminNotes, e.CreatedAt);
    }

    public async Task<int> CountNewAsync(CancellationToken ct) => await db.Enquiries.CountAsync(e => e.Status == EnquiryStatus.New, ct);
}

public sealed record NewsletterRequest(string Email, string? Source, string? Website);
public sealed record NewsletterSubscriberDto(int Id, string Email, NewsletterStatus Status, string? Source, DateTime CreatedAt);

public sealed class NewsletterService(IAppDbContext db)
{
    public async Task SubscribeAsync(NewsletterRequest r, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(r.Website)) return; // honeypot
        var email = r.Email.Trim().ToLowerInvariant();
        var existing = await db.NewsletterSubscribers.FirstOrDefaultAsync(s => s.Email == email, ct);
        if (existing is null)
        {
            db.NewsletterSubscribers.Add(new NewsletterSubscriber
            {
                Email = email, Source = Text.TrimToNull(r.Source),
                UnsubscribeToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant()
            });
        }
        else
        {
            existing.Status = NewsletterStatus.Subscribed;
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { /* concurrent duplicate subscribe – already subscribed */ }
    }

    public async Task<bool> UnsubscribeAsync(string token, CancellationToken ct) =>
        await db.NewsletterSubscribers.Where(s => s.UnsubscribeToken == token)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, NewsletterStatus.Unsubscribed), ct) > 0;

    public async Task<PagedResult<NewsletterSubscriberDto>> ListAsync(PageRequest page, CancellationToken ct) =>
        await db.NewsletterSubscribers.AsNoTracking().OrderByDescending(s => s.CreatedAt)
            .ToPagedAsync(page, q => q.Select(s => new NewsletterSubscriberDto(s.Id, s.Email, s.Status, s.Source, s.CreatedAt)), ct);
}

public sealed record TestimonialUpsertRequest(string Quote, string AuthorName, string? AuthorLocation, int? Rating, bool IsPublished, int SortOrder);
public sealed record AdminTestimonialDto(int Id, string Quote, string AuthorName, string? AuthorLocation, int? Rating, bool IsPublished, int SortOrder);

/// <summary>Testimonials are entered by the store owner from real customer feedback; none are seeded.</summary>
public sealed class TestimonialService(IAppDbContext db, IAuditLogger audit)
{
    public async Task<IReadOnlyList<AdminTestimonialDto>> ListAsync(CancellationToken ct) =>
        await db.Testimonials.AsNoTracking().OrderBy(t => t.SortOrder).ThenByDescending(t => t.CreatedAt)
            .Select(t => new AdminTestimonialDto(t.Id, t.Quote, t.AuthorName, t.AuthorLocation, t.Rating, t.IsPublished, t.SortOrder))
            .ToListAsync(ct);

    public async Task<AdminTestimonialDto> UpsertAsync(int? id, TestimonialUpsertRequest r, CancellationToken ct)
    {
        Testimonial t;
        if (id is { } existingId)
            t = await db.Testimonials.FirstOrDefaultAsync(x => x.Id == existingId, ct) ?? throw new NotFoundException("Testimonial", existingId);
        else
            db.Testimonials.Add(t = new Testimonial());
        t.Quote = r.Quote.Trim();
        t.AuthorName = r.AuthorName.Trim();
        t.AuthorLocation = Text.TrimToNull(r.AuthorLocation);
        t.Rating = r.Rating;
        t.IsPublished = r.IsPublished;
        t.SortOrder = r.SortOrder;
        await db.SaveChangesAsync(ct);
        audit.Log(id is null ? "testimonial.create" : "testimonial.update", "Testimonial", t.Id, $"Testimonial by {t.AuthorName}");
        await db.SaveChangesAsync(ct);
        return new AdminTestimonialDto(t.Id, t.Quote, t.AuthorName, t.AuthorLocation, t.Rating, t.IsPublished, t.SortOrder);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var t = await db.Testimonials.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Testimonial", id);
        db.Testimonials.Remove(t);
        audit.Log("testimonial.delete", "Testimonial", id, $"Deleted testimonial by {t.AuthorName}");
        await db.SaveChangesAsync(ct);
    }
}

public sealed record ReviewDto(int Id, int Rating, string? Title, string Body, string AuthorName, bool IsVerifiedPurchase, DateTime CreatedAt);
public sealed record CreateReviewRequest(int Rating, string? Title, string Body);
public sealed record AdminReviewDto(
    int Id, int ProductId, string ProductName, string ProductSlug, int Rating, string? Title, string Body, string AuthorName,
    string AuthorEmail, bool IsVerifiedPurchase, ReviewStatus Status, DateTime CreatedAt);
public sealed record AdminReviewQuery : PageRequest
{
    public ReviewStatus? Status { get; init; }
}
public sealed record ModerateReviewRequest(ReviewStatus Status);

public sealed class ReviewService(IAppDbContext db, IAuditLogger audit, ICurrentUser currentUser, TimeProvider clock)
{
    public async Task<IReadOnlyList<ReviewDto>> ListApprovedAsync(string productSlug, CancellationToken ct) =>
        await db.Reviews.AsNoTracking()
            .Where(r => r.Product!.Slug == productSlug && r.Status == ReviewStatus.Approved)
            .OrderByDescending(r => r.CreatedAt).Take(50)
            .Select(r => new ReviewDto(r.Id, r.Rating, r.Title, r.Body, r.User!.DisplayName ?? "Customer", r.IsVerifiedPurchase, r.CreatedAt))
            .ToListAsync(ct);

    /// <summary>
    /// Reviews start as Pending (moderated). "Verified purchase" is set only when the reviewer has a
    /// delivered or paid order containing this product – never from client input.
    /// </summary>
    public async Task<ReviewDto> CreateAsync(int userId, string productSlug, CreateReviewRequest r, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking().Where(p => p.Slug == productSlug && p.Status == ProductStatus.Published)
            .Select(p => new { p.Id }).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Product", productSlug);
        if (await db.Reviews.AnyAsync(x => x.ProductId == product.Id && x.UserId == userId, ct))
            throw new BusinessRuleException("You have already reviewed this item.", "already_reviewed");

        var qualifyingOrderId = await db.OrderItems.AsNoTracking()
            .Where(i => i.ProductId == product.Id && i.Order!.UserId == userId &&
                        (i.Order.Status == OrderStatus.Delivered || i.Order.PaymentStatus == PaymentStatus.Paid) &&
                        i.Order.Status != OrderStatus.Cancelled)
            .Select(i => (int?)i.OrderId).FirstOrDefaultAsync(ct);

        var review = new Review
        {
            ProductId = product.Id, UserId = userId, Rating = r.Rating, Title = Text.TrimToNull(r.Title), Body = r.Body.Trim(),
            Status = ReviewStatus.Pending, IsVerifiedPurchase = qualifyingOrderId is not null, OrderId = qualifyingOrderId
        };
        db.Reviews.Add(review);
        await db.SaveChangesAsync(ct);
        var name = await db.Users.Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct);
        return new ReviewDto(review.Id, review.Rating, review.Title, review.Body, name ?? "Customer", review.IsVerifiedPurchase, review.CreatedAt);
    }

    public async Task<PagedResult<AdminReviewDto>> ListAsync(AdminReviewQuery q, CancellationToken ct)
    {
        var reviews = db.Reviews.AsNoTracking();
        if (q.Status is { } s) reviews = reviews.Where(r => r.Status == s);
        return await reviews.OrderByDescending(r => r.CreatedAt).ToPagedAsync(q, x => x.Select(r => new AdminReviewDto(
            r.Id, r.ProductId, r.Product!.Name, r.Product.Slug, r.Rating, r.Title, r.Body, r.User!.DisplayName ?? "Customer",
            r.User.Email, r.IsVerifiedPurchase, r.Status, r.CreatedAt)), ct);
    }

    public async Task ModerateAsync(int id, ModerateReviewRequest r, CancellationToken ct)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Review", id);
        review.Status = r.Status;
        review.ModeratedAt = clock.GetUtcNow().UtcDateTime;
        review.ModeratedByUserId = currentUser.UserId;
        audit.Log("review.moderate", "Review", id, $"Review {id} → {r.Status}");
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Review", id);
        db.Reviews.Remove(review);
        audit.Log("review.delete", "Review", id, $"Deleted review {id}");
        await db.SaveChangesAsync(ct);
    }
}
