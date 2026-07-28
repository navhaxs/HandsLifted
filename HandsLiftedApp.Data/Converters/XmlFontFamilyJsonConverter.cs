using System;
using Newtonsoft.Json;
using Avalonia.Media;
using HandsLiftedApp.Data.Data.Models.Types;

public class XmlFontFamilyJsonConverter : JsonConverter<XmlFontFamily>
{
    public override XmlFontFamily ReadJson(JsonReader reader, Type objectType, XmlFontFamily existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        if (reader.TokenType == JsonToken.String)
        {
            string fontFamilyAsString = reader.Value.ToString();
            return fontFamilyAsString;
        }

        if (reader.TokenType == JsonToken.StartObject)
        {
            // Old appstate.json files (pre-converter) serialized this as "{}". Skip and fall back to default.
            reader.Skip();
            return new XmlFontFamily();
        }

        throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing XmlFontFamily");
    }

    public override void WriteJson(JsonWriter writer, XmlFontFamily value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteValue((string)value);
    }
}
