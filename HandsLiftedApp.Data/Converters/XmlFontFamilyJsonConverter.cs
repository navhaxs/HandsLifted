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
