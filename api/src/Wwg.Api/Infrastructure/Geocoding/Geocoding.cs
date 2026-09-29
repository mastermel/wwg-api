using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Wwg.Api.Infrastructure.Geocoding;

/// <summary>
/// Place search (<c>Geocoding</c> section, decision 0009). With a MapTiler key, searches go to
/// MapTiler; without one, to Photon's public server (no key, fair use). The key only ever leaves
/// the server in requests to MapTiler.
/// </summary>
internal sealed class GeocodingOptions
{
    public const string SectionName = "Geocoding";

    /// <summary>A MapTiler API key (a secret: user-secrets in development, an env var in production).</summary>
    public string? MapTilerApiKey { get; set; }

    public bool UsesMapTiler => !string.IsNullOrWhiteSpace(MapTilerApiKey);
}

/// <summary>A place found by a search, in degrees.</summary>
internal sealed record Place(
    string Name,
    string Description,
    double Longitude,
    double Latitude,
    (double West, double South, double East, double North)? Bounds
);

/// <summary>Searches for places by name. Throws <see cref="HttpRequestException"/> when the service fails.</summary>
internal interface IGeocoder
{
    Task<IReadOnlyList<Place>> SearchAsync(string query, CancellationToken cancellationToken);
}

/// <summary>MapTiler's forward geocoding: areas and settlements, not addresses or roads.</summary>
internal sealed class MapTilerGeocoder(HttpClient http, IOptions<GeocodingOptions> options)
    : IGeocoder
{
    // Places an Umpire would frame a campaign around, from countries down to villages.
    private const string Types =
        "country,region,subregion,county,joint_municipality,municipality,municipal_district,locality,place";

    public async Task<IReadOnlyList<Place>> SearchAsync(
        string query,
        CancellationToken cancellationToken
    )
    {
        var url =
            $"https://api.maptiler.com/geocoding/{Uri.EscapeDataString(query)}.json"
            + $"?key={Uri.EscapeDataString(options.Value.MapTilerApiKey ?? "")}"
            + $"&limit=8&language=en&types={Types}";
        var result = await http.GetFromJsonAsync<Collection>(url, cancellationToken);
        return
        [
            .. (result?.Features ?? [])
                .Where(f => f.Center is [_, _])
                .Select(f => new Place(
                    f.Text ?? f.PlaceName ?? "",
                    f.PlaceName ?? f.Text ?? "",
                    f.Center![0], // Checked above: [longitude, latitude].
                    f.Center[1],
                    f.Bbox is [var west, var south, var east, var north]
                        ? (west, south, east, north)
                        : null
                )),
        ];
    }

    private sealed record Collection(IReadOnlyList<Feature>? Features);

    private sealed record Feature(
        string? Text,
        [property: JsonPropertyName("place_name")] string? PlaceName,
        double[]? Center,
        double[]? Bbox
    );
}

/// <summary>Photon (komoot's public server, OpenStreetMap data): no key, fair use.</summary>
internal sealed class PhotonGeocoder(HttpClient http) : IGeocoder
{
    public async Task<IReadOnlyList<Place>> SearchAsync(
        string query,
        CancellationToken cancellationToken
    )
    {
        var url =
            $"https://photon.komoot.io/api/?q={Uri.EscapeDataString(query)}&limit=8&lang=en"
            + "&layer=city&layer=county&layer=state&layer=country&layer=district&layer=locality";
        var result = await http.GetFromJsonAsync<Collection>(url, cancellationToken);
        return
        [
            .. (result?.Features ?? [])
                .Where(f => f.Geometry?.Coordinates is [_, _])
                .Select(f => new Place(
                    f.Properties?.Name ?? "",
                    string.Join(
                        ", ",
                        new[]
                        {
                            f.Properties?.Name,
                            f.Properties?.State,
                            f.Properties?.Country,
                        }.Where(part => !string.IsNullOrEmpty(part))
                    ),
                    f.Geometry!.Coordinates![0], // Checked above: [longitude, latitude].
                    f.Geometry.Coordinates[1],
                    // Photon's extent is [west, north, east, south].
                    f.Properties?.Extent
                        is [var west, var north, var east, var south]
                        ? (west, south, east, north)
                        : null
                )),
        ];
    }

    private sealed record Collection(IReadOnlyList<Feature>? Features);

    private sealed record Feature(Geometry? Geometry, Properties? Properties);

    private sealed record Geometry(double[]? Coordinates);

    private sealed record Properties(
        string? Name,
        string? State,
        string? Country,
        double[]? Extent
    );
}

internal static class GeocodingExtensions
{
    public static IServiceCollection AddGeocoding(this IServiceCollection services)
    {
        services.AddValidatedOptions<GeocodingOptions>(GeocodingOptions.SectionName);
        services.AddHttpClient<MapTilerGeocoder>(ConfigureClient);
        services.AddHttpClient<PhotonGeocoder>(ConfigureClient);
        services.AddTransient<IGeocoder>(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<GeocodingOptions>>().Value.UsesMapTiler
                ? serviceProvider.GetRequiredService<MapTilerGeocoder>()
                : serviceProvider.GetRequiredService<PhotonGeocoder>()
        );
        return services;
    }

    private static void ConfigureClient(HttpClient client)
    {
        // A search that takes longer isn't worth waiting for.
        client.Timeout = TimeSpan.FromSeconds(10);
        // Photon's usage policy asks for an identifying User-Agent.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("WWG-Campaigner/1.0");
    }
}
