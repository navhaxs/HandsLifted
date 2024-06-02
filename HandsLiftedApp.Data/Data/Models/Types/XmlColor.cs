using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Avalonia.Media;

namespace HandsLiftedApp.Data.Data.Models.Types
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
}
