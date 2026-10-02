using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Wwg.Api.Features.Account;
using Wwg.Api.Features.Auth;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>Welcome and email confirmation (step 52b, decision 0023).</summary>
public sealed partial class EmailConfirmationTests : ApiTest
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [GeneratedRegex(
        @"/confirm-email\?user=(?<user>[0-9a-f-]+)&code=(?<code>[A-Za-z0-9_-]+)",
        RegexOptions.None,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex ConfirmLink();

    /// <summary>The account and code an email's confirmation link carries.</summary>
    private static ConfirmEmailRequest LinkIn(string text)
    {
        var match = ConfirmLink().Match(text);
        Assert.True(match.Success, "No confirmation link in the email.");
        return new ConfirmEmailRequest(
            Guid.Parse(match.Groups["user"].Value),
            match.Groups["code"].Value
        );
    }

    private Task<HttpResponseMessage> ConfirmAsync(ConfirmEmailRequest request) =>
        Client.PostAsJsonAsync(
            new Uri("/api/auth/confirm-email", UriKind.Relative),
            request,
            Token
        );

    [Fact]
    public async Task Register_SendsAWelcomeWithALinkToConfirmTheAddress()
    {
        using var client = await CreateUserClientAsync("ada@example.com");

        var welcome = await Emails.WaitForWelcomeToAsync("ada@example.com");

        Assert.Equal("Welcome to Wasatch Wargamers: confirm your email", welcome.Subject);
        LinkIn(welcome.TextBody);
        Assert.False((await client.GetAsAsync<MeResponse>("/api/me"))!.EmailConfirmed);
    }

    [Fact]
    public async Task ConfirmEmail_TheWelcomesLink_ConfirmsTheAddress()
    {
        using var client = await CreateUserClientAsync("ada@example.com");
        var welcome = await Emails.WaitForWelcomeToAsync("ada@example.com");

        using var confirmed = await ConfirmAsync(LinkIn(welcome.TextBody));

        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        Assert.True((await client.GetAsAsync<MeResponse>("/api/me"))!.EmailConfirmed);
    }

    [Fact]
    public async Task ConfirmEmail_AWrongCode_IsAValidationError()
    {
        using var client = await CreateUserClientAsync("ada@example.com");
        var link = LinkIn((await Emails.WaitForWelcomeToAsync("ada@example.com")).TextBody);

        using var confirmed = await ConfirmAsync(link with { Code = "bm90LWEtdG9rZW4" });

        await confirmed.AssertValidationProblemAsync("code");
    }

    [Fact]
    public async Task ChangeEmail_SendsALinkToTheNewAddress_AndItsUnconfirmedTillThen()
    {
        using var client = await CreateUserClientAsync("ada@example.com");
        await ConfirmAsync(
            LinkIn((await Emails.WaitForWelcomeToAsync("ada@example.com")).TextBody)
        );

        using var changed = await client.PutAsJsonAsync(
            new Uri("/api/me/email", UriKind.Relative),
            new ChangeEmailRequest("lovelace@example.com", TestPassword),
            Token
        );
        changed.EnsureSuccessStatusCode();

        var email = await Emails.WaitForEmailToAsync("lovelace@example.com");
        Assert.Equal("Confirm your new Wasatch Wargamers email", email.Subject);
        using var confirmed = await ConfirmAsync(LinkIn(email.TextBody));
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
    }

    [Fact]
    public async Task SendConfirmationEmail_Unconfirmed_SendsANewLink()
    {
        using var client = await CreateUserClientAsync("ada@example.com");

        using var sent = await client.PostAsync(
            new Uri("/api/me/confirmation-email", UriKind.Relative),
            null,
            Token
        );

        Assert.Equal(HttpStatusCode.NoContent, sent.StatusCode);
        var email = await Emails.WaitForEmailToAsync("ada@example.com");
        using var confirmed = await ConfirmAsync(LinkIn(email.TextBody));
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
    }
}
