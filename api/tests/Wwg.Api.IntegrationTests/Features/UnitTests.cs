using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Armies;
using Wwg.Api.Features.Units;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

public sealed class UnitTests : ApiTest
{
    private static Task<HttpResponseMessage> CreateAsync(
        CampaignScenario scenario,
        string name,
        UnitType type = UnitType.HeavyInfantry,
        int fightingFactor = 5,
        int points = 10
    ) => PostAsync(scenario, new CreateUnitRequest(name, type, fightingFactor, points));

    /// <summary>Any body, e.g. JSON the typed request can't express.</summary>
    private static Task<HttpResponseMessage> PostAsync(CampaignScenario scenario, object body) =>
        scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/armies/{scenario.ArmyId}/units", UriKind.Relative),
                body,
                TestJson.Options,
                TestContext.Current.CancellationToken
            );

    private static async Task<List<string>> UnitNamesAsync(CampaignScenario scenario)
    {
        var army = await scenario
            .As(Role.Commander)
            .GetAsAsync<ArmyResponse>($"/api/armies/{scenario.ArmyId}");
        return [.. army!.Units.Select(u => u.Name)];
    }

    [Fact]
    public async Task CreateUnit_Valid_AddsItToTheArmy()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await CreateAsync(
            scenario,
            "  Light Division ",
            UnitType.LightInfantry,
            fightingFactor: 6,
            points: 35
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var unit = await response.Content.ReadAsAsync<UnitResponse>();
        Assert.Equal($"/api/units/{unit?.Id}", response.Headers.Location?.ToString());
        Assert.Equal(
            new UnitResponse(
                unit!.Id,
                scenario.ArmyId,
                "Light Division",
                UnitType.LightInfantry,
                6,
                35
            ),
            unit
        );
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(9, 100)]
    public async Task CreateUnit_AtTheLimits_IsAccepted(int fightingFactor, int points)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await CreateAsync(
            scenario,
            "Guard",
            fightingFactor: fightingFactor,
            points: points
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData(0, 10, "fightingFactor")]
    [InlineData(10, 10, "fightingFactor")]
    [InlineData(5, -1, "points")]
    [InlineData(5, 101, "points")]
    public async Task CreateUnit_OutOfRange_IsAValidationError(
        int fightingFactor,
        int points,
        string field
    )
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await CreateAsync(
            scenario,
            "Guard",
            fightingFactor: fightingFactor,
            points: points
        );

        await response.AssertValidationProblemAsync(field);
    }

    [Fact]
    public async Task CreateUnit_UndefinedTypeNumber_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await PostAsync(
            scenario,
            new
            {
                name = "Guard",
                type = 99,
                fightingFactor = 5,
                points = 10,
            }
        );

        await response.AssertValidationProblemAsync("type");
    }

    [Theory]
    [InlineData("type")]
    [InlineData("fightingFactor")]
    [InlineData("points")]
    public async Task CreateUnit_MissingField_Returns400(string missing)
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["name"] = "Guard",
            ["type"] = "Skirmishers",
            ["fightingFactor"] = 5,
            ["points"] = 10,
        };
        body.Remove(missing);

        using var response = await PostAsync(scenario, body);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateUnit_BlankName_IsAValidationError(string name)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await CreateAsync(scenario, name);

        await response.AssertValidationProblemAsync("name");
    }

    [Fact]
    public async Task GetArmy_ListsItsUnitsByNameIgnoringCase()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        (await CreateAsync(scenario, "cavalry brigade")).Dispose();
        (await CreateAsync(scenario, "Artillery")).Dispose();

        Assert.Equal(
            ["1st Division", "Artillery", "cavalry brigade"],
            await UnitNamesAsync(scenario)
        );
    }

    [Fact]
    public async Task UpdateUnit_ChangesEverything()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri($"/api/units/{scenario.UnitId}", UriKind.Relative),
                new UpdateUnitRequest(" Horse Guards ", UnitType.HeavyCavalry, 8, 60),
                CancellationToken
            );

        var expected = new UnitResponse(
            scenario.UnitId,
            scenario.ArmyId,
            "Horse Guards",
            UnitType.HeavyCavalry,
            8,
            60
        );
        Assert.Equal(expected, await response.Content.ReadAsAsync<UnitResponse>());
        var army = await scenario
            .As(Role.Commander)
            .GetAsAsync<ArmyResponse>($"/api/armies/{scenario.ArmyId}");
        Assert.Equal(expected, Assert.Single(army!.Units));
    }

    [Fact]
    public async Task UpdateUnit_OutOfRange_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri($"/api/units/{scenario.UnitId}", UriKind.Relative),
                new UpdateUnitRequest("Guard", UnitType.HeavyInfantry, 0, 101),
                CancellationToken
            );

        await response.AssertValidationProblemAsync("fightingFactor", "points");
    }

    [Fact]
    public async Task DeleteUnit_RemovesIt()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .DeleteAsync(
                new Uri($"/api/units/{scenario.UnitId}", UriKind.Relative),
                CancellationToken
            );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await UnitNamesAsync(scenario));
    }

    [Fact]
    public async Task DeleteArmy_DeletesItsUnits()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .DeleteAsync(
                new Uri($"/api/armies/{scenario.ArmyId}", UriKind.Relative),
                CancellationToken
            );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await WithDbAsync(db => db.Units.CountAsync(CancellationToken)));
    }

    [Fact]
    public async Task UnassignCommander_KeepsTheUnits()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .DeleteAsync(
                new Uri($"/api/armies/{scenario.ArmyId}/commander", UriKind.Relative),
                CancellationToken
            );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, await WithDbAsync(db => db.Units.CountAsync(CancellationToken)));
    }
}
