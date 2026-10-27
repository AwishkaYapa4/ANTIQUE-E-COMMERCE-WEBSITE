using AntiqueStore.Application.Common;
using AntiqueStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AntiqueStore.Application.Settings;

public sealed class CommerceSettingsService(IAppDbContext db)
{
    /// <summary>Returns the single settings row (read-only), falling back to safe defaults if not yet seeded.</summary>
    public async Task<CommerceSettings> GetAsync(CancellationToken ct) =>
        await db.CommerceSettings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(ct) ?? new CommerceSettings();

    public async Task<CommerceSettings> GetTrackedAsync(CancellationToken ct)
    {
        var settings = await db.CommerceSettings.OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = new CommerceSettings();
            db.CommerceSettings.Add(settings);
        }
        return settings;
    }
}
