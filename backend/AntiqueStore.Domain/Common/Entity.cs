namespace AntiqueStore.Domain.Common;

/// <summary>Base type for all persisted entities. Timestamps are always UTC.</summary>
public abstract class Entity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Stage of a customizable website record. Customers only ever see <see cref="Published"/>.</summary>
public enum ContentStage
{
    Draft = 0,
    Published = 1
}
