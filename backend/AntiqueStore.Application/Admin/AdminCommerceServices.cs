using AntiqueStore.Application.Common;
using AntiqueStore.Application.Promotions;
using AntiqueStore.Application.Settings;
using AntiqueStore.Domain.Entities;
using AntiqueStore.Domain.Enums;
using AntiqueStore.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace AntiqueStore.Application.Admin;

public sealed record DiscountCodeDto(
    int Id, string Code, string? Description, DiscountType Type, decimal Value, decimal? MinOrderSubtotal,
    decimal? MaxDiscountAmount, DateTime? StartsAt, DateTime? EndsAt, int? UsageLimit, int? UsageLimitPerCustomer,
    int UsedCount, bool IsActive, DateTime CreatedAt);

public sealed record DiscountCodeUpsertRequest(
    string Code, string? Description, DiscountType Type, decimal Value, decimal? MinOrderSubtotal, decimal? MaxDiscountAmount,
    DateTime? StartsAt, DateTime? EndsAt, int? UsageLimit, int? UsageLimitPerCustomer, bool IsActive);

public sealed class AdminDiscountService(IAppDbContext db, IAuditLogger audit)
{
    public async Task<IReadOnlyList<DiscountCodeDto>> ListAsync(CancellationToken ct) =>
        await db.DiscountCodes.AsNoTracking().OrderByDescending(d => d.CreatedAt).Select(d => ToDto(d)).ToListAsync(ct);

    public async Task<DiscountCodeDto> UpsertAsync(int? id, DiscountCodeUpsertRequest r, CancellationToken ct)
    {
        var code = DiscountService.Normalize(r.Code);
        if (await db.DiscountCodes.AnyAsync(d => d.Code == code && d.Id != (id ?? 0), ct))
            throw RequestValidationException.For("code", "This code already exists.");

        DiscountCode d;
        if (id is { } existing)
            d = await db.DiscountCodes.FirstOrDefaultAsync(x => x.Id == existing, ct) ?? throw new NotFoundException("Discount code", existing);
        else
            db.DiscountCodes.Add(d = new DiscountCode());

        d.Code = code;
        d.Description = Text.TrimToNull(r.Description);
        d.Type = r.Type;
        d.Value = r.Value;
        d.MinOrderSubtotal = r.MinOrderSubtotal;
        d.MaxDiscountAmount = r.MaxDiscountAmount;
        d.StartsAt = r.StartsAt?.ToUniversalTime();
        d.EndsAt = r.EndsAt?.ToUniversalTime();
        d.UsageLimit = r.UsageLimit;
        d.UsageLimitPerCustomer = r.UsageLimitPerCustomer;
        d.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        audit.Log(id is null ? "discount.create" : "discount.update", "DiscountCode", d.Id, $"Discount {d.Code}");
        await db.SaveChangesAsync(ct);
        return ToDto(d);
    }

    /// <summary>Codes that were used on orders are deactivated rather than deleted to preserve history.</summary>
    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var d = await db.DiscountCodes.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Discount code", id);
        if (await db.DiscountUsages.AnyAsync(u => u.DiscountCodeId == id, ct) || await db.Orders.AnyAsync(o => o.DiscountCodeId == id, ct))
        {
            d.IsActive = false;
            audit.Log("discount.deactivate", "DiscountCode", id, $"Deactivated {d.Code} (has been used)");
            await db.SaveChangesAsync(ct);
            return false;
        }
        db.DiscountCodes.Remove(d);
        audit.Log("discount.delete", "DiscountCode", id, $"Deleted {d.Code}");
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static DiscountCodeDto ToDto(DiscountCode d) => new(
        d.Id, d.Code, d.Description, d.Type, d.Value, d.MinOrderSubtotal, d.MaxDiscountAmount, d.StartsAt, d.EndsAt,
        d.UsageLimit, d.UsageLimitPerCustomer, d.UsedCount, d.IsActive, d.CreatedAt);
}

