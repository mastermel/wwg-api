using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Wwg.Api.Features.Auth;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

public sealed partial class PasswordResetTests : ApiTest
{
    private const string NewPassword = "a brand new password";

    [GeneratedRegex(
        @"(https?://\S+/reset-password\?email=(?<email>[^&\s]+)&code=(?<code>[\w-]+))",
        RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex ResetLink();

    private Task<HttpResponseMessage> ForgotAsync(string email) =>
        Client.PostAsJsonAsync(
            new Uri("/api/auth/forgot-password", UriKind.Relative),
            new ForgotPasswordRequest(email),
            CancellationToken
        );

    private Task<HttpResponseMessage> ResetAsync(
        string email,
        string code,
        string password = NewPassword
    ) =>
        Client.PostAsJsonAsync(
            new Uri("/api/auth/reset-password", UriKind.Relative),
            new ResetPasswordRequest(email, code, password),
            CancellationToken
        );

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        CreateClient()
            .PostAsJsonAsync(
                new Uri("/api/auth/login", UriKind.Relative),
                new LoginRequest(email, password),
                CancellationToken
            );

    /// <summary>Asks for a reset and returns the code from the emailed link.</summary>
    private async Task<string> EmailedCodeAsync(string email = "mel@example.com")
    {
        using var response = await ForgotAsync(email);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var sent = await Emails.WaitForEmailToAsync(email);
        return ResetLink().Match(sent.TextBody).Groups["code"].Value;
    }

    [Fact]
    public async Task ForgotPassword_KnownEmail_EmailsALinkToTheAppsPublicUrl()
    {
        (await CreateUserClientAsync("mel@example.com")).Dispose();

        using var response = await ForgotAsync("MEL@example.com");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var sent = await Emails.WaitForEmailToAsync("mel@example.com");
        var link = ResetLink().Match(sent.TextBody);
        Assert.True(link.Success, sent.TextBody);
        // App:PublicUrl in Development, never the request's host.
        Assert.StartsWith(
            "http://localhost:5173/reset-password?",
            link.Value,
            StringComparison.Ordinal
        );
        Assert.Contains(link.Value, WebUtility.HtmlDecode(sent.HtmlBody), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_SucceedsTheSameWayAndSendsNothing()
    {
        using var response = await ForgotAsync("nobody@example.com");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await Task.Delay(TimeSpan.FromMilliseconds(200), CancellationToken);
        Assert.Empty(Emails.Sent);
    }

    [Fact]
    public async Task ResetPassword_EmailedCode_ChangesThePassword()
    {
        (await CreateUserClientAsync("mel@example.com")).Dispose();
        var code = await EmailedCodeAsync();

        using var reset = await ResetAsync("mel@example.com", code);
        using var newLogin = await LoginAsync("mel@example.com", NewPassword);
        using var oldLogin = await LoginAsync("mel@example.com", TestPassword);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_Succeeded_EndsExistingSessions()
    {
        using var existing = await CreateUserClientAsync("mel@example.com");
        var code = await EmailedCodeAsync();

        (await ResetAsync("mel@example.com", code)).EnsureSuccessStatusCode();
        using var me = await existing.GetAsync(
            new Uri("/api/me", UriKind.Relative),
            CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_CodeUsedTwice_FailsTheSecondTime()
    {
        (await CreateUserClientAsync("mel@example.com")).Dispose();
        var code = await EmailedCodeAsync();
        (await ResetAsync("mel@example.com", code)).EnsureSuccessStatusCode();

        using var again = await ResetAsync("mel@example.com", code, "yet another password");

        await again.AssertValidationProblemAsync("code");
    }

    [Theory]
    [InlineData("mel@example.com", "not-a-real-code")]
    [InlineData("nobody@example.com", null)]
    public async Task ResetPassword_BadCodeOrUnknownEmail_GetTheSameCodeError(
        string email,
        string? code
    )
    {
        (await CreateUserClientAsync("mel@example.com")).Dispose();
        code ??= await EmailedCodeAsync();

        using var response = await ResetAsync(email, code);

        await response.AssertValidationProblemAsync("code");
    }

    [Fact]
    public async Task ResetPassword_LinkExpired_Fails()
    {
        // Identity checks the link's age against the system clock (not the injected TimeProvider),
        // so shorten the lifetime and really wait.
        using var app = App.WithWebHostBuilder(builder =>
            builder.UseSetting("Auth:PasswordResetLinkLifetime", "00:00:01")
        );
        using var client = app.CreateClient();
        (await RegisterAsync(client, "mel@example.com")).EnsureSuccessStatusCode();
        (
            await client.PostAsJsonAsync(
                new Uri("/api/auth/forgot-password", UriKind.Relative),
                new ForgotPasswordRequest("mel@example.com"),
                CancellationToken
            )
        ).EnsureSuccessStatusCode();
        var sent = await Emails.WaitForEmailToAsync("mel@example.com");
        var code = ResetLink().Match(sent.TextBody).Groups["code"].Value;

        await Task.Delay(TimeSpan.FromSeconds(1.5), CancellationToken);
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/auth/reset-password", UriKind.Relative),
            new ResetPasswordRequest("mel@example.com", code, NewPassword),
            CancellationToken
        );

        await response.AssertValidationProblemAsync("code");
    }

    [Fact]
    public async Task ResetPassword_TooShortPassword_IsAValidationErrorOnNewPassword()
    {
        (await CreateUserClientAsync("mel@example.com")).Dispose();
        var code = await EmailedCodeAsync();

        using var response = await ResetAsync("mel@example.com", code, "short");

        await response.AssertValidationProblemAsync("newPassword");
    }

    [Fact]
    public async Task ResetPassword_LockedOutAccount_CanSignInAgain()
    {
        (await CreateUserClientAsync("mel@example.com")).Dispose();
        for (var i = 0; i < 5; i++)
        {
            using var failed = await LoginAsync("mel@example.com", "wrong password");
        }
        var code = await EmailedCodeAsync();

        (await ResetAsync("mel@example.com", code)).EnsureSuccessStatusCode();
        using var login = await LoginAsync("mel@example.com", NewPassword);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }
}
