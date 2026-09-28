using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wwg.Api.IntegrationTests.Support;

/// <summary>Reads API responses the way the API writes them: web defaults, enums as strings.</summary>
internal static class TestJson
{
    public static JsonSerializerOptions Options { get; } =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static Task<T?> ReadAsAsync<T>(this HttpContent content) =>
        content.ReadFromJsonAsync<T>(Options, TestContext.Current.CancellationToken);

    public static Task<T?> GetAsAsync<T>(this HttpClient client, string path) =>
        client.GetFromJsonAsync<T>(
            new Uri(path, UriKind.Relative),
            Options,
            TestContext.Current.CancellationToken
        );
}
