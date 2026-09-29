using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Account;
using Wwg.Api.Features.Armies;
using Wwg.Api.Features.Units;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests;

/// <summary>
/// Two requests racing: the second one's change reaches the database between this one's checks
/// and its save. The answer is a 409 or 404, never a 500 (DESIGN §3.3).
/// </summary>
public sealed class ConcurrencyTests : ApiTest
{
    private readonly InterruptingInterceptor _interceptor = new();

    public ConcurrencyTests() =>
        App.TestServices.Add(services =>
            services.ConfigureDbContext<WwgDbContext>(options =>
                options.AddInterceptors(_interceptor)
            )
        );

    [Fact]
    public async Task CreateUnit_ArmyDeletedBeforeTheSave_Returns409()
    {
        var scenario = await CreateCampaignScenarioAsync();
        _interceptor.BeforeNext(
            sql => sql.Contains("INSERT INTO \"Units\"", StringComparison.Ordinal),
            $"DELETE FROM \"Armies\" WHERE \"Id\" = '{scenario.ArmyId.ToString().ToUpperInvariant()}';"
        );

        using var response = await scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/armies/{scenario.ArmyId}/units", UriKind.Relative),
                new CreateUnitRequest("2nd Division", UnitType.LightInfantry, 4, 10),
                CancellationToken
            );

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RenameArmy_DeletedBeforeTheSave_Returns409()
    {
        var scenario = await CreateCampaignScenarioAsync();
        _interceptor.BeforeNext(
            sql => sql.StartsWith("UPDATE \"Armies\"", StringComparison.Ordinal),
            $"DELETE FROM \"Armies\" WHERE \"Id\" = '{scenario.ArmyId.ToString().ToUpperInvariant()}';"
        );

        using var response = await RenameArmyAsync(scenario);

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RenameArmy_DeletedAfterTheAccessCheck_Returns404()
    {
        var scenario = await CreateCampaignScenarioAsync();
        // The handler's own load of the army (every column; the access check reads one).
        _interceptor.BeforeNext(
            sql =>
                sql.Contains("FROM \"Armies\"", StringComparison.Ordinal)
                && sql.Contains("\"a\".\"Name\"", StringComparison.Ordinal),
            $"DELETE FROM \"Armies\" WHERE \"Id\" = '{scenario.ArmyId.ToString().ToUpperInvariant()}';"
        );

        using var response = await RenameArmyAsync(scenario);

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateMe_UserChangedBeforeTheSave_Returns409()
    {
        using var client = await CreateUserClientAsync("mel@example.com");
        // What another request saving the user does: a new concurrency stamp.
        _interceptor.BeforeNext(
            sql => sql.StartsWith("UPDATE \"AspNetUsers\"", StringComparison.Ordinal),
            "UPDATE \"AspNetUsers\" SET \"ConcurrencyStamp\" = 'changed' "
                + "WHERE \"NormalizedEmail\" = 'MEL@EXAMPLE.COM';"
        );

        using var response = await client.PutAsJsonAsync(
            new Uri("/api/me", UriKind.Relative),
            new UpdateProfileRequest("Melanie", "Green"),
            CancellationToken
        );

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    private static Task<HttpResponseMessage> RenameArmyAsync(CampaignScenario scenario) =>
        scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri($"/api/armies/{scenario.ArmyId}", UriKind.Relative),
                new RenameArmyRequest("Second Corps"),
                CancellationToken
            );
}
