using AntiqueStore.Application.Common;
using AntiqueStore.Domain.Entities;
using AntiqueStore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AntiqueStore.Application.Promotions;

public sealed record DiscountEvaluation(bool IsValid, decimal Amount, DiscountCode? Code, string? Error);

/// <summary>All discount validation happens on the server; the client only ever sends the code text.</summary>
public sealed class DiscountService(IAppDbContext db, TimeProvider clock)
{
    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    public async Task<DiscountEvaluation> EvaluateAsync(string? rawCode, decimal subtotal, int userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawCode)) return new DiscountEvaluation(true, 0m, null, null);
        var code = Normalize(rawCode);
        var discount = await db.DiscountCodes.AsNoTracking().FirstOrDefaultAsync(d => d.Code == code, ct);
        var error = await ValidateAsync(discount, subtotal, userId, ct);
        if (error is not null) return new DiscountEvaluation(false, 0m, null, error);
        return new DiscountEvaluation(true, Calculate(discount!, subtotal), discount, null);
    }

    private async Task<string?> ValidateAsync(DiscountCode? d, decimal subtotal, int userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (d is null || !d.IsActive) return "This discount code is not valid.";
        if (d.StartsAt is { } start && now < start) return "This discount code is not active yet.";
        if (d.EndsAt is { } end && now > end) return "This discount code has expired.";
        if (d.UsageLimit is { } limit && d.UsedCount >= limit) return "This discount code has reached its usage limit.";
        if (d.MinOrderSubtotal is { } min && subtotal < min)
            return $"This code requires a minimum order of LKR {min:N2}.";
        if (d.UsageLimitPerCustomer is { } perCustomer)
        {
            var used = await db.DiscountUsages.CountAsync(u => u.DiscountCodeId == d.Id && u.UserId == userId, ct);
            if (used >= perCustomer) return "You have already used this discount code.";
        }
        return null;
    }

    public static decimal Calculate(DiscountCode d, decimal subtotal)
    {
        var amount = d.Type == DiscountType.Percentage
            ? Math.Round(subtotal * d.Value / 100m, 2, MidpointRounding.AwayFromZero)
            : d.Value;
        if (d.MaxDiscountAmount is { } cap) amount = Math.Min(amount, cap);
        return Math.Clamp(amount, 0m, subtotal);
    }

    /// <summary>
    /// Atomically claims one use of the code (inside the order transaction). The conditional UPDATE
    /// guarantees the usage limit cannot be exceeded under concurrency.
    /// </summary>
    public async Task<bool> TryClaimUsageAsync(int discountId, CancellationToken ct)
    {
        var affected = await db.DiscountCodes
            .Where(d => d.Id == discountId && d.IsActive && (d.UsageLimit == null || d.UsedCount < d.UsageLimit))
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.UsedCount, d => d.UsedCount + 1), ct);
        return affected == 1;
    }

    public async Task ReleaseUsageAsync(int orderId, CancellationToken ct)
    {
        var usage = await db.DiscountUsages.FirstOrDefaultAsync(u => u.OrderId == orderId, ct);
        if (usage is null) return;
        await db.DiscountCodes.Where(d => d.Id == usage.DiscountCodeId && d.UsedCount > 0)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.UsedCount, d => d.UsedCount - 1), ct);
        db.DiscountUsages.Remove(usage);
    }
}
