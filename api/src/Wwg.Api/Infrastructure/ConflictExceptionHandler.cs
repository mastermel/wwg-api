using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SQLitePCL;

namespace Wwg.Api.Infrastructure;

/// <summary>
/// Another request changed or deleted the data this one was working on, after this one checked
/// it: Identity's optimistic concurrency failed (the user's ConcurrencyStamp), for example.
/// </summary>
internal sealed class ConcurrentChangeException() : Exception("The data was changed meanwhile.");

/// <summary>
/// The row a request was working on was deleted after the request checked it (the access filter
/// found it, then the handler didn't). See <see cref="GoneQueryableExtensions"/>.
/// </summary>
internal sealed class GoneMeanwhileException() : Exception("The row was deleted meanwhile.");

internal static class GoneQueryableExtensions
{
    /// <summary>
    /// <c>SingleAsync</c> for a row that was there a moment ago (the access filter found it): if
    /// it's gone, it was deleted meanwhile, which is a 404, not a 500.
    /// </summary>
    public static async Task<T> SingleOrGoneAsync<T>(
        this IQueryable<T> query,
        CancellationToken cancellationToken
    ) => await query.SingleOrDefaultAsync(cancellationToken) ?? throw new GoneMeanwhileException();
}

/// <summary>
/// Handlers check their rules (e.g. "email already in use") first, but two requests can race past
/// the check. The database is the final guarantee, and when it (or Identity's concurrency check)
/// refuses a change, the answer is a 409 or 404 Problem Details, never a 500:
/// <list type="bullet">
/// <item>a unique index: 409, the data already exists;</item>
/// <item>a foreign key, a trigger enforcing a rule (the commander rules), or a row changed or
/// deleted under an update (EF's and Identity's concurrency checks): 409, reload and try
/// again;</item>
/// <item>a row deleted between the access check and the handler: 404.</item>
/// </list>
/// </summary>
internal sealed class ConflictExceptionHandler(IProblemDetailsService problemDetails)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        var (status, detail) = exception switch
        {
            GoneMeanwhileException => (
                StatusCodes.Status404NotFound,
                "It was deleted while this request was running."
            ),
            _ when IsConstraintViolation(exception, raw.SQLITE_CONSTRAINT_UNIQUE)
                    || IsConstraintViolation(exception, raw.SQLITE_CONSTRAINT_PRIMARYKEY) => (
                StatusCodes.Status409Conflict,
                "The request conflicts with data that already exists."
            ),
            DbUpdateConcurrencyException or ConcurrentChangeException => ChangedMeanwhile,
            _ when IsConstraintViolation(exception, raw.SQLITE_CONSTRAINT_FOREIGNKEY)
                    || IsConstraintViolation(exception, raw.SQLITE_CONSTRAINT_TRIGGER) =>
                ChangedMeanwhile,
            _ => (0, null),
        };
        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = { Status = status, Detail = detail },
            }
        );
    }

    private static readonly (int, string) ChangedMeanwhile = (
        StatusCodes.Status409Conflict,
        "Someone else changed this at the same time. Reload, then try again."
    );

    /// <summary>
    /// From <c>SaveChanges</c> (wrapped in a <see cref="DbUpdateException"/>) or from
    /// <c>ExecuteUpdate</c>/<c>ExecuteDelete</c> (not wrapped).
    /// </summary>
    private static bool IsConstraintViolation(Exception exception, int extendedCode) =>
        (
            exception is DbUpdateException { InnerException: SqliteException wrapped }
                ? wrapped
                : exception as SqliteException
        )
            is { SqliteErrorCode: raw.SQLITE_CONSTRAINT, SqliteExtendedErrorCode: var code }
        && code == extendedCode;
}
