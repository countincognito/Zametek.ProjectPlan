using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zametek.ProjectPlan.CommandLine
{
    // Writes a duration as ISO 8601 and reads it as such - as a string - in place of the "00:00:05" .NET writes.
    internal sealed class IsoDurationConverter
        : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            string? text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;

            return IsoDurationHelper.TryParse(text, out TimeSpan duration)
                ? duration
                : throw new JsonException(Resource.ProjectPlan.Messages.Message_ServeErrorMustBeADuration);
        }

        public override void Write(
            Utf8JsonWriter writer,
            TimeSpan value,
            JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.WriteStringValue(IsoDurationHelper.ToString(value));
        }
    }
}
