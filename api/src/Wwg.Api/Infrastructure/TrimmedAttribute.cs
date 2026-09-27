using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wwg.Api.Infrastructure;

/// <summary>
/// Trims leading and trailing whitespace from a string property when a request body is read, so
/// validation and saving see the tidied value. For name-like fields and emails; never passwords.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class TrimmedAttribute() : JsonConverterAttribute(typeof(TrimmedStringConverter))
{
    private sealed class TrimmedStringConverter : JsonConverter<string>
    {
        public override bool HandleNull => false;

        public override string? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            return reader.GetString()?.Trim();
        }

        public override void Write(
            Utf8JsonWriter writer,
            string value,
            JsonSerializerOptions options
        )
        {
            writer.WriteStringValue(value);
        }
    }
}
