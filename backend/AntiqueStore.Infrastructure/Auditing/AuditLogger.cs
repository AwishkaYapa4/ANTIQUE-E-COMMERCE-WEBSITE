using System.Text.Json;
using AntiqueStore.Application.Common;
using AntiqueStore.Domain.Entities;

namespace AntiqueStore.Infrastructure.Auditing;

public sealed class AuditLogger(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock) : IAuditLogger
{
    public void Log(string action, string entityType, object? entityId, string? summary = null, object? data = null)
    {
        db.AdminAuditLogs.Add(new AdminAuditLog
        {
            UserId = currentUser.UserId,
            UserEmail = currentUser.Email,
            Action = action,
            EntityType = entityType,
            EntityId = entityId?.ToString(),
            Summary = summary is { Length: > 1000 } ? summary[..1000] : summary,
            DataJson = data is null ? null : JsonSerializer.Serialize(data),
            IpAddress = currentUser.IpAddress,
            CreatedAt = clock.GetUtcNow().UtcDateTime
        });
    }
}