public sealed record ShippingRateDto(int? Id, string Name, decimal Fee, decimal? MinSubtotal, decimal? MaxSubtotal, bool IsActive);

public sealed record ShippingZoneDto(
    int? Id, string Name, bool IsActive, bool CashOnDeliveryAvailable, string? EstimatedDelivery, int SortOrder,
    IReadOnlyList<string> Districts, IReadOnlyList<ShippingRateDto> Rates);

public sealed class AdminShippingService(IAppDbContext db, IAuditLogger audit)
{
    public async Task<IReadOnlyList<ShippingZoneDto>> ListAsync(CancellationToken ct) =>
        (await db.ShippingZones.AsNoTracking().Include(z => z.Districts).Include(z => z.Rates)
            .OrderBy(z => z.SortOrder).ThenBy(z => z.Name).AsSplitQuery().ToListAsync(ct))
        .Select(ToDto).ToList();

    public async Task<ShippingZoneDto> UpsertAsync(int? id, ShippingZoneDto r, CancellationToken ct)
    {
        var districts = r.Districts.Distinct().ToList();
        var unknown = districts.Where(d => !SriLanka.Districts.Contains(d)).ToList();
        if (unknown.Count > 0) throw RequestValidationException.For("districts", $"Unknown district(s): {string.Join(", ", unknown)}");
        var taken = await db.ShippingZoneDistricts.AsNoTracking()
            .Where(d => districts.Contains(d.District) && d.ShippingZoneId != (id ?? 0))
            .Select(d => d.District).ToListAsync(ct);
        if (taken.Count > 0)
            throw RequestValidationException.For("districts", $"Already assigned to another zone: {string.Join(", ", taken)}");

        ShippingZone zone;
        if (id is { } existing)
            zone = await db.ShippingZones.Include(z => z.Districts).Include(z => z.Rates).FirstOrDefaultAsync(z => z.Id == existing, ct)
                   ?? throw new NotFoundException("Shipping zone", existing);
        else
            db.ShippingZones.Add(zone = new ShippingZone());

        zone.Name = r.Name.Trim();
        zone.IsActive = r.IsActive;
        zone.CashOnDeliveryAvailable = r.CashOnDeliveryAvailable;
        zone.EstimatedDelivery = Text.TrimToNull(r.EstimatedDelivery);
        zone.SortOrder = r.SortOrder;
        zone.Districts.RemoveAll(d => !districts.Contains(d.District));
        foreach (var d in districts.Where(d => zone.Districts.All(x => x.District != d)))
            zone.Districts.Add(new ShippingZoneDistrict { District = d });

        var keepIds = r.Rates.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToHashSet();
        zone.Rates.RemoveAll(x => !keepIds.Contains(x.Id));
        foreach (var rate in r.Rates)
        {
            var target = rate.Id is { } rid ? zone.Rates.FirstOrDefault(x => x.Id == rid) : null;
            if (target is null) zone.Rates.Add(target = new ShippingRate());
            target.Name = rate.Name.Trim();
            target.Fee = rate.Fee;
            target.MinSubtotal = rate.MinSubtotal;
            target.MaxSubtotal = rate.MaxSubtotal;
            target.IsActive = rate.IsActive;
        }

        await db.SaveChangesAsync(ct);
        audit.Log(id is null ? "shipping.create" : "shipping.update", "ShippingZone", zone.Id, $"Shipping zone \"{zone.Name}\"");
        await db.SaveChangesAsync(ct);
        return ToDto(zone);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var zone = await db.ShippingZones.FirstOrDefaultAsync(z => z.Id == id, ct) ?? throw new NotFoundException("Shipping zone", id);
        db.ShippingZones.Remove(zone);
        audit.Log("shipping.delete", "ShippingZone", id, $"Deleted shipping zone \"{zone.Name}\"");
        await db.SaveChangesAsync(ct);
    }

