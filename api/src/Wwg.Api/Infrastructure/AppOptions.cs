using System.ComponentModel.DataAnnotations;

namespace Wwg.Api.Infrastructure;

/// <summary>General app settings (<c>App</c> section).</summary>
internal sealed class AppOptions : IValidatableObject
{
    public const string SectionName = "App";

    /// <summary>
    /// The URL users open the app at (e.g. <c>https://wwg.example.com</c>). Used to build links in
    /// emails, such as password reset links.
    /// </summary>
    [Required]
    public Uri? PublicUrl { get; set; }

    /// <summary>Serve Swagger UI outside development too.</summary>
    public bool EnableSwaggerUi { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PublicUrl is { } url && !(url.IsAbsoluteUri && url.Scheme is "http" or "https"))
        {
            yield return new ValidationResult(
                "PublicUrl must be an absolute http or https URL.",
                [nameof(PublicUrl)]
            );
        }
    }
}
