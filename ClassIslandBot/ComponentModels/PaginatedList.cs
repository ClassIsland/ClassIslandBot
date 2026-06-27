using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace ClassIslandBot.ComponentModels;

public class PaginatedList<T>(List<T> items, int count, int pageIndex, int pageSize)
{
    public int PageIndex { get; private set; } = pageIndex;
    public int TotalPages { get; private set; } = (int)Math.Ceiling(count / (double)pageSize);

    public int PageSize { get; private set; } = pageSize;

    public int ItemCount { get; private set; } = count;

    public List<T> Items { get; } = items;

    public bool HasPreviousPage => PageIndex > 1;

    public bool HasNextPage => PageIndex < TotalPages;

    public static async Task<PaginatedList<T>> CreateAsync<TKey>(IQueryable<T> source, int pageIndex, int pageSize,
        Expression<Func<T, TKey>> orderBy, bool decreasing = false)
    {
        if (pageIndex <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), "Page index should larger than 0");
        }
        
        var count = await source.CountAsync();
        var query = decreasing ? source.OrderByDescending(orderBy) : source.OrderBy(orderBy);
        var items = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return new PaginatedList<T>(items, count, pageIndex, pageSize);
    }

    public static async Task<PaginatedList<T>> CreateAsync<TSource, TKey>(
        IQueryable<TSource> source,
        int pageIndex,
        int pageSize,
        Expression<Func<TSource, TKey>> orderBy,
        Expression<Func<TSource, T>> selector,
        bool decreasing = false)
    {
        if (pageIndex <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), "Page index should larger than 0");
        }

        var count = await source.CountAsync();
        var query = decreasing ? source.OrderByDescending(orderBy) : source.OrderBy(orderBy);
        var items = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(selector)
            .ToListAsync();
        return new PaginatedList<T>(items, count, pageIndex, pageSize);
    }
    
    public static PaginatedList<T> CreateFromRawList(IList<T> source, int pageIndex, int pageSize)
    {
        if (pageIndex <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), "Page index should larger than 0");
        }
        
        var count = source.Count;
        var items = source.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
        return new PaginatedList<T>(items, count, pageIndex, pageSize);
    }
}
