using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SQLitePCL;

namespace Wwg.Api.Infrastructure;

/// <summary>
/// Handlers check business rules (e.g. "email already in use") first, but two requests can race
/// past the check. The unique index is the final guarantee: its violation becomes a 409 Problem
/// Details, never a 500.
/// </summary>
internal sealed class UniqueConstraintExceptionHandler(IProblemDetailsService problemDetails)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        if (!IsUniqueConstraintViolation(exception))
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problemDetails.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails =
                {
                    Status = StatusCodes.Status409Conflict,
                    Detail = "The request conflicts with data that already exists.",
                },
            }
        );
    }

    private static bool IsUniqueConstraintViolation(Exception exception)
    {
        return exception
            is DbUpdateException
            {
                InnerException: SqliteException
                {
                    SqliteErrorCode: raw.SQLITE_CONSTRAINT,
                    SqliteExtendedErrorCode: raw.SQLITE_CONSTRAINT_UNIQUE
                        or raw.SQLITE_CONSTRAINT_PRIMARYKEY,
                },
            };
    }
}
