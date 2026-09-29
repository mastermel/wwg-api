using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Admin;
using Wwg.Api.Infrastructure;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

public sealed class AdminUserTests : ApiTest
{
    /// <summary>Creates users directly (much faster than registering each over HTTP).</summary>
    private async Task SeedUsersAsync(params (string First, string Last, string Email)[] users)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        foreach (var (first, last, email) in users)
        {
            (
                await manager.CreateAsync(
                    new AppUser
                    {
                        FirstName = first,
                        LastName = last,
                        Email = email,
                        UserName = email,
                    },
                    TestPassword
                )
            ).EnsureSucceeded();
        }
    }

    private static Task<PagedResponse<UserSummary>?> ListAsync(
        HttpClient admin,
        string query = ""
    ) =>
        admin.GetFromJsonAsync<PagedResponse<UserSummary>>(
            new Uri("/api/admin/users" + query, UriKind.Relative),
            CancellationToken
        );

    [Fact]
    public async Task ListUsers_Anonymous_Returns401()
    {
        using var response = await Client.GetAsync(
            new Uri("/api/admin/users", UriKind.Relative),
            CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListUsers_NotAnAdmin_Returns403()
    {
        using var user = await CreateUserClientAsync();

        using var response = await user.GetAsync(
            new Uri("/api/admin/users", UriKind.Relative),
            CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListUsers_ManyUsers_ReturnsThemAPageAtATimeSortedByName()
    {
        using var admin = await CreateAdminClientAsync();
        await SeedUsersAsync([
            .. Enumerable
                .Range(1, 30)
                .Select(i => ($"First{i:D2}", "Zed", $"zed{i:D2}@example.com")),
        ]);

        var first = await ListAsync(admin);
        var second = await ListAsync(admin, "?page=2");

        Assert.Equal(31, first?.TotalCount); // 30 + the admin.
        Assert.Equal(25, first?.Items.Count);
        Assert.Equal(6, second?.Items.Count);
        // The admin ("User") sorts before the Zeds; then first names in order.
        Assert.Equal("User", first?.Items[0].LastName);
        Assert.True(first?.Items[0].IsAdmin);
        Assert.Equal("First01", first?.Items[1].FirstName);
        Assert.Equal("First30", second?.Items[^1].FirstName);
        Assert.Empty(first!.Items.Select(u => u.Id).Intersect(second!.Items.Select(u => u.Id)));
    }

    [Theory]
    [InlineData("?pageSize=101", "pageSize")]
    [InlineData("?page=0", "page")]
    [InlineData("?page=100001", "page")]
    public async Task ListUsers_PagingOutOfRange_IsAValidationError(string query, string field)
    {
        using var admin = await CreateAdminClientAsync();

        using var response = await admin.GetAsync(
            new Uri("/api/admin/users" + query, UriKind.Relative),
            CancellationToken
        );

        await response.AssertValidationProblemAsync(field);
    }

    [Theory]
    [InlineData("mel", 2)] // first name, any case
    [InlineData("GREEN", 1)] // last name
    [InlineData("@club.example", 1)] // email
    [InlineData("100%", 1)] // % is literal, not a wildcard
    [InlineData("nobody", 0)]
    public async Task ListUsers_Search_MatchesNamesAndEmailIgnoringCase(string search, int expected)
    {
        using var admin = await CreateAdminClientAsync();
        await SeedUsersAsync(
            ("Mel", "Green", "mel@example.com"),
            ("Melanie", "Brown", "mb@club.example"),
            ("Pat", "Stone", "100%pat@example.com")
        );

        var result = await ListAsync(admin, "?search=" + Uri.EscapeDataString(search));

        Assert.Equal(expected, result?.TotalCount);
    }

    [Fact]
    public async Task GetUser_Exists_ReturnsTheirDetails()
    {
        using var admin = await CreateAdminClientAsync();
        await SeedUsersAsync(("Mel", "Green", "mel@example.com"));
        var id = (await ListAsync(admin, "?search=mel"))!.Items.Single().Id;

        var user = await admin.GetFromJsonAsync<UserDetails>(
            new Uri($"/api/admin/users/{id}", UriKind.Relative),
            CancellationToken
        );

        Assert.Equal(
            ("mel@example.com", "Mel", "Green", false),
            (user?.Email, user?.FirstName, user?.LastName, user?.IsAdmin)
        );
        Assert.Null(user?.LockedOutUntil);
    }

    [Fact]
    public async Task GetUser_Unknown_Returns404()
    {
        using var admin = await CreateAdminClientAsync();

        using var response = await admin.GetAsync(
            new Uri($"/api/admin/users/{Guid.CreateVersion7()}", UriKind.Relative),
            CancellationToken
        );

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteUser_Exists_DeletesThemAndEndsTheirSessions()
    {
        using var admin = await CreateAdminClientAsync();
        using var mel = await CreateUserClientAsync("mel@example.com");
        var id = (await ListAsync(admin, "?search=mel@"))!.Items.Single().Id;

        using var response = await admin.DeleteAsync(
            new Uri($"/api/admin/users/{id}", UriKind.Relative),
            CancellationToken
        );
        using var melMe = await mel.GetAsync(
            new Uri("/api/me", UriKind.Relative),
            CancellationToken
        );
        using var details = await admin.GetAsync(
            new Uri($"/api/admin/users/{id}", UriKind.Relative),
            CancellationToken
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, melMe.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, details.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_Themselves_Returns409()
    {
        using var admin = await CreateAdminClientAsync("admin@example.com");
        var id = (await ListAsync(admin, "?search=admin@"))!.Items.Single().Id;

        using var response = await admin.DeleteAsync(
            new Uri($"/api/admin/users/{id}", UriKind.Relative),
            CancellationToken
        );

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }
}
