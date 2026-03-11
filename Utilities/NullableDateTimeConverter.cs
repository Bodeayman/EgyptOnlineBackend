using System.Text.Json;
using System.Text.Json.Serialization;

namespace EgyptOnline.Utilities
{
    public class NullableDateTimeConverter : JsonConverter<DateTime?>
    {
        private readonly string[] _formats = new[]
        {
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-ddTHH:mm:ss.fffZ",
            "yyyy-MM-dd",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy/MM/dd",
            "MM/dd/yyyy"
        };

        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (DateTime.TryParse(value, out var result))
            {
                return result;
            }

            throw new JsonException($"Unable to parse \"{value}\" as a valid DateTime. Expected format like \"yyyy-MM-ddTHH:mm:ssZ\".");
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
