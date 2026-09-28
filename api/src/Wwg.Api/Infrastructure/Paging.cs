using Microsoft.EntityFrameworkCore;

namespace Wwg.Api.Infrastructure;

/// <summary>One page of a list that can grow large.</summary>
/// <param name="Items">This page's items, in a stable order.</param>
/// <param name="Page">The page number, from 1.</param>
/// <param name="PageSize">The requested page size (the last page can have fewer items).</param>
/// <param name="TotalCount">How many items there are across all pages.</param>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount
);

internal static class Paging
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    /// <summary>
    /// Counts and fetches one page. The query must already have a stable order (a sort key, then
    /// Id), so pages don't shuffle between requests.
    /// </summary>
    public static async Task<PagedResponse<T>> ToPagedAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResponse<T>(items, page, pageSize, total);
    }
}

internal static class Search
{
    /// <summary>
    /// A LIKE pattern matching <paramref name="term"/> anywhere, with LIKE's wildcards escaped
    /// (use with <c>EF.Functions.Like(column, pattern, EscapeCharacter)</c>). LIKE rather than
    /// Contains: EF turns Contains into SQLite's instr(), which is case-sensitive even on NOCASE
    /// columns, while LIKE ignores ASCII case.
    /// </summary>
    public static string ContainsPattern(string term) =>
        "%"
        + term.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal)
        + "%";

    public const string EscapeCharacter = @"\";
}
