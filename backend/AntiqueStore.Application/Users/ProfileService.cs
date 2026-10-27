using AntiqueStore.Application.Common;
using AntiqueStore.Domain.Entities;
using AntiqueStore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AntiqueStore.Application.Users;

public sealed record MeDto(
    int Id, string Email, bool EmailVerified, string? DisplayName, string? PhoneNumber, string? PhotoUrl,
    UserRole Role, DateTime CreatedAt, IReadOnlyList<string> Permissions);

public sealed record UpdateProfileRequest(string? DisplayName, string? PhoneNumber);

public sealed record AddressDto(
    int Id, string Label, string FullName, string Phone, string Line1, string? Line2, string City, string District,
    string? PostalCode, string Country, bool IsDefault);

public sealed record AddressRequest(
    string Label, string FullName, string Phone, string Line1, string? Line2, string City, string District,
    string? PostalCode, bool IsDefault);

/// <summary>Identity details extracted from a verified Firebase token (never from the request body).</summary>
public sealed record VerifiedIdentity(string Uid, string? Email, bool EmailVerified, string? Name, string? Picture);

public static class Permissions
{
    public const string ManageCatalog = "catalog.manage";
    public const string ManageOrders = "orders.manage";
    public const string ManageCustomers = "customers.view";
    public const string ManageContent = "content.manage";
    public const string ManageSettings = "settings.manage";
    public const string ViewReports = "reports.view";

    public static readonly IReadOnlyList<string> All =
        [ManageCatalog, ManageOrders, ManageCustomers, ManageContent, ManageSettings, ViewReports];

    /// <summary>Role → permission map. A future "Staff" role can be added here with a subset.</summary>
    public static IReadOnlyList<string> For(UserRole role) => role == UserRole.Admin ? All : [];
}

public sealed class ProfileService(IAppDbContext db, TimeProvider clock)
{
    /// <summary>
    /// Finds or creates the MySQL profile for a verified Firebase identity. Role is NEVER taken from the
    /// token or client; new users are always customers. Admins are provisioned via the CLI command.
    /// </summary>
    public async Task<User> SyncAsync(VerifiedIdentity identity, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var user = await db.Users.FirstOrDefaultAsync(u => u.FirebaseUid == identity.Uid, ct);
        if (user is null)
        {
            user = new User
            {
                FirebaseUid = identity.Uid, Email = identity.Email ?? "", EmailVerified = identity.EmailVerified,
                DisplayName = identity.Name, PhotoUrl = identity.Picture, Role = UserRole.Customer, LastLoginAt = now
            };
            db.Users.Add(user);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Two parallel first requests: the other one created the row.
                db.ChangeTracker.Clear();
                user = await db.Users.FirstAsync(u => u.FirebaseUid == identity.Uid, ct);
            }
            return user;
        }

        var changed = false;
        if (identity.Email is { } email && user.Email != email) { user.Email = email; changed = true; }
        if (user.EmailVerified != identity.EmailVerified) { user.EmailVerified = identity.EmailVerified; changed = true; }
        if (string.IsNullOrEmpty(user.DisplayName) && identity.Name is not null) { user.DisplayName = identity.Name; changed = true; }
        if (user.LastLoginAt is null || now - user.LastLoginAt > TimeSpan.FromMinutes(30)) { user.LastLoginAt = now; changed = true; }
        if (changed) await db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<MeDto> GetMeAsync(int userId, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, ct) ?? throw new NotFoundException("User");
        return new MeDto(u.Id, u.Email, u.EmailVerified, u.DisplayName, u.PhoneNumber, u.PhotoUrl, u.Role, u.CreatedAt, Permissions.For(u.Role));
    }

    public async Task<MeDto> UpdateMeAsync(int userId, UpdateProfileRequest r, CancellationToken ct)
    {
        var u = await db.Users.FirstAsync(x => x.Id == userId, ct);
        // Only these fields are client-editable; email, role and UID are not (overposting protection).
        u.DisplayName = Text.TrimToNull(r.DisplayName);
        u.PhoneNumber = Text.TrimToNull(r.PhoneNumber);
        await db.SaveChangesAsync(ct);
        return await GetMeAsync(userId, ct);
    }

    public async Task<IReadOnlyList<AddressDto>> GetAddressesAsync(int userId, CancellationToken ct) =>
        await db.UserAddresses.AsNoTracking().Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault).ThenBy(a => a.Id)
            .Select(a => new AddressDto(a.Id, a.Label, a.FullName, a.Phone, a.Line1, a.Line2, a.City, a.District, a.PostalCode, a.Country, a.IsDefault))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AddressDto>> AddAddressAsync(int userId, AddressRequest r, CancellationToken ct)
    {
        if (await db.UserAddresses.CountAsync(a => a.UserId == userId, ct) >= 20)
            throw new BusinessRuleException("You can save up to 20 addresses.", "address_limit");
        var address = new UserAddress { UserId = userId };
        Apply(address, r);
        var isFirst = !await db.UserAddresses.AnyAsync(a => a.UserId == userId, ct);
        if (r.IsDefault || isFirst) await ClearDefaultAsync(userId, ct);
        address.IsDefault = r.IsDefault || isFirst;
        db.UserAddresses.Add(address);
        await db.SaveChangesAsync(ct);
        return await GetAddressesAsync(userId, ct);
    }

    public async Task<IReadOnlyList<AddressDto>> UpdateAddressAsync(int userId, int id, AddressRequest r, CancellationToken ct)
    {
        var address = await db.UserAddresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct)
                      ?? throw new NotFoundException("Address", id);
        Apply(address, r);
        if (r.IsDefault && !address.IsDefault)
        {
            await ClearDefaultAsync(userId, ct);
            address.IsDefault = true;
        }
        await db.SaveChangesAsync(ct);
        return await GetAddressesAsync(userId, ct);
    }

    public async Task<IReadOnlyList<AddressDto>> DeleteAddressAsync(int userId, int id, CancellationToken ct)
    {
        var address = await db.UserAddresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct)
                      ?? throw new NotFoundException("Address", id);
        db.UserAddresses.Remove(address);
        await db.SaveChangesAsync(ct);
        if (address.IsDefault)
        {
            var next = await db.UserAddresses.Where(a => a.UserId == userId).OrderBy(a => a.Id).FirstOrDefaultAsync(ct);
            if (next is not null) { next.IsDefault = true; await db.SaveChangesAsync(ct); }
        }
        return await GetAddressesAsync(userId, ct);
    }

    private async Task ClearDefaultAsync(int userId, CancellationToken ct) =>
        await db.UserAddresses.Where(a => a.UserId == userId && a.IsDefault)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false), ct);

    private static void Apply(UserAddress a, AddressRequest r)
    {
        a.Label = string.IsNullOrWhiteSpace(r.Label) ? "Home" : r.Label.Trim();
        a.FullName = r.FullName.Trim();
        a.Phone = r.Phone.Trim();
        a.Line1 = r.Line1.Trim();
        a.Line2 = Text.TrimToNull(r.Line2);
        a.City = r.City.Trim();
        a.District = r.District;
        a.PostalCode = Text.TrimToNull(r.PostalCode);
    }
}