    private static ShippingZoneDto ToDto(ShippingZone z) => new(
        z.Id, z.Name, z.IsActive, z.CashOnDeliveryAvailable, z.EstimatedDelivery, z.SortOrder,
        z.Districts.Select(d => d.District).OrderBy(d => d).ToList(),
        z.Rates.OrderBy(r => r.Fee).Select(r => new ShippingRateDto(r.Id, r.Name, r.Fee, r.MinSubtotal, r.MaxSubtotal, r.IsActive)).ToList());
}

public sealed record CommerceSettingsDto(
    string OrderNumberPrefix, int ReservationMinutes, int BankTransferPaymentWindowHours, decimal? FreeShippingThreshold,
    bool LocalPickupEnabled, string? LocalPickupAddress, string? LocalPickupInstructions, string? DeliveryInstructions,
    bool CashOnDeliveryEnabled, decimal? CashOnDeliveryMaxOrderValue, bool BankTransferEnabled, string? BankName,
    string? BankAccountName, string? BankAccountNumber, string? BankBranch, string? BankTransferInstructions,
    bool ReviewsEnabled, string? ReturnsSummary, string Currency);

public sealed class AdminSettingsService(CommerceSettingsService settings, IAppDbContext db, IAuditLogger audit)
{
    public async Task<CommerceSettingsDto> GetAsync(CancellationToken ct) => ToDto(await settings.GetAsync(ct));

    public async Task<CommerceSettingsDto> UpdateAsync(CommerceSettingsDto r, CancellationToken ct)
    {
        var s = await settings.GetTrackedAsync(ct);
        s.OrderNumberPrefix = r.OrderNumberPrefix.Trim().ToUpperInvariant();
        s.ReservationMinutes = r.ReservationMinutes;
        s.BankTransferPaymentWindowHours = r.BankTransferPaymentWindowHours;
        s.FreeShippingThreshold = r.FreeShippingThreshold;
        s.LocalPickupEnabled = r.LocalPickupEnabled;
        s.LocalPickupAddress = Text.TrimToNull(r.LocalPickupAddress);
        s.LocalPickupInstructions = Text.TrimToNull(r.LocalPickupInstructions);
        s.DeliveryInstructions = Text.TrimToNull(r.DeliveryInstructions);
        s.CashOnDeliveryEnabled = r.CashOnDeliveryEnabled;
        s.CashOnDeliveryMaxOrderValue = r.CashOnDeliveryMaxOrderValue;
        s.BankTransferEnabled = r.BankTransferEnabled;
        s.BankName = Text.TrimToNull(r.BankName);
        s.BankAccountName = Text.TrimToNull(r.BankAccountName);
        s.BankAccountNumber = Text.TrimToNull(r.BankAccountNumber);
        s.BankBranch = Text.TrimToNull(r.BankBranch);
        s.BankTransferInstructions = Text.TrimToNull(r.BankTransferInstructions);
        s.ReviewsEnabled = r.ReviewsEnabled;
        s.ReturnsSummary = Text.TrimToNull(r.ReturnsSummary);
        // Currency is fixed to LKR in this version; multi-currency would need price lists and FX rules.
        audit.Log("settings.commerce", "CommerceSettings", s.Id, "Updated store settings");
        await db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    private static CommerceSettingsDto ToDto(CommerceSettings s) => new(
        s.OrderNumberPrefix, s.ReservationMinutes, s.BankTransferPaymentWindowHours, s.FreeShippingThreshold, s.LocalPickupEnabled,
        s.LocalPickupAddress, s.LocalPickupInstructions, s.DeliveryInstructions, s.CashOnDeliveryEnabled,
        s.CashOnDeliveryMaxOrderValue, s.BankTransferEnabled, s.BankName, s.BankAccountName, s.BankAccountNumber, s.BankBranch,
        s.BankTransferInstructions, s.ReviewsEnabled, s.ReturnsSummary, s.Currency);
}

public sealed record AdminCustomerQuery : PageRequest
{
    public string? Q { get; init; }
}

public sealed record AdminCustomerDto(
    int Id, string Email, string? DisplayName, string? PhoneNumber, UserRole Role, bool EmailVerified, bool IsDisabled,
    int OrderCount, decimal PaidTotal, DateTime CreatedAt, DateTime? LastLoginAt);

public sealed record AdminCustomerDetailDto(
    AdminCustomerDto Customer, IReadOnlyList<Users.AddressDto> Addresses, IReadOnlyList<Orders.AdminOrderListItemDto> Orders);

public sealed class AdminCustomerService(IAppDbContext db)
{
    public async Task<PagedResult<AdminCustomerDto>> ListAsync(AdminCustomerQuery q, CancellationToken ct)
    {
        var users = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var term = q.Q.Trim();
            users = users.Where(u => u.Email.Contains(term) || (u.DisplayName != null && u.DisplayName.Contains(term)) ||
                                     (u.PhoneNumber != null && u.PhoneNumber.Contains(term)));
        }
        return await users.OrderByDescending(u => u.CreatedAt).ToPagedAsync(q, Project, ct);
    }

