using System;
using Newtonsoft.Json;
using Avalonia.Media;
using HandsLiftedApp.Data.Data.Models.Types;

public class XmlFontWeightJsonConverter : JsonConverter<XmlFontWeight>
{
    public override XmlFontWeight ReadJson(JsonReader reader, Type objectType, XmlFontWeight existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        if (reader.TokenType == JsonToken.Integer || reader.TokenType == JsonToken.Float)
        {
            int weight = Convert.ToInt32(reader.Value);
            return weight;
        }

        if (reader.TokenType == JsonToken.String)
        {
            string weightName = reader.Value.ToString();
            if (Enum.TryParse<FontWeight>(weightName, true, out FontWeight fontWeight))
            {
                return new XmlFontWeight(fontWeight);
            }
        }

        throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing XmlFontWeight");
    }

    public override void WriteJson(JsonWriter writer, XmlFontWeight value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        // Write the OpenType weight value
        writer.WriteValue((int)value);
    }
}
