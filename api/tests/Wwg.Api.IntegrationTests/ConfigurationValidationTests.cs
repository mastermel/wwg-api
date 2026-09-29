using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests;

public sealed class ConfigurationValidationTests
{
    [Fact]
    public void Startup_PublicUrlMissingOutsideDevelopment_Fails()
    {
        var exception = StartupException(builder => builder.UseEnvironment("Production"));

        Assert.Contains("PublicUrl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_PublicUrlRelative_Fails()
    {
        var exception = StartupException(builder => builder.UseSetting("App:PublicUrl", "/wwg"));

        Assert.Contains("absolute http or https", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_ConnectionStringMissing_Fails()
    {
        var exception = StartupException(builder =>
            builder.UseSetting("ConnectionStrings:Default", "")
        );

        Assert.Contains("ConnectionStrings:Default", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_DataProtectionKeysPathMissing_Fails()
    {
        var exception = StartupException(builder =>
            builder.UseSetting("Auth:DataProtectionKeysPath", "")
        );

        Assert.Contains("DataProtectionKeysPath", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_SmtpHostWithoutFromAddress_Fails()
    {
        var exception = StartupException(builder =>
            builder.UseSetting("Smtp:Host", "smtp.example.com").UseSetting("Smtp:FromAddress", "")
        );

        Assert.Contains("FromAddress", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ForwardedHeaders:KnownProxies:0", "not-an-ip")]
    [InlineData("ForwardedHeaders:KnownNetworks:0", "10.0.0.0")]
    public void Startup_InvalidProxySetting_Fails(string key, string value)
    {
        var exception = StartupException(builder => builder.UseSetting(key, value));

        Assert.Contains($"'{value}'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("RateLimits:Auth:PermitLimit", "0")]
    [InlineData("RateLimits:Refresh:Window", "00:00:00")]
    [InlineData("RateLimits:Email:PermitLimit", "-1")]
    public void Startup_InvalidRateLimit_Fails(string key, string value)
    {
        var exception = StartupException(builder => builder.UseSetting(key, value));

        Assert.Contains(key.Split(':')[^1], exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_AdminEmailInvalid_Fails()
    {
        var exception = StartupException(builder =>
            builder.UseSetting("Admin:Emails:0", "not an email")
        );

        Assert.Contains("'not an email'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_BackupKeepZero_Fails()
    {
        var exception = StartupException(builder => builder.UseSetting("Backup:Keep", "0"));

        Assert.Contains("Keep", exception.Message, StringComparison.Ordinal);
    }

    private static OptionsValidationException StartupException(Action<IWebHostBuilder> configure)
    {
        using var factory = new WwgApiFactory();
        using var configured = factory.WithWebHostBuilder(configure);

        return Assert.Throws<OptionsValidationException>(() => configured.CreateClient());
    }
}
