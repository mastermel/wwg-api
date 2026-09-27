using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Features.Auth;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

public sealed class AuthTests : ApiTest
{
    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password
    ) =>
        client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new LoginRequest(email, password),
            CancellationToken
        );

    [Fact]
    public async Task Register_ValidRequest_ReturnsAccessTokenAndSetsRefreshCookie()
    {
        using var response = await RegisterAsync(Client, "mel@example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(CancellationToken);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(CancellationToken);
        Assert.False(string.IsNullOrEmpty(token?.AccessToken));
        Assert.Equal(30 * 60, token?.ExpiresIn);
        // The refresh token is only ever in the cookie.
        Assert.DoesNotContain("refresh", body, StringComparison.OrdinalIgnoreCase);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("__Secure-wwg-refresh=", cookie, StringComparison.Ordinal);
        foreach (
            var attribute in new[] { "httponly", "secure", "samesite=strict", "path=/api/auth" }
        )
        {
            Assert.Contains(attribute, cookie, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Register_ValidRequest_StoresTheUserWithTrimmedNamesAndEmailAsUserName()
    {
        using var response = await RegisterAsync(
            Client,
            "  mel@example.com ",
            firstName: "  Mel ",
            lastName: " Green  "
        );
        response.EnsureSuccessStatusCode();

        var user = await WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(CancellationToken));
        Assert.Equal("mel@example.com", user.Email);
        Assert.Equal("mel@example.com", user.UserName);
        Assert.Equal("Mel", user.FirstName);
        Assert.Equal("Green", user.LastName);
    }

    [Fact]
    public async Task Register_EmailAlreadyUsedInAnyCase_Returns409()
    {
        (await RegisterAsync(Client, "mel@example.com")).EnsureSuccessStatusCode();

        using var response = await RegisterAsync(CreateClient(), "MEL@Example.com");

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("not-an-email", TestPassword, "Test", "User", "email")]
    [InlineData("a@example.com", "short", "Test", "User", "password")]
    [InlineData("a@example.com", TestPassword, "   ", "User", "firstName")]
    [InlineData("a@example.com", TestPassword, "Test", "", "lastName")]
    public async Task Register_InvalidField_ReturnsValidationProblemForThatField(
        string email,
        string password,
        string firstName,
        string lastName,
        string field
    )
    {
        using var response = await RegisterAsync(Client, email, password, firstName, lastName);

        await response.AssertValidationProblemAsync(field);
    }

    [Fact]
    public async Task Login_CorrectPassword_ReturnsAccessTokenAndSetsRefreshCookie()
    {
        (await RegisterAsync(Client, "mel@example.com")).EnsureSuccessStatusCode();

        using var response = await LoginAsync(CreateClient(), "Mel@Example.com", TestPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(CancellationToken);
        Assert.False(string.IsNullOrEmpty(token?.AccessToken));
        Assert.Contains(
            response.Headers.GetValues("Set-Cookie"),
            c => c.StartsWith("__Secure-wwg-refresh=", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task Login_WrongPasswordOrUnknownEmail_ReturnTheSame401()
    {
        (await RegisterAsync(Client, "mel@example.com")).EnsureSuccessStatusCode();

        using var wrongPassword = await LoginAsync(
            CreateClient(),
            "mel@example.com",
            "wrong password"
        );
        using var unknownEmail = await LoginAsync(
            CreateClient(),
            "nobody@example.com",
            TestPassword
        );

        var first = await wrongPassword.AssertProblemAsync(HttpStatusCode.Unauthorized);
        var second = await unknownEmail.AssertProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal(first.Detail, second.Detail);
    }

    [Fact]
    public async Task Login_AfterFiveFailures_IsLockedOutEvenWithTheRightPassword()
    {
        (await RegisterAsync(Client, "mel@example.com")).EnsureSuccessStatusCode();
        for (var i = 0; i < 5; i++)
        {
            using var failed = await LoginAsync(
                CreateClient(),
                "mel@example.com",
                "wrong password"
            );
        }

        using var response = await LoginAsync(CreateClient(), "mel@example.com", TestPassword);

        var problem = await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal("Account locked", problem.Title);
    }

    [Fact]
    public async Task Login_LockoutEnded_SucceedsAgain()
    {
        (await RegisterAsync(Client, "mel@example.com")).EnsureSuccessStatusCode();
        for (var i = 0; i < 5; i++)
        {
            using var failed = await LoginAsync(
                CreateClient(),
                "mel@example.com",
                "wrong password"
            );
        }

        // Identity's lockout reads the system clock, not the injected TimeProvider, so the fake
        // clock can't move it on; end the lockout the way time would.
#pragma warning disable RS0030 // Must match Identity's own clock.
        var ended = DateTimeOffset.UtcNow.AddSeconds(-1);
#pragma warning restore RS0030
        await WithDbAsync(db =>
            db.Users.ExecuteUpdateAsync(
                u => u.SetProperty(x => x.LockoutEnd, ended),
                CancellationToken
            )
        );
        using var response = await LoginAsync(CreateClient(), "mel@example.com", TestPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
