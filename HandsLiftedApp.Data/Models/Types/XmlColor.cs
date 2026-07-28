using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Avalonia.Media;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HandsLiftedApp.Data.Data.Models.Types
{
    // Newtonsoft can't see m_value (private, no public props), so without this converter
    // JsonConvert.SerializeObject writes "{}" for any XmlColor field and the color is lost
    // on the next JsonConvert.DeserializeObject (used for appstate.json / AppPreferencesViewModel).
    [JsonConverter(typeof(XmlColorJsonConverter))]
    public class XmlColor : IXmlSerializable
    {
        private Color m_value = Colors.Black;

        public XmlColor() { }
        public XmlColor(Color source) { m_value = source; }

        public static implicit operator Color?(XmlColor o)
        {
            return o == null ? default(Color?) : o.m_value;
        }

        public static implicit operator XmlColor?(string colorAsString)
        {
            try
            {
                return new XmlColor(Color.Parse(colorAsString)); // convert "colorAsString" to Color
            }
            catch (Exception e)
            {
                // Log
                return new XmlColor(Colors.Transparent); // set default color if parsing fails (e.g. if colorAsString is empty)
            }
        }

        public static implicit operator XmlColor(Color? o)
        {
            return o == null ? null : new XmlColor(o.Value);
        }

        public static implicit operator Color(XmlColor o)
        {
            return o == null ? default(Color) : o.m_value;
        }

        public static implicit operator XmlColor(Color o)
        {
            return o == default(Color) ? null : new XmlColor(o);
        }

        public void WriteXml(XmlWriter writer)
        {
            string colorAsString = m_value.ToString(); // convert Color to string
            writer.WriteString(colorAsString);
        }

        public void ReadXml(XmlReader reader)
        {
            string colorAsString = reader.ReadElementContentAsString();

            try
            {
                this.m_value = Color.Parse(colorAsString); // convert "colorAsString" to Color
            }
            catch (Exception e)
            {
                // Log
                this.m_value = Colors.Transparent; // set default color if parsing fails (e.g. if colorAsString is empty)
            }
        }

        public XmlSchema GetSchema()
        {
            return null;
        }
    }

    public class XmlColorJsonConverter : JsonConverter<XmlColor>
    {
        public override void WriteJson(JsonWriter writer, XmlColor value, JsonSerializer serializer)
        {
            writer.WriteValue(((Color)value).ToString());
        }

        public override XmlColor ReadJson(JsonReader reader, Type objectType, XmlColor existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            // JToken.Load fully consumes whatever shape is at the reader's current position (string,
            // object, null...) so the reader stays in sync afterwards. Needed because old appstate.json
            // files written before this converter existed have "{}" here instead of a color string.
            var token = JToken.Load(reader);
            var colorAsString = token.Type == JTokenType.String ? token.Value<string>() : null;

            if (string.IsNullOrEmpty(colorAsString))
                return new XmlColor(Colors.Transparent);

            try
            {
                return new XmlColor(Color.Parse(colorAsString));
            }
            catch (Exception)
            {
                return new XmlColor(Colors.Transparent);
            }
        }
    }
}
