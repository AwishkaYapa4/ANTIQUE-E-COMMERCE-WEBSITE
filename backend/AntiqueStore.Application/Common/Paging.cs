using Microsoft.EntityFrameworkCore;

namespace AntiqueStore.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public record PageRequest
{
    public const int MaxPageSize = 100;
    private int _page = 1;
    private int _pageSize = 24;

    public int Page { get => _page; init => _page = Math.Max(1, value); }
    public int PageSize { get => _pageSize; init => _pageSize = Math.Clamp(value, 1, MaxPageSize); }
}

public static class PagingExtensions
{
    public static async Task<PagedResult<TOut>> ToPagedAsync<TIn, TOut>(
        this IQueryable<TIn> query, PageRequest page, Func<IQueryable<TIn>, IQueryable<TOut>> project,
        CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await project(query.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize)).ToListAsync(ct);
        return new PagedResult<TOut>(items, page.Page, page.PageSize, total);
    }

    /// <summary>Paging with an async fetch step, for projections that need in-memory post-processing.</summary>
    public static async Task<PagedResult<TOut>> ToPagedAsync<TIn, TOut>(
        this IQueryable<TIn> query, PageRequest page, Func<IQueryable<TIn>, CancellationToken, Task<List<TOut>>> fetch,
        CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await fetch(query.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize), ct);
        return new PagedResult<TOut>(items, page.Page, page.PageSize, total);
    }
}
