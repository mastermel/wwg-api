using System.Reflection;

namespace Wwg.Api.Infrastructure;

internal static class BuildTime
{
    /// <summary>
    /// True when the build launches the app only to write <c>api/openapi.json</c> (the build-time
    /// tool's entry assembly is <c>GetDocument.Insider</c>). The app then runs in Production with
    /// no real settings, so startup work with side effects (migrations) and fail-fast config
    /// validation must be skipped. The document only needs endpoint metadata.
    /// </summary>
    public static bool IsGeneratingOpenApiDocument { get; } =
        string.Equals(
            Assembly.GetEntryAssembly()?.GetName().Name,
            "GetDocument.Insider",
            StringComparison.Ordinal
        );
}
