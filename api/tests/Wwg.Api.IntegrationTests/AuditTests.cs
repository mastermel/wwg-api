using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.IntegrationTests;

public sealed class AuditTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SaveChanges_NewUser_SetsCreatedAtFromClockAsUtc()
    {
        await using var factory = new WwgApiFactory();
        var clock = new FakeTimeProvider(Now);
        await using var app = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock))
        );
        using var client = app.CreateClient();

        var id = await WithDbAsync(
            app.Services,
            async db =>
            {
                var user = new AppUser
                {
                    FirstName = "Mel",
                    LastName = "Green",
                    UserName = "mel",
                };
                db.Users.Add(user);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
                return user.Id;
            }
        );

        var saved = await WithDbAsync(
            app.Services,
            db =>
                db.Users.AsNoTracking()
                    .SingleAsync(u => u.Id == id, TestContext.Current.CancellationToken)
        );
        Assert.Equal(Now.UtcDateTime, saved.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, saved.CreatedAt.Kind);
        Assert.Equal(7, saved.Id.Version);
    }

    private static async Task<T> WithDbAsync<T>(
        IServiceProvider services,
        Func<WwgDbContext, Task<T>> action
    )
    {
        await using var scope = services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<WwgDbContext>());
    }
}
