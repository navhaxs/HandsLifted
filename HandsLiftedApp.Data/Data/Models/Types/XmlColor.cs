using Avalonia.Media;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Types
{
    public class XmlColor : IXmlSerializable
    {
        private Color m_value = Colors.Black;

        public XmlColor() { }
        public XmlColor(Color source) { m_value = source; }

        public static implicit operator Color?(XmlColor o)
        {
            return o == null ? default(Color?) : o.m_value;
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

            this.m_value = Color.Parse(colorAsString); // convert "colorAsString" to Color
        }

        public XmlSchema GetSchema()
        {
            return null;
        }
    }
}
