using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Wwg.Api.IntegrationTests.Support;

/// <summary>
/// Makes a race happen on cue: just before the next SQL command that matches, runs another
/// statement on the same connection, as if a second request had got in first (deleted the row
/// the command needs, or changed it). Same connection, so it can't deadlock with the command.
/// </summary>
internal sealed class InterruptingInterceptor : DbCommandInterceptor
{
    private readonly Lock _lock = new();
    private (Func<string, bool> Match, string Sql)? _next;

    /// <summary>Before the next command whose SQL matches, runs <paramref name="sql"/>.</summary>
    public void BeforeNext(Func<string, bool> match, string sql)
    {
        lock (_lock)
        {
            _next = (match, sql);
        }
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default
    )
    {
        await InterruptAsync(command, cancellationToken);
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        await InterruptAsync(command, cancellationToken);
        return result;
    }

    private async Task InterruptAsync(DbCommand command, CancellationToken cancellationToken)
    {
        string sql;
        lock (_lock)
        {
            if (_next is not { } next || !next.Match(command.CommandText))
            {
                return;
            }

            sql = next.Sql;
            _next = null;
        }

        await using var interruption = command.Connection!.CreateCommand(); // Set: it's executing.
        interruption.Transaction = command.Transaction;
#pragma warning disable CA2100 // Test-only SQL, written by the test itself.
        interruption.CommandText = sql;
#pragma warning restore CA2100
        await interruption.ExecuteNonQueryAsync(cancellationToken);
    }
}
