using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data.Entities;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests;

public sealed class AuditTests : ApiTest
{
    [Fact]
    public async Task SaveChanges_NewUser_SetsCreatedAtFromClockAsUtc()
    {
        var id = await WithDbAsync(async db =>
        {
            var user = new AppUser
            {
                FirstName = "Mel",
                LastName = "Green",
                UserName = "mel",
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(CancellationToken);
            return user.Id;
        });

        var saved = await WithDbAsync(db =>
            db.Users.AsNoTracking().SingleAsync(u => u.Id == id, CancellationToken)
        );
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, saved.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, saved.CreatedAt.Kind);
        Assert.Equal(7, saved.Id.Version);
    }
}