    private IQueryable<AdminCustomerDto> Project(IQueryable<User> users) =>
        users.Select(u => new AdminCustomerDto(
            u.Id, u.Email, u.DisplayName, u.PhoneNumber, u.Role, u.EmailVerified, u.IsDisabled,
            db.Orders.Count(o => o.UserId == u.Id),
            db.Orders.Where(o => o.UserId == u.Id && o.PaymentStatus == PaymentStatus.Paid).Sum(o => (decimal?)o.Total) ?? 0m,
            u.CreatedAt, u.LastLoginAt));

    public async Task<AdminCustomerDetailDto> GetAsync(int id, CancellationToken ct)
    {
        var customer = await Project(db.Users.AsNoTracking().Where(u => u.Id == id)).FirstOrDefaultAsync(ct)
                       ?? throw new NotFoundException("Customer", id);
        var addresses = await db.UserAddresses.AsNoTracking().Where(a => a.UserId == id)
            .Select(a => new Users.AddressDto(a.Id, a.Label, a.FullName, a.Phone, a.Line1, a.Line2, a.City, a.District, a.PostalCode, a.Country, a.IsDefault))
            .ToListAsync(ct);
        var orders = await db.Orders.AsNoTracking().Where(o => o.UserId == id).OrderByDescending(o => o.PlacedAt).Take(50)
            .Select(o => new Orders.AdminOrderListItemDto(o.Id, o.OrderNumber, o.CustomerName, o.CustomerEmail, o.Status, o.PaymentStatus,
                o.PaymentMethod, o.FulfillmentMethod, o.Total, o.Items.Sum(i => i.Quantity), o.PlacedAt))
            .ToListAsync(ct);
        return new AdminCustomerDetailDto(customer, addresses, orders);
    }
}

public sealed record AuditLogDto(long Id, string? UserEmail, string Action, string EntityType, string? EntityId, string? Summary, DateTime CreatedAt);
public sealed record AuditLogQuery : PageRequest
{
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
}

public sealed class AuditLogQueryService(IAppDbContext db)
{
    public async Task<PagedResult<AuditLogDto>> ListAsync(AuditLogQuery q, CancellationToken ct)
    {
        var logs = db.AdminAuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.EntityType)) logs = logs.Where(l => l.EntityType == q.EntityType);
        if (!string.IsNullOrWhiteSpace(q.EntityId)) logs = logs.Where(l => l.EntityId == q.EntityId);
        return await logs.OrderByDescending(l => l.Id).ToPagedAsync(q, x => x.Select(l =>
            new AuditLogDto(l.Id, l.UserEmail, l.Action, l.EntityType, l.EntityId, l.Summary, l.CreatedAt)), ct);
    }
}
